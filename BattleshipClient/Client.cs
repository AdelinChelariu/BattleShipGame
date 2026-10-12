using System.IO;
using System.Net.Sockets;
using System.Text;
using BattleshipGame.Shared;

namespace BattleshipClient
{
    /// <summary>
    /// Gestionează conexiunea TCP cu serverul.
    /// Comunică cu UI-ul exclusiv prin evenimente.
    /// </summary>
    public class Client
    {
        // ──────────────────────────────────────────────
        // Evenimente lobby
        // ──────────────────────────────────────────────

        public event Action?                  OnLobbyWelcome;
        public event Action<string[]>?        OnLobbyUpdate;       // lista de nume din lobby
        public event Action<string>?          OnChallengedBy;      // nume provocator
        public event Action<string>?          OnChallengeDeclined; // numele care a respins
        public event Action?                  OnGameReady;
        public event Action<string>?          OnLobbyChatReceived;
        public event Action<string, string>?  OnPlayerNames;    // (myName, opponentName)
        public event Action<string>?          OnTurnTimeout;    // (playerName care a expirat)
        public event Action?                  OnRematchRequested;
        public event Action?                  OnRematchAccepted;

        // ──────────────────────────────────────────────
        // Evenimente joc
        // ──────────────────────────────────────────────

        public event Action<int>?                         OnPlayerIdReceived;
        public event Action?                              OnStartPlacement;
        public event Action?                              OnPlaceOk;
        public event Action<string>?                      OnPlaceError;
        public event Action?                              OnGameStart;
        public event Action?                              OnYourTurn;
        public event Action?                              OnWait;
        public event Action<int, int, bool>?              OnHit;   // (row, col, byMe)
        public event Action<int, int, bool>?              OnMiss;  // (row, col, byMe)
        public event Action<string, List<(int, int)>, bool>? OnSunk; // (shipName, cells, byMe)
        public event Action?                              OnWin;
        public event Action?                              OnLose;
        public event Action<string>?                      OnChatReceived;
        public event Action?                              OnOpponentDisconnected;
        public event Action<string>?                      OnError;
        public event Action?                              OnConnected;
        public event Action<string>?                      OnConnectionFailed;
        public event Action?                              OnDisconnected;

        // ──────────────────────────────────────────────
        // Câmpuri
        // ──────────────────────────────────────────────

        private TcpClient?    _tcpClient;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private readonly object _writeLock = new();

        private bool _connected = false;
        private bool _isMyTurn  = false;

        public int    PlayerId     { get; private set; }
        public string MyName       { get; private set; } = "";
        public string OpponentName { get; private set; } = "";
        public bool   IsMyTurn     => _isMyTurn;
        public bool   IsConnected  => _connected;

        // ──────────────────────────────────────────────
        // Conectare
        // ──────────────────────────────────────────────

        public async Task ConnectAsync(string host, int port)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(host, port);

                var stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                _connected = true;

