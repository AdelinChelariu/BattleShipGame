using System.Windows;

namespace BattleshipClient
{
    public partial class ConnectDialog : Window
    {
        public ConnectDialog()
        {
            InitializeComponent();
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            string host = TxtHost.Text.Trim();

            if (string.IsNullOrEmpty(name) || name.Contains(' '))
            {
                TxtStatus.Text = "Numele nu poate fi gol sau să conțină spații.";
                return;
            }

            if (!int.TryParse(TxtPort.Text.Trim(), out int port))
            {
                TxtStatus.Text = "Port invalid.";
                return;
            }

            BtnConnect.IsEnabled = false;
            TxtStatus.Text = "Conectare...";

            var client = new Client();
            bool connected = false;

            client.OnConnected += () => connected = true;
            client.OnConnectionFailed += (err) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = $"Eroare: {err}";
                    BtnConnect.IsEnabled = true;
                });
            };

            await client.ConnectAsync(host, port);

            if (connected)
            {
                var lobbyWindow = new LobbyWindow(client, name);
                lobbyWindow.Show();
                Close();
            }
        }
    }
}
