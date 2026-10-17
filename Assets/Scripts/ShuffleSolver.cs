using System;                                  

namespace BlocXplode
{
    public sealed class ShuffleSolver
    {
        public enum Result { Shuffled, Repaired, Recolored, Unsolvable }   

        readonly int rows, cols;                
        readonly int[] pairA, pairB;            
        readonly int[] colorCount = new int[SpriteLibrary.MaxColors];   
        int pairCount;                          

        public ShuffleSolver(int rows, int cols)
        {
            this.rows = rows;
            this.cols = cols;
            pairA = new int[rows * cols * 2];   
            pairB = new int[rows * cols * 2];
        }

        static bool IsNormal(Block b) => b != null && b.kind == BlockKind.Normal;
        
        public Result Solve(Block[] cells, int[] cellIdx, int n, int colorKinds, Random rng)
        {
            // Unsolvable if no pair can exist
            CollectPairs(cells, cellIdx, n);                
            if (pairCount == 0) return Result.Unsolvable;    

            // Fisher-Yates - walk backwards and swap every block with a random earlier one
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);                  
                if (i == j) continue;                        
                int ci = cellIdx[i], cj = cellIdx[j];      
                Block t = cells[ci]; cells[ci] = cells[cj]; cells[cj] = t;   
            }
            if (HasMatch(cells)) return Result.Shuffled;    // if there is a match, deadlock solved

            // count how many blocks each colour has
            Array.Clear(colorCount, 0, colorCount.Length);
            for (int k = 0; k < n; k++) colorCount[cells[cellIdx[k]].color]++;

            // pick a random colour that has at least 2 blocks 
            int pick = -1, eligible = 0;
            for (int c = 0; c < colorKinds; c++)
            {
                if (colorCount[c] < 2) continue;             // alone block can't form a pair
                eligible++;
                if (rng.Next(0, eligible) == 0) pick = c;  
            }

            if (pick < 0)                                    // every colour has at most one block
            {
                int p = rng.Next(0, pairCount);              // choose a random touching pair
                cells[pairB[p]].color = cells[pairA[p]].color;   // make the second block the colour of the first
                return Result.Recolored;
            }

            // choose the pair that already has the most blocks of the picked colour (fewest swaps needed)
            int best = -1, bestCost = 3, ties = 0;
            for (int p = 0; p < pairCount; p++)
            {
                int cost = (cells[pairA[p]].color != pick ? 1 : 0) + (cells[pairB[p]].color != pick ? 1 : 0);
                if (cost < bestCost) { bestCost = cost; best = p; ties = 1; }             
                else if (cost == bestCost && rng.Next(0, ++ties) == 0) best = p;          
            }

            int pa = pairA[best], pb = pairB[best];          
            PlaceColor(cells, cellIdx, n, pa, pb, pick);    
            PlaceColor(cells, cellIdx, n, pb, pa, pick);     
            return Result.Repaired;
        }

        // Stores every pair of touching coloured-block cells in pairA / pairB.
        void CollectPairs(Block[] cells, int[] cellIdx, int n)
        {
            pairCount = 0;
            for (int k = 0; k < n; k++)
            {
                int idx = cellIdx[k];
                int r = idx / cols;
                int c = idx - r * cols;
                // pair with the cell to the right, if it exists and holds a coloured block
                if (c < cols - 1 && IsNormal(cells[idx + 1])) { pairA[pairCount] = idx; pairB[pairCount++] = idx + 1; }
                // pair with the cell above, if it exists and holds a coloured block
                if (r < rows - 1 && IsNormal(cells[idx + cols])) { pairA[pairCount] = idx; pairB[pairCount++] = idx + cols; }
            }
        }

        // True if at least one touching pair has the same colour.
        bool HasMatch(Block[] cells)
        {
            for (int p = 0; p < pairCount; p++)
                if (cells[pairA[p]].color == cells[pairB[p]].color) return true;
            return false;
        }

        // Makes cell 'target' hold a block of 'color' by swapping with a block of that colour somewhere else.
        static void PlaceColor(Block[] cells, int[] cellIdx, int n, int target, int other, int color)
        {
            if (cells[target].color == color) return;        // already right
            for (int k = 0; k < n; k++)
            {
                int s = cellIdx[k];
                if (s == target || s == other || cells[s].color != color) continue;   
                Block t = cells[target]; cells[target] = cells[s]; cells[s] = t;      
                return;
            }
        }
    }
}