                OnConnected?.Invoke();
                _ = Task.Run(ListenLoop);
            }
            catch (Exception ex)
            {
                OnConnectionFailed?.Invoke(ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        // Trimitere mesaje lobby
        // ──────────────────────────────────────────────

        public void SendJoin(string name)
        {
            MyName = name;
            Send(Protocol.Build(Protocol.JOIN, name));
        }

        public void SendChallenge(string targetName)
            => Send(Protocol.Build(Protocol.CHALLENGE, targetName));

        public void SendChallengeAccept()
            => Send(Protocol.CHALLENGE_ACCEPT);

        public void SendChallengeDecline()
            => Send(Protocol.CHALLENGE_DECLINE);

        public void SendLobbyChat(string message)
            => Send(Protocol.Build(Protocol.LOBBY_CHAT, message));

        public void SendRematchRequest()
            => Send(Protocol.REMATCH_REQUEST);

        // ──────────────────────────────────────────────
        // Trimitere mesaje joc
        // ──────────────────────────────────────────────

        public void SendPlace(int row, int col, string direction, int size)
            => Send(Protocol.Build(Protocol.PLACE, row, col, direction, size));

        public void SendAttack(int row, int col)
            => Send(Protocol.Build(Protocol.ATTACK, row, col));

        public void SendChat(string message)
            => Send(Protocol.Build(Protocol.CHAT, message));

        private void Send(string message)
        {
            if (!_connected || _writer == null) return;
            try
            {
                lock (_writeLock)
                    _writer.WriteLine(message);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Eroare la trimitere: {ex.Message}");
                Disconnect();
            }
        }

        // ──────────────────────────────────────────────
        // Citire mesaje de la server
        // ──────────────────────────────────────────────

        private async Task ListenLoop()
        {
            try
            {
                string? line;
                while (_connected && _reader != null &&
                       (line = await _reader.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        HandleServerMessage(line);
                }
            }
            catch (Exception) when (!_connected) { }
            catch (Exception ex)
            {
                OnError?.Invoke($"Conexiune pierdută: {ex.Message}");
            }
            finally
            {
                _connected = false;
                OnDisconnected?.Invoke();
            }
        }

        private void HandleServerMessage(string message)
        {
            var (type, args) = Protocol.Parse(message);

            switch (type)
            {
                // ── Lobby ──────────────────────────────
                case Protocol.LOBBY_WELCOME:
                    OnLobbyWelcome?.Invoke();
                    break;

                case Protocol.LOBBY_UPDATE:
                    OnLobbyUpdate?.Invoke(args);
                    break;

                case Protocol.CHALLENGED_BY:
                    OnChallengedBy?.Invoke(args.Length > 0 ? args[0] : "");
                    break;

                case Protocol.CHALLENGE_DECLINED:
                    OnChallengeDeclined?.Invoke(args.Length > 0 ? args[0] : "");
                    break;

                case Protocol.GAME_READY:
                    OnGameReady?.Invoke();
                    break;

                case Protocol.LOBBY_CHAT:
                    OnLobbyChatReceived?.Invoke(args.Length > 0 ? args[0] : "");
                    break;

                // ── Joc ───────────────────────────────
                case Protocol.PLAYER_ID:
                    if (int.TryParse(args[0], out int id))
                    {
                        PlayerId = id;
                        OnPlayerIdReceived?.Invoke(id);
                    }
                    break;

                case Protocol.PLAYER_NAMES:
                    if (args.Length >= 2)
                    {
                        string p1Name = args[0];
                        string p2Name = args[1];
                        MyName       = PlayerId == 1 ? p1Name : p2Name;
                        OpponentName = PlayerId == 1 ? p2Name : p1Name;
                        OnPlayerNames?.Invoke(MyName, OpponentName);
                    }
                    break;

                case Protocol.START_PLACEMENT:
                    OnStartPlacement?.Invoke();
                    break;

                case Protocol.PLACE_OK:
                    OnPlaceOk?.Invoke();
                    break;

                case Protocol.PLACE_ERR:
                    OnPlaceError?.Invoke(string.Join(" ", args));
                    break;

                case Protocol.GAME_START:
                    OnGameStart?.Invoke();
                    break;

                case Protocol.YOUR_TURN:
                    _isMyTurn = true;
                    OnYourTurn?.Invoke();
                    break;

                case Protocol.WAIT:
                    _isMyTurn = false;
                    OnWait?.Invoke();
                    break;

                case Protocol.HIT:
                    if (args.Length >= 2 &&
                        int.TryParse(args[0], out int hr) &&
                        int.TryParse(args[1], out int hc))
                        OnHit?.Invoke(hr, hc, _isMyTurn);
                    break;

                case Protocol.MISS:
                    if (args.Length >= 2 &&
                        int.TryParse(args[0], out int mr) &&
                        int.TryParse(args[1], out int mc))
                        OnMiss?.Invoke(mr, mc, _isMyTurn);
                    break;

                case Protocol.SUNK:
                    // Format: SUNK shipName r1 c1 r2 c2 ...
                    if (args.Length >= 1)
                    {
                        string shipName = args[0];
                        var cells = new List<(int, int)>();
                        for (int i = 1; i + 1 < args.Length; i += 2)
                        {
                            if (int.TryParse(args[i], out int r) && int.TryParse(args[i + 1], out int c))
                                cells.Add((r, c));
                        }
                        OnSunk?.Invoke(shipName, cells, _isMyTurn);
                    }
                    break;

                case Protocol.TURN_TIMEOUT:
                    OnTurnTimeout?.Invoke(args.Length > 0 ? args[0] : "");
                    break;

                case Protocol.REMATCH_REQUESTED:
                    OnRematchRequested?.Invoke();
                    break;

                case Protocol.REMATCH_ACCEPTED:
                    OnRematchAccepted?.Invoke();
                    break;

                case Protocol.WIN:
                    _isMyTurn = false;
                    OnWin?.Invoke();
                    break;

                case Protocol.LOSE:
                    _isMyTurn = false;
                    OnLose?.Invoke();
                    break;

                case Protocol.CHAT:
                    OnChatReceived?.Invoke(args.Length > 0 ? args[0] : "");
                    break;

                case Protocol.OPPONENT_DISCONNECTED:
                    OnOpponentDisconnected?.Invoke();
                    break;

                case Protocol.ERROR:
                    OnError?.Invoke(string.Join(" ", args));
                    break;
            }
        }

        // ──────────────────────────────────────────────
        // Deconectare
        // ──────────────────────────────────────────────

        public void Disconnect()
        {
            _connected = false;
            try
            {
                _reader?.Close();
                _writer?.Close();
                _tcpClient?.Close();
            }
            catch { }
        }
    }
}
