using BattleshipGame.Shared;

namespace BattleshipServer
{
    public enum GamePhase
    {
        WaitingForPlacement,
        Playing,
        GameOver
    }

    /// <summary>
    /// Gestionează o sesiune completă (inclusiv rematch-uri).
    /// Thread-urile de ascultare sunt pornite de Server — Start() doar redirecționează callback-urile.
    /// </summary>
    public class GameSession
    {
        private readonly ClientHandler _player1;
        private readonly ClientHandler _player2;
        private readonly string        _name1;
        private readonly string        _name2;

        private Board _board1 = new();
        private Board _board2 = new();

        private GamePhase _phase       = GamePhase.WaitingForPlacement;
        private int       _currentTurn = 1;

        private int _placedByPlayer1 = 0;
        private int _placedByPlayer2 = 0;
        private readonly int _totalShips = Board.ShipTypes.Length;

        // Rematch
        private bool _p1WantsRematch = false;
        private bool _p2WantsRematch = false;

        // Timer per tură
        private CancellationTokenSource? _timerCts;
        private static readonly Random _rng = new();
        private const int TurnTimeoutSeconds = 30;

        private readonly object _lock = new();

        public GameSession(ClientHandler handler1, string name1, ClientHandler handler2, string name2)
        {
            _player1 = handler1;
            _name1   = name1;
            _player2 = handler2;
            _name2   = name2;
        }

        // ──────────────────────────────────────────────
        // Pornire sesiune
        // ──────────────────────────────────────────────

        public void Start()
        {
            _player1.OnMessageReceived = HandleMessage;
            _player2.OnMessageReceived = HandleMessage;
            _player1.OnDisconnected    = HandleDisconnect;
            _player2.OnDisconnected    = HandleDisconnect;

            SendGameInit();
            Console.WriteLine($"[Session] Sesiune pornită: {_name1} vs {_name2}");
        }

        private void SendGameInit()
        {
            _player1.Send(Protocol.Build(Protocol.PLAYER_ID, 1));
            _player2.Send(Protocol.Build(Protocol.PLAYER_ID, 2));

            string namesMsg = Protocol.Build(Protocol.PLAYER_NAMES, _name1, _name2);
            _player1.Send(namesMsg);
            _player2.Send(namesMsg);

            _player1.Send(Protocol.START_PLACEMENT);
            _player2.Send(Protocol.START_PLACEMENT);
        }

        // ──────────────────────────────────────────────
        // Router de mesaje
        // ──────────────────────────────────────────────

        private void HandleMessage(int connId, string rawMessage)
        {
            lock (_lock)
            {
                int gamePlayerId = connId == _player1.PlayerId ? 1 : 2;
                var (type, args) = Protocol.Parse(rawMessage);

                switch (type)
                {
                    case Protocol.PLACE:          HandlePlace(gamePlayerId, args);   break;
                    case Protocol.ATTACK:         HandleAttack(gamePlayerId, args);  break;
                    case Protocol.CHAT:           HandleChat(gamePlayerId, args);    break;
                    case Protocol.REMATCH_REQUEST: HandleRematch(gamePlayerId);      break;
                    default:
                        GetHandler(gamePlayerId).Send(Protocol.Build(Protocol.ERROR, $"Mesaj necunoscut: {type}"));
                        break;
                }
            }
        }

        // ──────────────────────────────────────────────
        // Plasare nave
        // ──────────────────────────────────────────────

