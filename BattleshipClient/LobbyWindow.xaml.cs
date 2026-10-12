using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BattleshipClient
{
    public partial class LobbyWindow : Window
    {
        private static readonly SolidColorBrush BrushOnline   = new(Color.FromRgb(46,  204, 113));
        private static readonly SolidColorBrush BrushText     = new(Color.FromRgb(234, 234, 234));
        private static readonly SolidColorBrush BrushDim      = new(Color.FromRgb(136, 136, 136));
        private static readonly SolidColorBrush BrushMyMsg    = new(Color.FromRgb(74,  158, 218));
        private static readonly SolidColorBrush BrushSysMsg   = new(Color.FromRgb(100, 120, 150));

        private readonly Client _client;
        private readonly string _myName;
        private bool _transitioning = false;

        public LobbyWindow(Client client, string playerName)
        {
            InitializeComponent();
            _client  = client;
            _myName  = playerName;

            TxtWelcome.Text = $"Bine ai venit, {_myName}!";

            SubscribeToEvents();
            _client.SendJoin(_myName);
        }

        // ──────────────────────────────────────────────
        // Abonare la evenimente
        // ──────────────────────────────────────────────

        private void SubscribeToEvents()
        {
            _client.OnLobbyUpdate       += names => Dispatch(() => RefreshPlayerList(names));
            _client.OnLobbyChatReceived += msg   => Dispatch(() => AppendChatMessage(msg));
            _client.OnChallengedBy      += name  => Dispatch(() => ShowIncomingChallenge(name));
            _client.OnChallengeDeclined += name  => Dispatch(() =>
            {
                SetStatus($"'{name}' a respins provocarea ta.", isError: true);
                AppendSystemMessage($"'{name}' a respins provocarea ta.");
            });
            _client.OnGameReady   += () => Dispatch(TransitionToGame);
            _client.OnError       += err => Dispatch(() => SetStatus(err, isError: true));
            _client.OnDisconnected += () => Dispatch(() =>
            {
                if (!_transitioning)
                    SetStatus("Deconectat de la server.", isError: true);
            });
        }

        // ──────────────────────────────────────────────
        // Lista jucătorilor
        // ──────────────────────────────────────────────

        private void RefreshPlayerList(string[] names)
        {
            PlayersList.Items.Clear();

            var others = names.Where(n => n != _myName).ToArray();
            TxtPlayersHeader.Text = $"Jucători online ({others.Length}):";

            if (others.Length == 0)
            {
                PlayersList.Items.Add(new TextBlock
                {
                    Text       = "Niciun alt jucător.\nAșteaptă...",
                    Foreground = BrushDim,
                    FontSize   = 12,
                    Margin     = new Thickness(0, 6, 0, 0)
                });
                return;
            }

            foreach (string name in others)
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var dot = new Ellipse
                {
                    Width = 7, Height = 7,
                    Fill  = BrushOnline,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dot, 0);

                var nameBlock = new TextBlock
                {
                    Text              = name,
                    Foreground        = BrushText,
                    FontSize          = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(6, 0, 4, 0),
                    TextTrimming      = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(nameBlock, 1);

                string targetName = name;
                var btn = new Button
                {
                    Content  = "Provoacă",
                    FontSize = 11,
                    Padding  = new Thickness(6, 3, 6, 3)
                };
                btn.Style   = (Style)FindResource("SecondaryButton");
                btn.Click  += (s, e) => OnChallengeClick(targetName);
                Grid.SetColumn(btn, 2);

                row.Children.Add(dot);
                row.Children.Add(nameBlock);
                row.Children.Add(btn);
                PlayersList.Items.Add(row);
            }
        }

        // ──────────────────────────────────────────────
        // Chat
        // ──────────────────────────────────────────────

        private void AppendChatMessage(string rawMessage)
        {
            // Detectăm dacă e mesajul nostru pentru a-l colora diferit
            bool isOwn = rawMessage.StartsWith($"[{_myName}]:");

            var tb = new TextBlock
            {
                Text         = rawMessage,
                Foreground   = isOwn ? BrushMyMsg : BrushText,
                FontSize     = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 2, 0, 2)
            };

            ChatMessages.Children.Add(tb);
            ChatScroll.ScrollToBottom();
        }

        private void AppendSystemMessage(string text)
        {
            ChatMessages.Children.Add(new TextBlock
            {
                Text         = $"— {text}",
                Foreground   = BrushSysMsg,
                FontSize     = 11,
                FontStyle    = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 2, 0, 2)
            });
            ChatScroll.ScrollToBottom();
        }

        private void BtnSendChat_Click(object sender, RoutedEventArgs e) => TrySendChat();

        private void TxtChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TrySendChat();
                e.Handled = true;
            }
        }

        private void TrySendChat()
        {
            string text = TxtChatInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _client.SendLobbyChat(text);
            TxtChatInput.Clear();
        }

        // ──────────────────────────────────────────────
        // Provocări
        // ──────────────────────────────────────────────

        private void OnChallengeClick(string targetName)
        {
            _client.SendChallenge(targetName);
            SetStatus($"Provocare trimisă lui '{targetName}'...");
            AppendSystemMessage($"Ai provocat pe '{targetName}'.");
        }

        private void ShowIncomingChallenge(string challengerName)
        {
            TxtChallengedBy.Text      = $"'{challengerName}' te-a provocat!";
            ChallengePanel.Visibility = Visibility.Visible;
            AppendSystemMessage($"'{challengerName}' te-a provocat! Acceptă sau respinge.");
        }

        private void BtnAccept_Click(object sender, RoutedEventArgs e)
        {
            ChallengePanel.Visibility = Visibility.Collapsed;
            SetStatus("Acceptat! Jocul pornește...");
            _client.SendChallengeAccept();
        }

        private void BtnDecline_Click(object sender, RoutedEventArgs e)
        {
            ChallengePanel.Visibility = Visibility.Collapsed;
            SetStatus("Provocare respinsă.");
            _client.SendChallengeDecline();
        }

        // ──────────────────────────────────────────────
        // Tranziție la joc
        // ──────────────────────────────────────────────

        private void TransitionToGame()
        {
            _transitioning = true;
            var mainWindow = new MainWindow(_client);
            mainWindow.Show();
            Close();
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        private void SetStatus(string text, bool isError = false)
        {
            TxtStatus.Text       = text;
            TxtStatus.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(233, 69, 96))
                : BrushDim;
        }

        private void Dispatch(Action action) => Dispatcher.Invoke(action);

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_transitioning)
                _client.Disconnect();
        }
    }
}
