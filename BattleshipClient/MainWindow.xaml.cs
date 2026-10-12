using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using BattleshipGame.Shared;

namespace BattleshipClient
{
    public partial class MainWindow : Window
    {
        // ──────────────────────────────────────────────
        // Constante vizuale
        // ──────────────────────────────────────────────

        private static readonly SolidColorBrush BrushWater    = new(Color.FromRgb(26,   58,  92));
        private static readonly SolidColorBrush BrushWaterHov = new(Color.FromRgb(36,   78, 122));
        private static readonly SolidColorBrush BrushShip     = new(Color.FromRgb(74,  158, 218));
        private static readonly SolidColorBrush BrushShipPrev = new(Color.FromArgb(120, 74, 158, 218));
        private static readonly SolidColorBrush BrushShipBad  = new(Color.FromArgb(120, 233, 69, 96));
        private static readonly SolidColorBrush BrushHit      = new(Color.FromRgb(233,  69,  96));
        private static readonly SolidColorBrush BrushMiss     = new(Color.FromRgb(85,   85, 119));
        private static readonly SolidColorBrush BrushSunk     = new(Color.FromRgb(139,   0,   0));
        private static readonly SolidColorBrush BrushGrid     = new(Color.FromRgb(30,   50,  80));
        private static readonly SolidColorBrush BrushEnemyHov = new(Color.FromRgb(180,  40,  60));

        private static readonly SolidColorBrush BrushTimerGreen  = new(Color.FromRgb(46,  204, 113));
        private static readonly SolidColorBrush BrushTimerYellow = new(Color.FromRgb(241, 196,  15));
        private static readonly SolidColorBrush BrushTimerRed    = new(Color.FromRgb(233,  69,  96));

        // ──────────────────────────────────────────────
        // Câmpuri
        // ──────────────────────────────────────────────

        private readonly Client    _client;
        private readonly ChatWindow _chatWindow;

        private readonly Rectangle[,] _myCells    = new Rectangle[10, 10];
        private readonly Rectangle[,] _enemyCells = new Rectangle[10, 10];

        private Board _myBoard    = new();
        private Board _enemyBoard = new();

        // Plasare
        private bool _isPlacementPhase = false;
        private bool _isHorizontal     = true;
        private int  _previewRow       = -1;
        private int  _previewCol       = -1;
        private int  _lastPlaceRow     = -1;
        private int  _lastPlaceCol     = -1;
        private Direction _lastPlaceDir = Direction.Horizontal;
        private Queue<(int row, int col, Direction dir)>? _autoPlacements;

        // Statistici
        private int _myShipsRemaining    = 5;
        private int _enemyShipsRemaining = 5;
        private int _myTotalShots        = 0;
        private int _myHits              = 0;
        private int _enemyTotalShots     = 0;
        private int _enemyHits           = 0;

        // Timer
        private DispatcherTimer? _countdownTimer;
        private int  _timerSeconds;
        private bool _isMyTurnTimer; // true = e tura mea, false = e tura adversarului
        private const int TurnTimeoutSeconds = 30;

        // ──────────────────────────────────────────────
        // Constructor
        // ──────────────────────────────────────────────

        public MainWindow(Client client)
        {
            InitializeComponent();
            _client     = client;
            _chatWindow = new ChatWindow(client);

            BuildGrids();
            SubscribeToEvents();

            KeyDown += (s, e) => { if (e.Key == Key.R) RotateDirection(); };
        }

        // ──────────────────────────────────────────────
        // Construire grile
        // ──────────────────────────────────────────────

