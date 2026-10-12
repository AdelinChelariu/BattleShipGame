using BattleshipGame.Shared;

namespace BattleshipServer
{
    /// <summary>
    /// Gestionează lobby-ul: clienți conectați, provocări și formarea meciului.
    /// Fiecare client intră în lobby imediat după conectare.
    /// Când doi jucători acceptă un meci, se creează o GameSession.
    /// </summary>
    public class LobbyManager
    {
        private readonly List<LobbyEntry> _entries = new();
        private readonly object _lock = new();

        // ──────────────────────────────────────────────
        // Entry (starea unui client în lobby)
        // ──────────────────────────────────────────────

        private class LobbyEntry
        {
            public ClientHandler Handler { get; }
            public string Name { get; set; } = "";

            /// <summary>Numele jucătorului care a trimis o provocare acestui jucător</summary>
            public string? ChallengedByName { get; set; }

            /// <summary>Numele jucătorului pe care îl provoacă acest jucător</summary>
            public string? ChallengingSentTo { get; set; }

            public bool HasName => !string.IsNullOrEmpty(Name);

            public LobbyEntry(ClientHandler handler) => Handler = handler;
        }

        // ──────────────────────────────────────────────
        // Adăugare client nou
        // ──────────────────────────────────────────────

        /// <summary>
        /// Înregistrează un handler în lobby și setează callback-urile.
        /// Apelat ÎNAINTE de pornirea thread-ului de ascultare.
        /// </summary>
        public void AddClient(ClientHandler handler)
        {
            lock (_lock)
            {
                var entry = new LobbyEntry(handler);
                _entries.Add(entry);

                handler.OnMessageReceived = HandleLobbyMessage;
                handler.OnDisconnected    = HandleDisconnect;

                handler.Send(Protocol.LOBBY_WELCOME);
                Console.WriteLine($"[Lobby] Client {handler.PlayerId} conectat. Total: {_entries.Count}");
            }
        }

        // ──────────────────────────────────────────────
        // Router mesaje lobby
        // ──────────────────────────────────────────────

        private void HandleLobbyMessage(int connId, string rawMessage)
        {
            lock (_lock)
            {
                var entry = FindById(connId);
                if (entry == null) return;

                var (type, args) = Protocol.Parse(rawMessage);

                switch (type)
                {
                    case Protocol.JOIN:
                        HandleJoin(entry, args);
                        break;

                    case Protocol.CHALLENGE:
                        HandleChallenge(entry, args);
                        break;

                    case Protocol.CHALLENGE_ACCEPT:
                        HandleChallengeAccept(entry);
                        break;

                    case Protocol.CHALLENGE_DECLINE:
                        HandleChallengeDecline(entry);
                        break;

                    case Protocol.LOBBY_CHAT:
                        HandleLobbyChat(entry, args);
                        break;

                    default:
                        entry.Handler.Send(Protocol.Build(Protocol.ERROR, "Ești în lobby. Trimite JOIN <nume> mai întâi."));
                        break;
                }
            }
        }

        // ──────────────────────────────────────────────
        // JOIN
        // ──────────────────────────────────────────────

        private void HandleJoin(LobbyEntry entry, string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                entry.Handler.Send(Protocol.Build(Protocol.ERROR, "Foloseste: JOIN <nume> (fara spatii)"));
                return;
            }

            string name = args[0].Trim();

            if (name.Contains(' '))
            {
                entry.Handler.Send(Protocol.Build(Protocol.ERROR, "Numele nu poate conține spații."));
                return;
            }

            if (name.Length > 20)
            {
                entry.Handler.Send(Protocol.Build(Protocol.ERROR, "Numele este prea lung (max 20 caractere)."));
                return;
            }

            // Verifică unicitate
            if (_entries.Any(e => e != entry && e.Name == name))
            {
                entry.Handler.Send(Protocol.Build(Protocol.ERROR, $"Numele '{name}' este deja folosit."));
                return;
            }

            entry.Name = name;
            Console.WriteLine($"[Lobby] Client {entry.Handler.PlayerId} s-a alăturat ca '{name}'.");

            BroadcastLobbyUpdate();
        }

        // ──────────────────────────────────────────────
        // CHALLENGE
        // ──────────────────────────────────────────────

        private void HandleChallenge(LobbyEntry challenger, string[] args)
        {
            if (!challenger.HasName)
            {
                challenger.Handler.Send(Protocol.Build(Protocol.ERROR, "Trebuie să trimiți JOIN <nume> înainte."));
                return;
            }

            if (args.Length == 0)
            {
                challenger.Handler.Send(Protocol.Build(Protocol.ERROR, "Foloseste: CHALLENGE <nume>"));
                return;
            }

            string targetName = args[0];

            if (targetName == challenger.Name)
            {
                challenger.Handler.Send(Protocol.Build(Protocol.ERROR, "Nu poți să te provoci pe tine însuți."));
                return;
            }

            var target = FindByName(targetName);
            if (target == null)
            {
                challenger.Handler.Send(Protocol.Build(Protocol.ERROR, $"Jucătorul '{targetName}' nu este în lobby."));
                return;
            }

            if (target.ChallengedByName != null)
            {
                challenger.Handler.Send(Protocol.Build(Protocol.ERROR, $"'{targetName}' are deja o provocare în așteptare."));
                return;
            }

            if (challenger.ChallengingSentTo != null)
            {
                challenger.Handler.Send(Protocol.Build(Protocol.ERROR, "Ai deja o provocare trimisă. Așteaptă răspunsul."));
                return;
            }

            challenger.ChallengingSentTo = targetName;
            target.ChallengedByName      = challenger.Name;

            target.Handler.Send(Protocol.Build(Protocol.CHALLENGED_BY, challenger.Name));
            Console.WriteLine($"[Lobby] '{challenger.Name}' a provocat '{targetName}'.");
        }

