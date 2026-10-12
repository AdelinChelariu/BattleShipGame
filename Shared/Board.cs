namespace BattleshipGame.Shared
{
    /// <summary>
    /// Starea vizuală a unei celule (folosită pentru afișare)
    /// </summary>
    public enum CellState
    {
        Empty,      // Necunoscută / nelovită
        Ship,       // Ocupată de o navă (vizibil doar pe propria grilă)
        Hit,        // Lovit cu succes (navă)
        Miss,       // Lovit și ratat (apă)
        Sunk        // Parte dintr-o navă scufundată
    }

    /// <summary>
    /// Grila de joc 10x10.
    /// Fiecare jucător are propria grilă (cu nave) și o grilă "inamică" (ce știe despre adversar).
    /// </summary>
    public class Board
    {
        // ──────────────────────────────────────────────
        // Constante
        // ──────────────────────────────────────────────

        public const int Size = 10;

        /// <summary>
        /// Navele standard din Battleship clasic:
        /// Carrier (5), Battleship (4), Destroyer (3), Submarine (3), Patrol Boat (2)
        /// </summary>
        public static readonly (string Name, int Size)[] ShipTypes = new[]
        {
            ("Portavion",   5),
            ("Cuirasat",    4),
            ("Distrugator", 3),
            ("Submarin",    3),
            ("Barca",       2)
        };

        // ──────────────────────────────────────────────
        // Câmpuri și proprietăți
        // ──────────────────────────────────────────────

        private CellState[,] _grid = new CellState[Size, Size];
        public List<Ship> Ships { get; } = new();

        public bool AllShipsSunk => Ships.Count > 0 && Ships.All(s => s.IsSunk);
        public int ShipsPlaced => Ships.Count;
        public bool IsFullyPlaced => Ships.Count == ShipTypes.Length;

        // ──────────────────────────────────────────────
        // Plasare nave
        // ──────────────────────────────────────────────

        /// <summary>
        /// Încearcă să plaseze o navă. Returnează null dacă reușește, sau un mesaj de eroare.
        /// </summary>
        public string? TryPlaceShip(string shipName, int shipSize, int startRow, int startCol, Direction direction)
        {
            // Verifică dacă mai putem plasa această navă
            int alreadyPlaced = Ships.Count(s => s.Name == shipName);
            int expectedCount = ShipTypes.Count(t => t.Name == shipName);
            if (alreadyPlaced >= expectedCount)
                return $"Nava '{shipName}' a fost deja plasata.";

            // Calculează celulele
            var cells = new List<(int Row, int Col)>();
            for (int i = 0; i < shipSize; i++)
            {
                int r = direction == Direction.Horizontal ? startRow : startRow + i;
                int c = direction == Direction.Horizontal ? startCol + i : startCol;
                cells.Add((r, c));
            }

            // Verifică limite grilă
            foreach (var (r, c) in cells)
            {
                if (r < 0 || r >= Size || c < 0 || c >= Size)
                    return "Nava iese din limitele grilei.";
            }

            // Verifică suprapuneri
            foreach (var (r, c) in cells)
            {
                if (_grid[r, c] == CellState.Ship)
                    return "Nava se suprapune cu alta navă existentă.";
            }

            // Plasează
            var ship = new Ship(shipName, shipSize);
            ship.Place(startRow, startCol, direction);
            Ships.Add(ship);

            foreach (var (r, c) in cells)
                _grid[r, c] = CellState.Ship;

            return null; // succes
        }

        // ──────────────────────────────────────────────
        // Atacuri
        // ──────────────────────────────────────────────

        /// <summary>
        /// Primește un atac la (row, col).
        /// Returnează (hit, sunkShip) unde sunkShip e nava scufundată dacă e cazul.
        /// </summary>
        public (bool Hit, Ship? SunkShip) ReceiveAttack(int row, int col)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size)
                return (false, null);

            // Verifică dacă a fost deja atacată
            if (_grid[row, col] == CellState.Hit || _grid[row, col] == CellState.Miss)
                return (false, null);

            // Caută nava lovită
            foreach (var ship in Ships)
            {
                if (ship.Hit(row, col))
                {
                    // Actualizează grila
                    if (ship.IsSunk)
                    {
                        // Marchează toate celulele navei ca scufundate
                        foreach (var (r, c) in ship.Cells)
                            _grid[r, c] = CellState.Sunk;
                        return (true, ship);
                    }
                    else
                    {
                        _grid[row, col] = CellState.Hit;
                        return (true, null);
                    }
                }
            }

            // Ratare
            _grid[row, col] = CellState.Miss;
            return (false, null);
        }

        /// <summary>
        /// Verifică dacă coordonatele au mai fost atacate anterior
        /// </summary>
        public bool AlreadyAttacked(int row, int col)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size) return true;
            var state = _grid[row, col];
            return state == CellState.Hit || state == CellState.Miss || state == CellState.Sunk;
        }

        // ──────────────────────────────────────────────
        // Acces la grilă
        // ──────────────────────────────────────────────

        public CellState GetCell(int row, int col) => _grid[row, col];

        /// <summary>Resetează complet grila și navele — folosit la rematch</summary>
        public void Reset()
        {
            _grid = new CellState[Size, Size];
            Ships.Clear();
        }

        /// <summary>
        /// Returnează o copie a grilei — folosit de client pentru afișare
        /// </summary>
        public CellState[,] GetGridCopy()
        {
            var copy = new CellState[Size, Size];
            Array.Copy(_grid, copy, _grid.Length);
            return copy;
        }

        /// <summary>Actualizează grila inamicului cu rezultatul unui atac (folosit pe client)</summary>
        public void MarkAttack(int row, int col, bool hit, bool sunk = false)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size) return;
            _grid[row, col] = sunk ? CellState.Sunk : (hit ? CellState.Hit : CellState.Miss);
        }

        /// <summary>Marchează o navă scufundată pe grila inamicului (client)</summary>
        public void MarkSunk(List<(int Row, int Col)> cells)
        {
            foreach (var (r, c) in cells)
                _grid[r, c] = CellState.Sunk;
        }
    }
}