        private void BuildGrids()
        {
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    var myRect = MakeCell();
                    int r = row, c = col;
                    myRect.MouseLeftButtonDown += (s, e) => OnMyGridClick(r, c);
                    myRect.MouseEnter          += (s, e) => OnMyGridHover(r, c);
                    myRect.MouseLeave          += (s, e) => ClearPreview();
                    _myCells[row, col] = myRect;
                    MyGrid.Children.Add(myRect);

                    var enemyRect = MakeCell();
                    enemyRect.MouseLeftButtonDown += (s, e) => OnEnemyGridClick(r, c);
                    enemyRect.MouseEnter          += (s, e) => OnEnemyGridHover(r, c);
                    enemyRect.MouseLeave          += (s, e) => OnEnemyGridLeave(r, c);
                    _enemyCells[row, col] = enemyRect;
                    EnemyGrid.Children.Add(enemyRect);
                }
            }
        }

        private Rectangle MakeCell() => new Rectangle
        {
            Fill = BrushWater, Stroke = BrushGrid, StrokeThickness = 0.5, Margin = new Thickness(1)
        };

        // ──────────────────────────────────────────────
        // Abonare la evenimente
        // ──────────────────────────────────────────────

        private void SubscribeToEvents()
        {
            _client.OnPlayerNames          += (myName, oppName) => Dispatch(() =>
            {
                Title                  = $"Battleship — {myName}";
                _chatWindow.Title      = $"Chat — {myName}";
                TxtMyGridLabel.Text    = myName.ToUpper();
                TxtEnemyGridLabel.Text = oppName.ToUpper();
            });

            _client.OnStartPlacement       += () => Dispatch(StartPlacementPhase);
            _client.OnPlaceOk              += () => Dispatch(OnPlaceOk);
            _client.OnPlaceError           += err => Dispatch(() => SetStatus($"⚠ {err}", isError: true));
            _client.OnGameStart            += () => Dispatch(OnGameStart);

            _client.OnYourTurn             += () => Dispatch(() =>
            {
                SetStatus("Este rândul tău — atacă grila inamicului!");
                TxtTurn.Text       = "● RÂNDUL TĂU";
                TxtTurn.Foreground = BrushTimerGreen;
                StartCountdown(isMyTurn: true);
            });

            _client.OnWait                 += () => Dispatch(() =>
            {
                SetStatus("Aștept mutarea adversarului...");
                TxtTurn.Text       = "○ Adversarul atacă";
                TxtTurn.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
                StartCountdown(isMyTurn: false);
            });

            _client.OnHit                  += (r, c, byMe) => Dispatch(() => HandleHit(r, c, byMe));
            _client.OnMiss                 += (r, c, byMe) => Dispatch(() => HandleMiss(r, c, byMe));
            _client.OnSunk                 += (name, cells, byMe) => Dispatch(() => HandleSunk(name, cells, byMe));

            _client.OnTurnTimeout          += name => Dispatch(() =>
            {
                StopCountdown();
                string label = name == _client.MyName ? "Tura ta" : $"Tura lui {name}";
                AddLogEntry($"⏰ {label} a expirat — auto-atac!", Color.FromRgb(241, 196, 15));
                SetStatus($"⏰ {label} a expirat — atac automat!");
            });

            _client.OnWin                  += () => Dispatch(() => ShowGameOver(won: true));
            _client.OnLose                 += () => Dispatch(() => ShowGameOver(won: false));

            _client.OnRematchRequested     += () => Dispatch(() =>
            {
                SetStatus("Adversarul vrea să joace din nou!");
                BtnRematch.Content = "🔁 Acceptă rematch";
            });

            _client.OnRematchAccepted      += () => Dispatch(ResetForRematch);

            _client.OnOpponentDisconnected += () => Dispatch(() =>
            {
                StopCountdown();
                MessageBox.Show("Adversarul s-a deconectat.", "Joc terminat",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            });

            _client.OnDisconnected         += () => Dispatch(() =>
                SetStatus("Deconectat de la server.", isError: true));

            _client.OnError                += err => Dispatch(() =>
                SetStatus($"⚠ {err}", isError: true));
        }

        // ──────────────────────────────────────────────
        // Faza de plasare
        // ──────────────────────────────────────────────

        private void StartPlacementPhase()
        {
            _isPlacementPhase         = true;
            PlacementPanel.Visibility = Visibility.Visible;
            BtnRotate.Visibility      = Visibility.Visible;
            RefreshShipList();
            UpdateCurrentShipLabel();
            SetStatus("Plasează-ți navele pe grila ta.");
        }

        private void RefreshShipList()
        {
            ShipList.Items.Clear();
            foreach (var (name, size) in Board.ShipTypes)
            {
                bool placed = _myBoard.Ships.Any(s => s.Name == name);
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                item.Children.Add(new TextBlock
                {
                    Text       = placed ? "✓ " : "○ ",
                    Foreground = placed ? new SolidColorBrush(Color.FromRgb(46, 204, 113))
                                        : new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                    FontSize   = 13, Width = 22
                });

                for (int i = 0; i < size; i++)
                    item.Children.Add(new Rectangle
                    {
                        Width = 14, Height = 14, Fill = placed ? BrushMiss : BrushShip,
                        Margin = new Thickness(1, 0, 1, 0), RadiusX = 2, RadiusY = 2
                    });

                item.Children.Add(new TextBlock
                {
                    Text            = $" {name}",
                    Foreground      = placed ? new SolidColorBrush(Color.FromRgb(100, 100, 100))
                                             : new SolidColorBrush(Color.FromRgb(234, 234, 234)),
                    FontSize        = 12, Margin = new Thickness(6, 0, 0, 0),
                    TextDecorations = placed ? TextDecorations.Strikethrough : null
                });

                ShipList.Items.Add(item);
            }
        }

        private void UpdateCurrentShipLabel()
        {
            int placed = _myBoard.ShipsPlaced;
            if (placed < Board.ShipTypes.Length)
            {
                var next = Board.ShipTypes[placed];
                TxtCurrentShip.Text          = $"{next.Name} ({next.Size} celule)";
                TxtPlacementInstruction.Text = "Click pe grila ta pentru a plasa nava";
            }
            else
            {
                TxtCurrentShip.Text          = "";
                TxtPlacementInstruction.Text = "Toate navele plasate! Aștept adversarul...";
            }
        }

        // ──────────────────────────────────────────────
        // Click / hover grila proprie
        // ──────────────────────────────────────────────

        private void OnMyGridClick(int row, int col)
        {
            if (!_isPlacementPhase || _myBoard.IsFullyPlaced) return;
            SendPlacement(row, col, _isHorizontal ? Direction.Horizontal : Direction.Vertical);
        }

        private void OnMyGridHover(int row, int col)
        {
            if (!_isPlacementPhase || _myBoard.IsFullyPlaced) return;
            _previewRow = row; _previewCol = col;
            DrawPreview(row, col);
        }

        private void ClearPreview()
        {
            _previewRow = -1; _previewCol = -1;
            RefreshMyGrid();
        }

        private void DrawPreview(int startRow, int startCol)
        {
            RefreshMyGrid();
            int placed = _myBoard.ShipsPlaced;
            if (placed >= Board.ShipTypes.Length) return;

            int size = Board.ShipTypes[placed].Size;
            bool valid = true;
            var cells = new List<(int r, int c)>();

            for (int i = 0; i < size; i++)
            {
                int r = _isHorizontal ? startRow : startRow + i;
                int c = _isHorizontal ? startCol + i : startCol;
                cells.Add((r, c));
                if (r >= 10 || c >= 10) valid = false;
                else if (_myBoard.GetCell(r, c) == CellState.Ship) valid = false;
            }

            foreach (var (r, c) in cells)
            {
                if (r >= 10 || c >= 10) continue;
                _myCells[r, c].Fill = valid ? BrushShipPrev : BrushShipBad;
            }
        }

        // ──────────────────────────────────────────────
        // Trimitere plasare (manual + auto)
        // ──────────────────────────────────────────────

        private void SendPlacement(int row, int col, Direction dir)
        {
            _lastPlaceRow = row;
            _lastPlaceCol = col;
            _lastPlaceDir = dir;

            int placed    = _myBoard.ShipsPlaced;
            var shipType  = Board.ShipTypes[placed];
            string dirStr = dir == Direction.Horizontal ? "H" : "V";
            _client.SendPlace(row, col, dirStr, shipType.Size);
        }

        private void OnPlaceOk()
        {
            if (_lastPlaceRow >= 0 && _lastPlaceCol >= 0)
            {
                int placed = _myBoard.ShipsPlaced;
                if (placed < Board.ShipTypes.Length)
                {
                    var st = Board.ShipTypes[placed];
                    _myBoard.TryPlaceShip(st.Name, st.Size, _lastPlaceRow, _lastPlaceCol, _lastPlaceDir);
                }
            }

            RefreshMyGrid();
            RefreshShipList();
            UpdateCurrentShipLabel();
            _chatWindow.AddSystemMessage("Navă plasată!");

            // Auto-plasare: trimite următoarea navă
            if (_autoPlacements != null && _autoPlacements.Count > 0)
            {
                var (r, c, dir) = _autoPlacements.Dequeue();
                SendPlacement(r, c, dir);
                if (_autoPlacements.Count == 0) _autoPlacements = null;
                return;
            }

            if (_myBoard.IsFullyPlaced)
                SetStatus("Toate navele plasate! Aștept adversarul...");
        }

        // ──────────────────────────────────────────────
        // Plasare aleatorie
        // ──────────────────────────────────────────────

        private void BtnRandomPlace_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPlacementPhase || _myBoard.IsFullyPlaced) return;

            var placements = GenerateRemainingPlacements();
            if (placements.Count == 0) return;

            _autoPlacements = new Queue<(int, int, Direction)>(placements);
            var (row, col, dir) = _autoPlacements.Dequeue();
            SendPlacement(row, col, dir);
        }

        private List<(int row, int col, Direction dir)> GenerateRemainingPlacements()
        {
            var result = new List<(int, int, Direction)>();
            var rand   = new Random();

            // Board temporar cu navele deja plasate de jucător
            var tempBoard = new Board();
            foreach (var ship in _myBoard.Ships)
            {
                if (ship.Cells.Count == 0) continue;
                var start = ship.Cells[0];
                var d     = ship.Cells.Count > 1 && ship.Cells[1].Row > ship.Cells[0].Row
                            ? Direction.Vertical : Direction.Horizontal;
                tempBoard.TryPlaceShip(ship.Name, ship.Size, start.Row, start.Col, d);
            }

            // Jumătatea de sus = rândurile 0..MaxRow inclusiv (5 rânduri din 10)
            const int MaxRow = 4;

            for (int i = _myBoard.ShipsPlaced; i < Board.ShipTypes.Length; i++)
            {
                var (name, size) = Board.ShipTypes[i];

                // Lista EXHAUSTIVĂ a pozițiilor care încap geometric în top-half
                // → imposibil să scape vreo navă în jos prin calcul aleatoriu
                var candidates = new List<(int r, int c, Direction d)>();
                for (int r = 0; r <= MaxRow; r++)
                {
                    for (int c = 0; c < Board.Size; c++)
                    {
                        // Orizontal: ultima celulă e (r, c+size-1) — trebuie < 10
                        if (c + size - 1 < Board.Size)
                            candidates.Add((r, c, Direction.Horizontal));

                        // Vertical: ultima celulă e (r+size-1, c) — trebuie <= MaxRow
                        if (r + size - 1 <= MaxRow)
                            candidates.Add((r, c, Direction.Vertical));
                    }
                }

                // Amestecare Fisher-Yates pentru ordine cu adevărat aleatorie
                for (int k = candidates.Count - 1; k > 0; k--)
                {
                    int j = rand.Next(k + 1);
                    (candidates[k], candidates[j]) = (candidates[j], candidates[k]);
                }

                // Prima poziție fără suprapunere cu alte nave
                foreach (var (r, c, d) in candidates)
                {
                    if (tempBoard.TryPlaceShip(name, size, r, c, d) == null)
                    {
                        result.Add((r, c, d));
                        break;
                    }
                }
            }

            return result;
        }

        // ──────────────────────────────────────────────
        // Start joc
        // ──────────────────────────────────────────────

        private void OnGameStart()
        {
            _isPlacementPhase         = false;
            PlacementPanel.Visibility = Visibility.Collapsed;
            BtnRotate.Visibility      = Visibility.Collapsed;
            TimerPanel.Visibility     = Visibility.Visible;
            LogBorder.Visibility      = Visibility.Visible;
            SetStatus("Jocul a început!");
            _chatWindow.AddSystemMessage("Jocul a început! Mult succes!");
            UpdateStats();
        }

        // ──────────────────────────────────────────────
        // Click grila inamicului
        // ──────────────────────────────────────────────

        private void OnEnemyGridClick(int row, int col)
        {
            if (!_client.IsMyTurn) return;
            if (_enemyBoard.AlreadyAttacked(row, col)) return;
            _client.SendAttack(row, col);
        }

        private void OnEnemyGridHover(int row, int col)
        {
            if (!_client.IsMyTurn) return;
            if (_enemyBoard.AlreadyAttacked(row, col)) return;
            _enemyCells[row, col].Fill = BrushEnemyHov;
            Mouse.SetCursor(Cursors.Cross);
        }

        private void OnEnemyGridLeave(int row, int col)
        {
            if (_enemyBoard.AlreadyAttacked(row, col)) return;
            _enemyCells[row, col].Fill = BrushWater;
            Mouse.SetCursor(Cursors.Arrow);
        }

        // ──────────────────────────────────────────────
        // Rezultate atacuri
        // ──────────────────────────────────────────────

        private void HandleHit(int row, int col, bool byMe)
        {
            if (byMe)
            {
                _enemyBoard.MarkAttack(row, col, hit: true);
                _enemyCells[row, col].Fill = BrushHit;
                _myTotalShots++; _myHits++;
                SetStatus($"💥 HIT la ({row},{col})!");
                AddLogEntry($"💥 {_client.MyName} → ({row},{col})", Color.FromRgb(74, 158, 218));
            }
            else
            {
                _myBoard.MarkAttack(row, col, hit: true);
                _myCells[row, col].Fill = BrushHit;
                _enemyTotalShots++; _enemyHits++;
                SetStatus($"💥 Adversarul te-a lovit la ({row},{col})!");
                AddLogEntry($"💥 {_client.OpponentName} → ({row},{col})", Color.FromRgb(233, 69, 96));
            }
            UpdateStats();
        }

        private void HandleMiss(int row, int col, bool byMe)
        {
            if (byMe)
            {
                _enemyBoard.MarkAttack(row, col, hit: false);
                _enemyCells[row, col].Fill = BrushMiss;
                _myTotalShots++;
                SetStatus($"🌊 MISS la ({row},{col}).");
                AddLogEntry($"🌊 {_client.MyName} → ({row},{col})", Color.FromRgb(85, 85, 119));
            }
            else
            {
                _myBoard.MarkAttack(row, col, hit: false);
                _myCells[row, col].Fill = BrushMiss;
                _enemyTotalShots++;
                SetStatus($"🌊 Adversarul a ratat la ({row},{col}).");
                AddLogEntry($"🌊 {_client.OpponentName} → ({row},{col})", Color.FromRgb(85, 85, 119));
            }
            UpdateStats();
        }

        private void HandleSunk(string shipName, List<(int, int)> cells, bool byMe)
        {
            if (byMe)
            {
                foreach (var (r, c) in cells)
                {
                    _enemyBoard.MarkAttack(r, c, hit: true, sunk: true);
                    _enemyCells[r, c].Fill = BrushSunk;
                }
                _enemyShipsRemaining = Math.Max(0, _enemyShipsRemaining - 1);
                SetStatus($"⚓ Ai scufundat {shipName}!");
                AddLogEntry($"⚓ {_client.MyName} scufundă {shipName}!", Color.FromRgb(46, 204, 113));
                _chatWindow.AddSystemMessage($"Ai scufundat {shipName}!");
            }
            else
            {
                foreach (var (r, c) in cells)
                {
                    _myBoard.MarkAttack(r, c, hit: true, sunk: true);
                    _myCells[r, c].Fill = BrushSunk;
                }
                _myShipsRemaining = Math.Max(0, _myShipsRemaining - 1);
                SetStatus($"⚓ Adversarul ți-a scufundat {shipName}!");
                AddLogEntry($"⚓ {_client.OpponentName} scufundă {shipName}!", Color.FromRgb(233, 69, 96));
                _chatWindow.AddSystemMessage($"Adversarul ți-a scufundat {shipName}!");
            }
            UpdateStats();
        }

        // ──────────────────────────────────────────────
        // Statistici
        // ──────────────────────────────────────────────

        private void UpdateStats()
        {
            int myRate    = _myTotalShots    > 0 ? (int)(100.0 * _myHits    / _myTotalShots)    : 0;
            int enemyRate = _enemyTotalShots > 0 ? (int)(100.0 * _enemyHits / _enemyTotalShots) : 0;

            TxtMyStats.Text    = $"🚢 {_myShipsRemaining}/5 nave  ·  💥 Adversar: {enemyRate}%";
            TxtEnemyStats.Text = $"🚢 {_enemyShipsRemaining}/5 nave  ·  💥 Tu: {myRate}%";
        }

        // ──────────────────────────────────────────────
        // Jurnal atacuri
        // ──────────────────────────────────────────────

        private void AddLogEntry(string text, Color color)
        {
            var tb = new TextBlock
            {
                Text         = text,
                Foreground   = new SolidColorBrush(color),
                FontSize     = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 1, 0, 1)
            };
            AttackLog.Children.Add(tb);

            // Păstrăm max 50 de intrări
            while (AttackLog.Children.Count > 50)
                AttackLog.Children.RemoveAt(0);

            LogScroll.ScrollToBottom();
        }

        // ──────────────────────────────────────────────
        // Timer countdown
        // ──────────────────────────────────────────────

        private void StartCountdown(bool isMyTurn)
        {
            _isMyTurnTimer = isMyTurn;
            _timerSeconds  = TurnTimeoutSeconds;
            _countdownTimer?.Stop();

            TxtTimerLabel.Text = isMyTurn ? "RÂNDUL TĂU" : $"TURA {_client.OpponentName.ToUpper()}";
            UpdateTimerDisplay();

            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += (s, e) =>
            {
                _timerSeconds = Math.Max(0, _timerSeconds - 1);
                UpdateTimerDisplay();
                if (_timerSeconds == 0) _countdownTimer.Stop();
            };
            _countdownTimer.Start();
        }

        private void StopCountdown() => _countdownTimer?.Stop();

        private void UpdateTimerDisplay()
        {
            TxtTimer.Text = _timerSeconds.ToString();

            // Culoare în funcție de timp rămas
            var color = _timerSeconds > 10 ? BrushTimerGreen
                      : _timerSeconds > 5  ? BrushTimerYellow
                      : BrushTimerRed;

            TxtTimer.Foreground   = color;
            TimerBarFill.Fill     = color;
            TimerBarFill.Width    = 90.0 * _timerSeconds / TurnTimeoutSeconds;
        }

        // ──────────────────────────────────────────────
        // Rematch
        // ──────────────────────────────────────────────

        private void BtnRematch_Click(object sender, RoutedEventArgs e)
        {
            BtnRematch.IsEnabled = false;
            BtnRematch.Content   = "⏳ Aștept adversarul...";
            _client.SendRematchRequest();
            SetStatus("Cerere de rematch trimisă. Aștept adversarul...");
        }

        private void ResetForRematch()
        {
            StopCountdown();

            // Reset stare
            _myBoard    = new Board();
            _enemyBoard = new Board();

            _myShipsRemaining    = 5;
            _enemyShipsRemaining = 5;
            _myTotalShots        = 0;
            _myHits              = 0;
            _enemyTotalShots     = 0;
            _enemyHits           = 0;
            _autoPlacements      = null;
            _lastPlaceRow        = -1;
            _lastPlaceCol        = -1;
            _isHorizontal        = true;

            // Reset grile vizuale
            for (int r = 0; r < 10; r++)
                for (int c = 0; c < 10; c++)
                {
                    _myCells[r, c].Fill    = BrushWater;
                    _enemyCells[r, c].Fill = BrushWater;
                }

            // Reset UI
            AttackLog.Children.Clear();
            TimerPanel.Visibility = Visibility.Collapsed;
            LogBorder.Visibility  = Visibility.Collapsed;
            BtnRematch.Visibility = Visibility.Collapsed;
            BtnRematch.IsEnabled  = true;
            BtnRematch.Content    = "🔁 Joacă din nou";
            TxtTurn.Text          = "";
            TxtMyStats.Text       = "";
            TxtEnemyStats.Text    = "";
            TxtDirection.Text     = "→ Orizontal";

            SetStatus("Rematch! Plasează-ți navele.");
            _chatWindow.AddSystemMessage("Rematch! Joc nou.");
        }

        // ──────────────────────────────────────────────
        // Sfârșit joc
        // ──────────────────────────────────────────────

        private void ShowGameOver(bool won)
        {
            StopCountdown();
            TimerPanel.Visibility = Visibility.Collapsed;

            TxtTurn.Text       = won ? "🏆 AI CÂȘTIGAT!" : "💀 AI PIERDUT!";
            TxtTurn.Foreground = won ? BrushTimerGreen : BrushTimerRed;

            SetStatus(won ? "Felicitări! Ai câștigat!" : "Ai pierdut. Mai mult noroc data viitoare!");

            BtnRematch.Visibility = Visibility.Visible;
            BtnRematch.IsEnabled  = true;
            BtnRematch.Content    = "🔁 Joacă din nou";

            string msg = won ? "🏆 Felicitări! Ai câștigat!" : "💀 Ai pierdut!";
            MessageBox.Show(msg, "Joc terminat", MessageBoxButton.OK,
                won ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        // ──────────────────────────────────────────────
        // Refresh grile
        // ──────────────────────────────────────────────

        private void RefreshMyGrid()
        {
            for (int r = 0; r < 10; r++)
                for (int c = 0; c < 10; c++)
                    _myCells[r, c].Fill = _myBoard.GetCell(r, c) switch
                    {
                        CellState.Ship => BrushShip,
                        CellState.Hit  => BrushHit,
                        CellState.Miss => BrushMiss,
                        CellState.Sunk => BrushSunk,
                        _              => BrushWater
                    };
        }

        // ──────────────────────────────────────────────
        // Rotire direcție
        // ──────────────────────────────────────────────

        private void BtnRotate_Click(object sender, RoutedEventArgs e) => RotateDirection();

        private void RotateDirection()
        {
            _isHorizontal     = !_isHorizontal;
            TxtDirection.Text = _isHorizontal ? "→ Orizontal" : "↓ Vertical";
            if (_previewRow >= 0 && _previewCol >= 0)
                DrawPreview(_previewRow, _previewCol);
        }

        // ──────────────────────────────────────────────
        // Chat
        // ──────────────────────────────────────────────

        private void BtnChat_Click(object sender, RoutedEventArgs e)
        {
            if (!_chatWindow.IsVisible)
            {
                _chatWindow.Left = Left + Width + 8;
                _chatWindow.Top  = Top;
                _chatWindow.Show();
            }
            else
            {
                _chatWindow.Hide();
            }
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        private void SetStatus(string text, bool isError = false)
        {
            TxtStatus.Text       = text;
            TxtStatus.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(233, 69, 96))
                : new SolidColorBrush(Color.FromRgb(234, 234, 234));
        }

        private void Dispatch(Action action) => Dispatcher.Invoke(action);

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopCountdown();
            _client.Disconnect();
            _chatWindow.Owner = null;
            Application.Current.Shutdown();
        }
    }
}
