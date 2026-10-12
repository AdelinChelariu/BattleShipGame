using System.Net.Sockets;
using System.Text;
using BattleshipGame.Shared;

namespace BattleshipServer
{
    /// <summary>
    /// Gestionează comunicarea cu un singur client TCP.
    /// Citește mesaje incoming pe un thread dedicat și le pasează la GameSession.
    /// Oferă metodă thread-safe pentru trimiterea mesajelor.
    /// </summary>
    public class ClientHandler
    {
        // ──────────────────────────────────────────────
        // Câmpuri
        // ──────────────────────────────────────────────

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly object _writeLock = new();

        public int PlayerId { get; }
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Callback apelat de thread-ul de citire când sosește un mesaj nou.
        /// Setat de GameSession.
        /// </summary>
        public Action<int, string>? OnMessageReceived { get; set; }

        /// <summary>
        /// Callback apelat când clientul se deconectează.
        /// </summary>
        public Action<int>? OnDisconnected { get; set; }

        // ──────────────────────────────────────────────
        // Constructor
        // ──────────────────────────────────────────────

        public ClientHandler(TcpClient client, int playerId)
        {
            _client = client;
            PlayerId = playerId;
            IsConnected = true;

            _stream = client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
        }

        // ──────────────────────────────────────────────
        // Trimitere mesaje
        // ──────────────────────────────────────────────

        /// <summary>
        /// Trimite un mesaj către client (thread-safe).
        /// </summary>
        public void Send(string message)
        {
            if (!IsConnected) return;
            try
            {
                lock (_writeLock)
                {
                    _writer.WriteLine(message);
                }
                Console.WriteLine($"[→ P{PlayerId}] {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientHandler P{PlayerId}] Eroare la trimitere: {ex.Message}");
                Disconnect();
            }
        }

        // ──────────────────────────────────────────────
        // Citire mesaje (rulează pe thread separat)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Pornește bucla de citire. Apelat pe un thread dedicat din GameSession.
        /// </summary>
        public void StartListening()
        {
            try
            {
                string? line;
                while (IsConnected && (line = _reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    Console.WriteLine($"[← P{PlayerId}] {line}");
                    OnMessageReceived?.Invoke(PlayerId, line);
                }
            }
            catch (Exception ex) when (IsConnected)
            {
                Console.WriteLine($"[ClientHandler P{PlayerId}] Conexiune pierdută: {ex.Message}");
            }
            finally
            {
                Disconnect();
                OnDisconnected?.Invoke(PlayerId);
            }
        }

        // ──────────────────────────────────────────────
        // Deconectare
        // ──────────────────────────────────────────────

        public void Disconnect()
        {
            if (!IsConnected) return;
            IsConnected = false;
            try
            {
                _reader.Close();
                _writer.Close();
                _stream.Close();
                _client.Close();
            }
            catch { /* ignorăm erorile la închidere */ }
        }
    }
}
