namespace BattleshipGame.Shared
{
    /// <summary>
    /// Direcția de plasare a navei pe grilă
    /// </summary>
    public enum Direction
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Reprezintă o navă în jocul Battleship.
    /// O navă ocupă mai multe celule și se scufundă când toate celulele sunt lovite.
    /// </summary>
    public class Ship
    {
        // ──────────────────────────────────────────────
        // Proprietăți
        // ──────────────────────────────────────────────

        public string Name { get; }
        public int Size { get; }

        /// <summary>Lista de celule (row, col) ocupate de navă</summary>
        public List<(int Row, int Col)> Cells { get; private set; }

        /// <summary>Setul de celule lovite</summary>
        private HashSet<(int Row, int Col)> _hitCells = new();

        public bool IsPlaced => Cells != null && Cells.Count > 0;
        public bool IsSunk => IsPlaced && _hitCells.Count == Size;
        public int HitCount => _hitCells.Count;

        // ──────────────────────────────────────────────
        // Constructor
        // ──────────────────────────────────────────────

        public Ship(string name, int size)
        {
            Name = name;
            Size = size;
            Cells = new List<(int, int)>();
        }

        // ──────────────────────────────────────────────
        // Metode publice
        // ──────────────────────────────────────────────

        /// <summary>
        /// Plasează nava pe grilă.
        /// Calculează celulele ocupate pornind de la (startRow, startCol) în direcția dată.
        /// </summary>
        public void Place(int startRow, int startCol, Direction direction)
        {
            Cells = new List<(int, int)>();
            for (int i = 0; i < Size; i++)
            {
                if (direction == Direction.Horizontal)
                    Cells.Add((startRow, startCol + i));
                else
                    Cells.Add((startRow + i, startCol));
            }
        }

        /// <summary>
        /// Încearcă să lovească nava la coordonatele date.
        /// Returnează true dacă lovitura a nimerit.
        /// </summary>
        public bool Hit(int row, int col)
        {
            var cell = (row, col);
            if (Cells.Contains(cell))
            {
                _hitCells.Add(cell);
                return true;
            }
            return false;
        }

        /// <summary>Verifică dacă nava ocupă celula dată</summary>
        public bool OccupiesCell(int row, int col) => Cells.Contains((row, col));

        /// <summary>Verifică dacă o celulă a navei a fost deja lovită</summary>
        public bool IsCellHit(int row, int col) => _hitCells.Contains((row, col));

        public override string ToString() => $"{Name} (size {Size}, sunk: {IsSunk})";
    }
}
