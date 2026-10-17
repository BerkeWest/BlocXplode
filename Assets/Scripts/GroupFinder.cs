namespace BlocXplode
{
    public sealed class GroupFinder
    {
        public readonly int Rows, Cols;     // board size this finder was built for

        public readonly int[] groupOf;      // for every cell: the id of its group, or -1 if it is in no group
        public readonly int[] sizes;        // for every group id: how many blocks it has
        public int groupCount;              // how many groups were found
        public int largest;                 // size of the biggest group (below 2 = deadlock)

        readonly int[] stack;               // the to-do list of the flood fill (an array used as a stack)

        public GroupFinder(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            int n = rows * cols;            // number of cells
            groupOf = new int[n];
            sizes = new int[n];             // there can never be more groups than cells
            stack = new int[n];             // the stack can never hold more cells than the board has
        }

        // A block can join a group only if it exists, is a coloured block and is not moving.
        static bool Eligible(Block b) => b != null && b.kind == BlockKind.Normal && b.state == BlockState.Idle;

        // Recomputes all groups. 'cells' is the board, row-major (index = row * Cols + column).
        public void Find(Block[] cells)
        {
            int n = cells.Length;
            for (int i = 0; i < n; i++) groupOf[i] = -1;   // forget the previous result
            groupCount = 0;
            largest = 0;

            for (int start = 0; start < n; start++)         // try every cell as the start of a new group
            {
                Block first = cells[start];
                if (groupOf[start] != -1 || !Eligible(first)) continue;   // already in a group, or not eligible: skip

                int color = first.color;                    // the group's colour
                int id = groupCount;                        // the id this group will get
                int sp = 0;                                 // stack pointer (number of cells waiting)
                int size = 0;                               // blocks counted so far
                stack[sp++] = start;                        // push the start cell
                groupOf[start] = id;                        // mark it (so it is never pushed twice)

                while (sp > 0)                              // until the to-do list is empty
                {
                    int cur = stack[--sp];                  // take a cell from the stack
                    size++;                                 // it belongs to the group
                    int r = cur / Cols;                     // its row
                    int c = cur - r * Cols;                 // its column

                    int nb;                                 // index of the neighbour being examined
                    // look at the 4 neighbours (down, up, left, right); each is added if it matches and is unmarked
                    if (r > 0)        { nb = cur - Cols; if (groupOf[nb] == -1 && Match(cells[nb], color)) { groupOf[nb] = id; stack[sp++] = nb; } }
                    if (r < Rows - 1) { nb = cur + Cols; if (groupOf[nb] == -1 && Match(cells[nb], color)) { groupOf[nb] = id; stack[sp++] = nb; } }
                    if (c > 0)        { nb = cur - 1;    if (groupOf[nb] == -1 && Match(cells[nb], color)) { groupOf[nb] = id; stack[sp++] = nb; } }
                    if (c < Cols - 1) { nb = cur + 1;    if (groupOf[nb] == -1 && Match(cells[nb], color)) { groupOf[nb] = id; stack[sp++] = nb; } }
                }

                sizes[id] = size;                           // store the finished group's size
                groupCount++;                               // the next group gets the next id
                if (size > largest) largest = size;         // track the biggest group
            }
        }

        // True if the block can join a group of the given colour.
        static bool Match(Block b, int color) => Eligible(b) && b.color == color;
    }
}
