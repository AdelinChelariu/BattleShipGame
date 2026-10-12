using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BattleshipClient
{
    public partial class ChatWindow : Window
    {
        private readonly Client _client;

        // Culorile mesajelor
        private static readonly SolidColorBrush BrushMe       = new(Color.FromRgb(78, 158, 218));
        private static readonly SolidColorBrush BrushOpponent = new(Color.FromRgb(233, 69, 96));
        private static readonly SolidColorBrush BrushSystem   = new(Color.FromRgb(136, 136, 136));

        public ChatWindow(Client client)
        {
            InitializeComponent();
            _client = client;

            _client.OnChatReceived += OnChatReceived;
        }

        // ──────────────────────────────────────────────
        // Primire mesaje
        // ──────────────────────────────────────────────

        private void OnChatReceived(string message)
        {
            Dispatcher.Invoke(() =>
            {
                // Mesajul vine în format "[P1]: text" sau "[P2]: text"
                bool isMe = message.StartsWith($"[P{_client.PlayerId}]:");
                AddMessage(message, isMe ? BrushMe : BrushOpponent);
            });
        }

        // ──────────────────────────────────────────────
        // Adăugare mesaj în UI
        // ──────────────────────────────────────────────

        public void AddSystemMessage(string text)
        {
            Dispatcher.Invoke(() => AddMessage($"⚙ {text}", BrushSystem));
        }

        private void AddMessage(string text, SolidColorBrush color)
        {
            var tb = new TextBlock
            {
                Text            = text,
                Foreground      = color,
                FontSize        = 13,
                TextWrapping    = TextWrapping.Wrap,
                Margin          = new Thickness(0, 2, 0, 2)
            };

            MessagesPanel.Children.Add(tb);

            // Scroll automat la ultimul mesaj
            ScrollMessages.ScrollToBottom();
        }

        // ──────────────────────────────────────────────
        // Trimitere mesaje
        // ──────────────────────────────────────────────

        private void BtnSend_Click(object sender, RoutedEventArgs e) => SendMessage();

        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SendMessage();
        }

        private void SendMessage()
        {
            string text = TxtMessage.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _client.SendChat(text);
            TxtMessage.Clear();
            TxtMessage.Focus();
        }

        // ──────────────────────────────────────────────
        // La închidere nu distrugem fereastra, doar o ascundem
        // ──────────────────────────────────────────────

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