        // ──────────────────────────────────────────────
        // CHALLENGE_ACCEPT
        // ──────────────────────────────────────────────

        private void HandleChallengeAccept(LobbyEntry accepter)
        {
            if (accepter.ChallengedByName == null)
            {
                accepter.Handler.Send(Protocol.Build(Protocol.ERROR, "Nu ai nicio provocare de acceptat."));
                return;
            }

            var challenger = FindByName(accepter.ChallengedByName);
            if (challenger == null)
            {
                // Provocatorul s-a deconectat între timp
                accepter.ChallengedByName = null;
                accepter.Handler.Send(Protocol.Build(Protocol.ERROR, "Provocatorul s-a deconectat."));
                return;
            }

            Console.WriteLine($"[Lobby] Meci: '{challenger.Name}' vs '{accepter.Name}'");

            // Scoatem ambii din lobby înainte de a le modifica callback-urile
            _entries.Remove(challenger);
            _entries.Remove(accepter);

            BroadcastLobbyUpdate();

            // Notificăm că meciul e gata
            challenger.Handler.Send(Protocol.GAME_READY);
            accepter.Handler.Send(Protocol.GAME_READY);

            // Pornim game session pe un thread nou — pasăm și numele jucătorilor
            var session = new GameSession(challenger.Handler, challenger.Name, accepter.Handler, accepter.Name);
            var t = new Thread(session.Start) { IsBackground = true, Name = "GameSession" };
            t.Start();
        }

        // ──────────────────────────────────────────────
        // CHALLENGE_DECLINE
        // ──────────────────────────────────────────────

        private void HandleChallengeDecline(LobbyEntry decliner)
        {
            if (decliner.ChallengedByName == null)
            {
                decliner.Handler.Send(Protocol.Build(Protocol.ERROR, "Nu ai nicio provocare de respins."));
                return;
            }

            string challengerName = decliner.ChallengedByName;
            decliner.ChallengedByName = null;

            var challenger = FindByName(challengerName);
            if (challenger != null)
            {
                challenger.ChallengingSentTo = null;
                challenger.Handler.Send(Protocol.Build(Protocol.CHALLENGE_DECLINED, decliner.Name));
            }

            Console.WriteLine($"[Lobby] '{decliner.Name}' a respins provocarea lui '{challengerName}'.");
        }

        // ──────────────────────────────────────────────
        // Chat lobby
        // ──────────────────────────────────────────────

        private void HandleLobbyChat(LobbyEntry sender, string[] args)
        {
            if (!sender.HasName || args.Length == 0) return;

            string message  = args[0].Trim();
            if (string.IsNullOrEmpty(message)) return;

            string broadcast = Protocol.Build(Protocol.LOBBY_CHAT, $"[{sender.Name}]: {message}");
            foreach (var e in _entries)
                e.Handler.Send(broadcast);

            Console.WriteLine($"[Lobby Chat] {sender.Name}: {message}");
        }

        // ──────────────────────────────────────────────
        // Deconectare
        // ──────────────────────────────────────────────

        private void HandleDisconnect(int connId)
        {
            lock (_lock)
            {
                var entry = FindById(connId);
                if (entry == null) return;

                Console.WriteLine($"[Lobby] '{(entry.HasName ? entry.Name : $"Client{connId}")}' s-a deconectat.");

                // Dacă era provocat, notifică provocatorul
                if (entry.ChallengedByName != null)
                {
                    var challenger = FindByName(entry.ChallengedByName);
                    if (challenger != null)
                    {
                        challenger.ChallengingSentTo = null;
                        challenger.Handler.Send(Protocol.Build(Protocol.ERROR, $"'{entry.Name}' s-a deconectat."));
                    }
                }

                // Dacă provoca pe cineva, notifică ținta
                if (entry.ChallengingSentTo != null)
                {
                    var target = FindByName(entry.ChallengingSentTo);
                    if (target != null)
                    {
                        target.ChallengedByName = null;
                        target.Handler.Send(Protocol.Build(Protocol.ERROR, $"'{entry.Name}' s-a deconectat."));
                    }
                }

                _entries.Remove(entry);
                BroadcastLobbyUpdate();
            }
        }

        // ──────────────────────────────────────────────
        // Broadcast
        // ──────────────────────────────────────────────

        private void BroadcastLobbyUpdate()
        {
            var names = _entries.Where(e => e.HasName).Select(e => e.Name).ToArray();

            // Construiesc manual mesajul ca să suport 0 jucători
            string msg = names.Length > 0
                ? Protocol.Build(Protocol.LOBBY_UPDATE, names.Cast<object>().ToArray())
                : Protocol.LOBBY_UPDATE;

            foreach (var e in _entries)
                e.Handler.Send(msg);
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        private LobbyEntry? FindById(int connId) =>
            _entries.FirstOrDefault(e => e.Handler.PlayerId == connId);

        private LobbyEntry? FindByName(string name) =>
            _entries.FirstOrDefault(e => e.Name == name);
    }
}
