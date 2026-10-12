using System.Net;
using System.Net.Sockets;

namespace BattleshipServer
{
    /// <summary>
    /// Ascultă conexiuni TCP și trimite fiecare client în LobbyManager.
    /// Clienții rămân în lobby până găsesc un adversar, apoi trec în GameSession.
    /// </summary>
    public class Server
    {
        private readonly int _port;
        private TcpListener? _listener;
        private bool _running;
        private int _nextConnectionId = 0;

        public Server(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _running = true;

            var lobby = new LobbyManager();

            Console.WriteLine($"[Server] Ascult pe portul {_port}...");
            Console.WriteLine("[Server] Lobby activ — aștept jucători...\n");

            try
            {
                while (_running)
                {
                    TcpClient tcpClient = _listener.AcceptTcpClient();
                    int connId = Interlocked.Increment(ref _nextConnectionId);
                    Console.WriteLine($"[Server] Client #{connId} conectat: {tcpClient.Client.RemoteEndPoint}");

                    var handler = new ClientHandler(tcpClient, playerId: connId);

                    // Setăm callback-urile lobby ÎNAINTE de a porni thread-ul de citire
                    lobby.AddClient(handler);

                    var listenerThread = new Thread(handler.StartListening)
                    {
                        IsBackground = true,
                        Name         = $"Client-{connId}-Listener"
                    };
                    listenerThread.Start();
                }
            }
            catch (SocketException) when (!_running) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Eroare: {ex.Message}");
            }
            finally
            {
                _listener?.Stop();
                Console.WriteLine("[Server] Oprit.");
            }
        }

        public void Stop()
        {
            _running = false;
            _listener?.Stop();
        }
    }
}
