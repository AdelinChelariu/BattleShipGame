namespace BattleshipGame.Shared
{
    /// <summary>
    /// Definește toate mesajele folosite în comunicarea client-server.
    /// Format general: "TIP_MESAJ arg1 arg2 ..."
    /// </summary>
    public static class Protocol
    {
        // ──────────────────────────────────────────────
        // Faza de conectare / așteptare
        // ──────────────────────────────────────────────

        /// <summary>Server → Client: "Ești jucătorul 1 sau 2"  (ex: "PLAYER_ID 1")</summary>
        public const string PLAYER_ID = "PLAYER_ID";

        /// <summary>Server → Client: Ambii jucători conectați, începe plasarea navelor</summary>
        public const string START_PLACEMENT = "START_PLACEMENT";

        // ──────────────────────────────────────────────
        // Faza de plasare nave
        // ──────────────────────────────────────────────

        /// <summary>
        /// Client → Server: Plasează o navă
        /// Format: "PLACE row col direction size"
        /// direction: H (orizontal) sau V (vertical)
        /// Exemplu: "PLACE 2 3 H 4"
        /// </summary>
        public const string PLACE = "PLACE";

        /// <summary>Server → Client: Nava a fost plasată cu succes</summary>
        public const string PLACE_OK = "PLACE_OK";

        /// <summary>Server → Client: Eroare la plasare (ex: "PLACE_ERR Nava iese din grila")</summary>
        public const string PLACE_ERR = "PLACE_ERR";

        /// <summary>Server → Client: Ambii jucători au plasat toate navele, jocul începe</summary>
        public const string GAME_START = "GAME_START";

        // ──────────────────────────────────────────────
        // Faza de joc (atacuri)
        // ──────────────────────────────────────────────

        /// <summary>Server → Client: Este rândul tău să ataci</summary>
        public const string YOUR_TURN = "YOUR_TURN";

        /// <summary>Server → Client: Așteaptă, celălalt jucător atacă</summary>
        public const string WAIT = "WAIT";

        /// <summary>
        /// Client → Server: Atac la coordonate
        /// Format: "ATTACK row col"
        /// Exemplu: "ATTACK 4 7"
        /// </summary>
        public const string ATTACK = "ATTACK";

        /// <summary>
        /// Server → ambii clienți: Lovitura a nimerit o navă
        /// Format: "HIT row col"
        /// </summary>
        public const string HIT = "HIT";

        /// <summary>
        /// Server → ambii clienți: Lovitura a ratat
        /// Format: "MISS row col"
        /// </summary>
        public const string MISS = "MISS";

        /// <summary>
        /// Server → ambii clienți: O navă a fost scufundată
        /// Format: "SUNK shipName r1 c1 r2 c2 ..." — include coordonatele tuturor celulelor
        /// Exemplu: "SUNK Distrugator 3 5 4 5 5 5"
        /// </summary>
        public const string SUNK = "SUNK";

        // ──────────────────────────────────────────────
        // Sfârșitul jocului
        // ──────────────────────────────────────────────

        /// <summary>Server → Client: Ai câștigat</summary>
        public const string WIN = "WIN";

        /// <summary>Server → Client: Ai pierdut</summary>
        public const string LOSE = "LOSE";

        // ──────────────────────────────────────────────
        // Chat
        // ──────────────────────────────────────────────

        /// <summary>
        /// Client → Server → Client: Mesaj de chat
        /// Format: "CHAT mesajul aici"
        /// Exemplu: "CHAT Noroc bun!"
        /// </summary>
        public const string CHAT = "CHAT";

        // ──────────────────────────────────────────────
        // Diverse
        // ──────────────────────────────────────────────

        /// <summary>Server → Client: Celălalt jucător s-a deconectat</summary>
        public const string OPPONENT_DISCONNECTED = "OPPONENT_DISCONNECTED";

        // ──────────────────────────────────────────────
        // Timer per tură
        // ──────────────────────────────────────────────

        /// <summary>
        /// Server → ambii clienți: Tura unui jucător a expirat, s-a efectuat un auto-atac
        /// Format: "TURN_TIMEOUT NumeJucator"
        /// </summary>
        public const string TURN_TIMEOUT = "TURN_TIMEOUT";

        // ──────────────────────────────────────────────
        // Rematch
        // ──────────────────────────────────────────────

        /// <summary>Client → Server: Jucătorul vrea rematch</summary>
        public const string REMATCH_REQUEST = "REMATCH_REQUEST";

        /// <summary>Server → Client: Adversarul tău vrea rematch</summary>
        public const string REMATCH_REQUESTED = "REMATCH_REQUESTED";

        /// <summary>Server → ambii clienți: Ambii vor rematch — jocul repornește</summary>
        public const string REMATCH_ACCEPTED = "REMATCH_ACCEPTED";

        // ──────────────────────────────────────────────
        // Lobby
        // ──────────────────────────────────────────────

        /// <summary>Server → Client: Ești în lobby, trimite-ți numele cu JOIN</summary>
        public const string LOBBY_WELCOME = "LOBBY_WELCOME";

        /// <summary>Client → Server: Setează numele de jucător. Format: "JOIN NumePret"</summary>
        public const string JOIN = "JOIN";

        /// <summary>
        /// Server → Client: Lista curentă de jucători din lobby
        /// Format: "LOBBY_UPDATE Alice Bob Carol" (spațiu-delimitați, fără spații în nume)
        /// </summary>
        public const string LOBBY_UPDATE = "LOBBY_UPDATE";

        /// <summary>Client → Server: Provoacă un jucător. Format: "CHALLENGE NumeTinta"</summary>
        public const string CHALLENGE = "CHALLENGE";

        /// <summary>Server → Client: Cineva te-a provocat. Format: "CHALLENGED_BY NumeProvocare"</summary>
        public const string CHALLENGED_BY = "CHALLENGED_BY";

        /// <summary>Client → Server: Acceptă provocarea primită</summary>
        public const string CHALLENGE_ACCEPT = "CHALLENGE_ACCEPT";

        /// <summary>Client → Server: Respinge provocarea primită</summary>
        public const string CHALLENGE_DECLINE = "CHALLENGE_DECLINE";

        /// <summary>Server → Client: Provocarea ta a fost respinsă. Format: "CHALLENGE_DECLINED NumeTinta"</summary>
        public const string CHALLENGE_DECLINED = "CHALLENGE_DECLINED";

        /// <summary>Server → Client: Meci găsit — urmează PLAYER_ID și START_PLACEMENT</summary>
        public const string GAME_READY = "GAME_READY";

        /// <summary>
        /// Server → ambii clienți: Numele celor doi jucători
        /// Format: "PLAYER_NAMES numeJucator1 numeJucator2"
        /// Trimis imediat după PLAYER_ID, înainte de START_PLACEMENT
        /// </summary>
        public const string PLAYER_NAMES = "PLAYER_NAMES";

        /// <summary>
        /// Client → Server → toți clienții din lobby: mesaj de chat
        /// Format trimis de client: "LOBBY_CHAT mesajul aici"
        /// Format broadcast: "LOBBY_CHAT [Nume]: mesajul aici"
        /// </summary>
        public const string LOBBY_CHAT = "LOBBY_CHAT";

        /// <summary>Server → Client: Mesaj de eroare general (ex: "ERROR Nu e randul tau")</summary>
        public const string ERROR = "ERROR";

        // ──────────────────────────────────────────────
        // Metode helper
        // ──────────────────────────────────────────────

        /// <summary>Construiește un mesaj din tip + argumente (separate prin spațiu)</summary>
        public static string Build(string type, params object[] args)
        {
            if (args == null || args.Length == 0)
                return type;
            return type + " " + string.Join(" ", args);
        }

        /// <summary>Parsează un mesaj și returnează tipul și argumentele</summary>
        public static (string type, string[] args) Parse(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return (string.Empty, Array.Empty<string>());

            // Mesajele de chat pot conține spații — le tratăm special
            if (message.StartsWith(CHAT + " "))
            {
                string chatContent = message.Substring(CHAT.Length + 1);
                return (CHAT, new[] { chatContent });
            }

            if (message.StartsWith(LOBBY_CHAT + " "))
            {
                string chatContent = message.Substring(LOBBY_CHAT.Length + 1);
                return (LOBBY_CHAT, new[] { chatContent });
            }

            var parts = message.Trim().Split(' ');
            string type = parts[0];
            string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            return (type, args);
        }
    }
}