        private void HandlePlace(int playerId, string[] args)
        {
            if (_phase != GamePhase.WaitingForPlacement)
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.ERROR, "Nu ești în faza de plasare."));
                return;
            }

            if (args.Length < 4 ||
                !int.TryParse(args[0], out int row) ||
                !int.TryParse(args[1], out int col) ||
                !int.TryParse(args[3], out int size))
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.PLACE_ERR, "Format invalid."));
                return;
            }

            string dirStr = args[2].ToUpper();
            if (dirStr != "H" && dirStr != "V")
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.PLACE_ERR, "Direcția trebuie să fie H sau V."));
                return;
            }

            Direction direction = dirStr == "H" ? Direction.Horizontal : Direction.Vertical;
            Board board = playerId == 1 ? _board1 : _board2;

            int alreadyPlaced = board.Ships.Count;
            if (alreadyPlaced >= Board.ShipTypes.Length)
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.PLACE_ERR, "Ai plasat deja toate navele."));
                return;
            }

            var shipType = Board.ShipTypes[alreadyPlaced];

            if (shipType.Size != size)
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.PLACE_ERR,
                    $"Trebuie să plasezi {shipType.Name} (dim. {shipType.Size}), nu {size}."));
                return;
            }

            string? error = board.TryPlaceShip(shipType.Name, size, row, col, direction);
            if (error != null) { GetHandler(playerId).Send(Protocol.Build(Protocol.PLACE_ERR, error)); return; }

            GetHandler(playerId).Send(Protocol.PLACE_OK);

            if (playerId == 1) _placedByPlayer1++;
            else               _placedByPlayer2++;

            Console.WriteLine($"[Session] P{playerId} ({(playerId==1?_name1:_name2)}) a plasat {shipType.Name}.");

            if (_placedByPlayer1 == _totalShips && _placedByPlayer2 == _totalShips)
                StartGame();
        }

        private void StartGame()
        {
            _phase       = GamePhase.Playing;
            _currentTurn = 1;

            Console.WriteLine($"[Session] {_name1} vs {_name2} — jocul a început!");

            _player1.Send(Protocol.GAME_START);
            _player2.Send(Protocol.GAME_START);
            _player1.Send(Protocol.YOUR_TURN);
            _player2.Send(Protocol.WAIT);

            StartTurnTimer(1);
        }

        // ──────────────────────────────────────────────
        // Atacuri
        // ──────────────────────────────────────────────

        private void HandleAttack(int playerId, string[] args)
        {
            if (_phase != GamePhase.Playing)
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.ERROR, "Jocul nu este activ."));
                return;
            }
            if (playerId != _currentTurn)
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.ERROR, "Nu este rândul tău."));
                return;
            }
            if (args.Length < 2 ||
                !int.TryParse(args[0], out int row) ||
                !int.TryParse(args[1], out int col))
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.ERROR, "Format invalid. ATTACK row col"));
                return;
            }

            Board targetBoard = playerId == 1 ? _board2 : _board1;

            if (targetBoard.AlreadyAttacked(row, col))
            {
                GetHandler(playerId).Send(Protocol.Build(Protocol.ERROR, "Ai atacat deja această celulă."));
                return;
            }

            // Anulăm timerul curent — atac valid primit la timp
            _timerCts?.Cancel();

            var (hit, sunkShip) = targetBoard.ReceiveAttack(row, col);

            if (sunkShip != null)
            {
                var parts = new List<object> { sunkShip.Name };
                foreach (var (r, c) in sunkShip.Cells) { parts.Add(r); parts.Add(c); }
                string sunkMsg = Protocol.Build(Protocol.SUNK, parts.ToArray());
                _player1.Send(sunkMsg);
                _player2.Send(sunkMsg);

                Console.WriteLine($"[Session] {(playerId==1?_name1:_name2)} a scufundat {sunkShip.Name}!");

                if (targetBoard.AllShipsSunk) { EndGame(winner: playerId); return; }
            }
            else if (hit)
            {
                string hitMsg = Protocol.Build(Protocol.HIT, row, col);
                _player1.Send(hitMsg);
                _player2.Send(hitMsg);
            }
            else
            {
                string missMsg = Protocol.Build(Protocol.MISS, row, col);
                _player1.Send(missMsg);
                _player2.Send(missMsg);
            }

            // Schimbă tura și pornește noul timer
            _currentTurn = playerId == 1 ? 2 : 1;
            GetHandler(_currentTurn).Send(Protocol.YOUR_TURN);
            GetHandler(playerId).Send(Protocol.WAIT);
            StartTurnTimer(_currentTurn);
        }

        // ──────────────────────────────────────────────
        // Timer per tură
        // ──────────────────────────────────────────────

        private void StartTurnTimer(int gamePlayerId)
        {
            _timerCts?.Cancel();
            _timerCts = new CancellationTokenSource();
            var token       = _timerCts.Token;
            var capturedTurn = gamePlayerId;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TurnTimeoutSeconds * 1000, token);
                    OnTurnTimerExpired(capturedTurn);
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        private void OnTurnTimerExpired(int gamePlayerId)
        {
            lock (_lock)
            {
                if (_phase != GamePhase.Playing || _currentTurn != gamePlayerId) return;

                Board targetBoard = gamePlayerId == 1 ? _board2 : _board1;
                var unattacked    = new List<(int r, int c)>();
                for (int r = 0; r < Board.Size; r++)
                    for (int c = 0; c < Board.Size; c++)
                        if (!targetBoard.AlreadyAttacked(r, c))
                            unattacked.Add((r, c));

                if (unattacked.Count == 0) return;

                var (row, col)  = unattacked[_rng.Next(unattacked.Count)];
                string expName  = gamePlayerId == 1 ? _name1 : _name2;

                Console.WriteLine($"[Session] Tura lui {expName} a expirat! Auto-atac ({row},{col}).");

                string timeoutMsg = Protocol.Build(Protocol.TURN_TIMEOUT, expName);
                _player1.Send(timeoutMsg);
                _player2.Send(timeoutMsg);

                // HandleAttack va anula timerCts (deja nul) și va porni un timer nou
                HandleAttack(gamePlayerId, new[] { row.ToString(), col.ToString() });
            }
        }

        // ──────────────────────────────────────────────
        // Chat
        // ──────────────────────────────────────────────

        private void HandleChat(int playerId, string[] args)
        {
            if (args.Length == 0) return;
            string senderName = playerId == 1 ? _name1 : _name2;
            string chatMsg    = Protocol.Build(Protocol.CHAT, $"[{senderName}]: {args[0]}");
            _player1.Send(chatMsg);
            _player2.Send(chatMsg);
        }

        // ──────────────────────────────────────────────
        // Rematch
        // ──────────────────────────────────────────────

        private void HandleRematch(int playerId)
        {
            if (_phase != GamePhase.GameOver) return;

            if (playerId == 1) _p1WantsRematch = true;
            else               _p2WantsRematch = true;

            Console.WriteLine($"[Session] {(playerId==1?_name1:_name2)} vrea rematch.");

            if (_p1WantsRematch && _p2WantsRematch)
            {
                ResetForRematch();
            }
            else
            {
                // Notificăm celălalt jucător
                int otherId = playerId == 1 ? 2 : 1;
                GetHandler(otherId).Send(Protocol.REMATCH_REQUESTED);
            }
        }

        private void ResetForRematch()
        {
            Console.WriteLine($"[Session] Rematch: {_name1} vs {_name2}!");

            _board1.Reset();
            _board2.Reset();
            _placedByPlayer1  = 0;
            _placedByPlayer2  = 0;
            _p1WantsRematch   = false;
            _p2WantsRematch   = false;
            _phase            = GamePhase.WaitingForPlacement;

            _player1.Send(Protocol.REMATCH_ACCEPTED);
            _player2.Send(Protocol.REMATCH_ACCEPTED);

            SendGameInit();
        }

        // ──────────────────────────────────────────────
        // Sfârșitul jocului
        // ──────────────────────────────────────────────

        private void EndGame(int winner)
        {
            _phase = GamePhase.GameOver;
            _timerCts?.Cancel();

            int loser = winner == 1 ? 2 : 1;
            Console.WriteLine($"[Session] {(winner==1?_name1:_name2)} a câștigat!");

            GetHandler(winner).Send(Protocol.WIN);
            GetHandler(loser).Send(Protocol.LOSE);
        }

        // ──────────────────────────────────────────────
        // Deconectare
        // ──────────────────────────────────────────────

        private void HandleDisconnect(int connId)
        {
            lock (_lock)
            {
                if (_phase == GamePhase.GameOver) return;

                int gamePlayerId = connId == _player1.PlayerId ? 1 : 2;
                Console.WriteLine($"[Session] {(gamePlayerId==1?_name1:_name2)} s-a deconectat.");

                _phase = GamePhase.GameOver;
                _timerCts?.Cancel();

                int otherId = gamePlayerId == 1 ? 2 : 1;
                GetHandler(otherId).Send(Protocol.OPPONENT_DISCONNECTED);
                GetHandler(otherId).Disconnect();
            }
        }

        // ──────────────────────────────────────────────
        // Helper
        // ──────────────────────────────────────────────

        private ClientHandler GetHandler(int gamePlayerId) =>
            gamePlayerId == 1 ? _player1 : _player2;
    }
}
