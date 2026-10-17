using System;                                  
using System.Collections.Generic;              
using UnityEngine;

namespace BlocXplode
{
    public sealed class BoardController : MonoBehaviour    
    {
        enum ShuffleStage : byte { None, Waiting, Moving }  

        public event Action<int> Blasted;              
        public event Action ShuffleStarted;              
        public event Action NoMovesLeft;                 

        public int[] ObstaclesDestroyedByLastBlast => obstaclesDestroyed;

        // Set to true when the level is over
        public bool InputLocked { get; set; }

        // True when the player can tap
        public bool IsInteractive => stage == ShuffleStage.None && !gameOver && !InputLocked;

        SpriteLibrary sprites;                  // all sprites
        GameSettings settings;                  // tunable numbers (gravity, durations, icon thresholds)
        UITheme theme;                          // colours of the board frame
        BlockPool pool;                         // recycles block objects
        LevelData level;                        // the level being played
        System.Random rng;                      // seeded random generator => same level for every player
        int rows, cols;                         // board size
        Vector2 origin;                         // world position of the BOTTOM-LEFT cell's centre
        Block[] cells;                          // the grid (null = empty cell)
        int[] normalCells;                      // scratch list: indices of cells that hold coloured blocks
        int[] boxStamp;                         // scratch: remembers which boxes were already hit by the current blast
        bool[] colDirty;                        // scratch: which columns need gravity / refill after a blast
        GroupFinder finder;                     // flood fill
        ShuffleSolver solver;                   // deadlock solver

        // Blocks that are playing a pop / punch animation. Preallocated list, reused forever.
        readonly List<Block> animating = new List<Block>(64);
        readonly int[] obstaclesDestroyed = new int[(int)ObstacleType.Count];

        int fallingCount;                       // how many blocks are currently falling (0 = board is at rest)
        int stampCounter;                       // increases every blast; boxStamp compares against it
        int shuffleCount;                       // how many blocks take part in the running shuffle
        float stageTimer;                       // time spent in the current shuffle stage
        ShuffleStage stage;                     // current shuffle stage
        bool groupsDirty;                       // groups must be recomputed (something moved or was removed)
        bool deadlockCheckPending;              // after the board settles we must check for a deadlock
        bool gameOver;                          // a deadlock that cannot be solved was found

        SpriteRenderer frameOuter, frameInner;  // the golden frame and the dark board background
        SpriteMask mask;                        // clips blocks that spawn above the board


        // Called once by GameBootstrap. Stores the shared data and builds the objects that never change.
        public void Init(SpriteLibrary blockSprites, GameSettings gameSettings, UITheme uiTheme)
        {
            sprites = blockSprites;
            settings = gameSettings;
            theme = uiTheme;
            pool = new BlockPool(transform, 128);   // 128 blocks are enough for the biggest 10x10 board plus spawn queue

            // Two stretched rounded rectangles form the board frame (sorting order negative = drawn behind the blocks).
            frameOuter = CreateBackdrop("FrameOuter", -20, theme.boardFrame);
            frameInner = CreateBackdrop("FrameInner", -10, theme.boardInner);

            var maskGo = new GameObject("BoardMask");           // object that holds the mask
            maskGo.transform.SetParent(transform, false);
            mask = maskGo.AddComponent<SpriteMask>();           // a SpriteMask only lets masked renderers show inside its shape
            mask.sprite = sprites.Pixel;                        // a white square, scaled to the board size in Build()
        }

        // Creates one backdrop object that draws the 9-slice panel sprite stretched to any size without distortion.
        SpriteRenderer CreateBackdrop(string name, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites.Panel;                          // the rounded panel
            sr.drawMode = SpriteDrawMode.Sliced;                // keep the rounded corners, stretch only the middle
            sr.color = color;                                   // tint it
            sr.sortingOrder = order;                            // draw order (lower = further back)
            return sr;
        }

        // Starts a level: clears the old board and fills a new one from the LevelData.
        public void Build(LevelData levelData)
        {
            ReleaseAll();                                       // give the old blocks back to the pool

            level = levelData;
            rows = level.rows;
            cols = level.columns;
            int n = rows * cols;                                // number of cells

            // The same seed always gives the same start board, refills and shuffles => the same level for everyone.
            rng = new System.Random(level.seed);

            // Allocate the arrays only when the board size changed since the last level.
            if (cells == null || finder.Rows != rows || finder.Cols != cols)
            {
                cells = new Block[n];
                normalCells = new int[n];
                boxStamp = new int[n];
                colDirty = new bool[cols];
                finder = new GroupFinder(rows, cols);
                solver = new ShuffleSolver(rows, cols);
            }
            Array.Clear(boxStamp, 0, n);                        // forget hit marks of the previous level
            stampCounter = 0;

            // World position of the bottom-left cell so that the whole board is centred on (0, 0).
            origin = new Vector2(-(cols - 1) * 0.5f, -(rows - 1) * 0.5f);

            // Size the frame, background and mask to the board. 'size' is used because the sprites are 9-sliced.
            frameOuter.size = new Vector2(cols + 0.6f, rows + 0.6f);
            frameInner.size = new Vector2(cols + 0.2f, rows + 0.2f);
            mask.transform.localScale = new Vector3(cols, rows, 1f);

            stage = ShuffleStage.None;                          // no shuffle running
            gameOver = false;
            fallingCount = 0;

            // 1) place the box obstacles from the level layout. colDirty temporarily means "this column has a box".
            Array.Clear(colDirty, 0, cols);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int hp = level.GetBoxHealth(r, c);          // 'B' = 2, 'b' = 1, '.' = 0
                    if (hp <= 0) continue;                      // no box here
                    Block box = pool.Get();                     // take a block from the pool
                    box.InitBox(r, c, hp, sprites);             // make it a box with that health
                    box.ApplyPosition(origin);                  // move the object to its cell
                    cells[r * cols + c] = box;                  // register it in the grid
                    colDirty[c] = true;                         // remember this column contains a box
                }
            }

            // 2) fill every other cell with a coloured block. Fixed order + seeded rng = identical board for everyone.
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    if (cells[idx] != null) continue;           // a box is already here

                    // Columns without a box drop in from above; columns with a box just appear (blocks would otherwise
                    // visually pass through the box).
                    bool dropIn = !colDirty[c];
                    Block b = pool.Get();
                    b.InitNormal(rng.Next(level.colors), r, c, dropIn ? r + rows : r, sprites);   // random colour, start height
                    if (dropIn) { b.state = BlockState.Falling; fallingCount++; }                 // it must fall into place
                    b.ApplyPosition(origin);
                    cells[idx] = b;
                }
            }
            Array.Clear(colDirty, 0, cols);                     // colDirty goes back to its normal meaning

            groupsDirty = true;                                 // groups and icons must be computed
            deadlockCheckPending = true;                        // check for a deadlock once everything settled
        }

        // Returns every block (including popping ones) to the pool.
        void ReleaseAll()
        {
            for (int i = 0; i < animating.Count; i++)
            {
                Block b = animating[i];
                if (b.anim == BlockAnim.Pop) pool.Release(b);   // popping blocks are no longer in the grid, release them here
                else b.anim = BlockAnim.None;                   // punching blocks are still in the grid, released below
            }
            animating.Clear();

            if (cells == null) return;                          // first level: nothing to release
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                pool.Release(cells[i]);
                cells[i] = null;
            }
        }
        
        // Called by GameBootstrap with the tapped WORLD position. Returns true if a group was blasted.
        public bool TryTap(Vector2 world)
        {
            if (!IsInteractive) return false;                   // shuffling, game over or locked

            // world position -> grid cell (each cell is 1 unit wide, centred on its integer position)
            int c = Mathf.FloorToInt(world.x - origin.x + 0.5f);
            int r = Mathf.FloorToInt(world.y - origin.y + 0.5f);
            if (c < 0 || c >= cols || r < 0 || r >= rows) return false;   // tapped outside the board

            int idx = r * cols + c;
            Block b = cells[idx];
            if (b == null || b.state != BlockState.Idle) return false;    // empty or still falling: not tappable

            if (b.kind == BlockKind.Box) { StartPunch(b); return false; } // boxes cannot be tapped, just bump them

            if (groupsDirty) RecomputeGroups();                 // make sure the group data is up to date
            int g = finder.groupOf[idx];                        // the group of the tapped block
            if (g < 0 || finder.sizes[g] < 2) { StartPunch(b); return false; }   // a single block is not blastable

            Blast(g);                                           // remove the whole group
            return true;
        }
        
        // Removes group 'group', damages touching boxes, applies gravity and refills.
        void Blast(int group)
        {
            int size = finder.sizes[group];
            int[] groupOf = finder.groupOf;
            stampCounter++;                                     // new blast => new stamp value
            Array.Clear(obstaclesDestroyed, 0, obstaclesDestroyed.Length);   // nothing destroyed yet by this blast

            for (int i = 0; i < cells.Length; i++)              // visit every cell
            {
                if (groupOf[i] != group) continue;              // not part of the blasted group

                Block b = cells[i];
                cells[i] = null;                                // the cell is free immediately (logic first)
                StartPop(b);                                    // the block plays its shrink animation, then returns to the pool

                int r = i / cols;
                int c = i - r * cols;
                colDirty[c] = true;                             // this column must fall and refill

                // Every box touching a blasted block takes 1 damage. boxStamp makes sure a box next to SEVERAL blasted
                // blocks is only damaged once per blast.
                if (r > 0) DamageBox(i - cols);                 // below
                if (r < rows - 1) DamageBox(i + cols);          // above
                if (c > 0) DamageBox(i - 1);                    // left
                if (c < cols - 1) DamageBox(i + 1);             // right
            }

            for (int c = 0; c < cols; c++)                      // apply gravity + refill to every changed column
            {
                if (!colDirty[c]) continue;
                colDirty[c] = false;
                SettleColumn(c);
            }

            groupsDirty = true;                                 // groups changed
            deadlockCheckPending = true;                        // maybe no moves are left now
            Blasted?.Invoke(size);                              // tell listeners (the ?. means "only if someone listens")
        }

        // Gives one damage to the box at 'idx' (if there is a box that was not yet hit by this blast).
        void DamageBox(int idx)
        {
            Block box = cells[idx];
            if (box == null || box.kind != BlockKind.Box || boxStamp[idx] == stampCounter) return;
            boxStamp[idx] = stampCounter;                       // mark it as hit by this blast

            box.health--;                                       // lose one health point
            if (box.health <= 0)                                // destroyed
            {
                cells[idx] = null;                              // free the cell
                obstaclesDestroyed[(int)ObstacleType.Box]++;    // the goal panel counts it
                colDirty[idx % cols] = true;                    // the column can now fall / refill through this cell
                StartPop(box);                                  // play the disappear animation
            }
            else                                                // damaged but alive
            {
                box.RefreshBoxSprite(sprites);                  // switch to the damaged picture
                StartPunch(box);                                // small bump as feedback
            }
        }

        // Gravity + refill for one column. Boxes are fixed: they split the column into segments and blocks never cross
        // them. Only the TOP segment (above the highest box) gets new blocks, created above the board.
        void SettleColumn(int c)
        {
            int write = 0;                                      // the lowest free row of the current segment
            for (int r = 0; r < rows; r++)                      // walk the column bottom -> top
            {
                int idx = r * cols + c;
                Block b = cells[idx];
                if (b == null) continue;                        // empty cell

                if (b.kind == BlockKind.Box) { write = r + 1; continue; }   // a box: the next segment starts above it

                if (r != write)                                 // there is a gap below: the block must drop
                {
                    cells[write * cols + c] = b;                // move it down in the grid ...
                    cells[idx] = null;
                    b.row = write;                              // ... and update its logical row
                    StartFall(b, write);                        // ... and let it fall visually
                }
                write++;                                        // the next free slot is one higher
            }

            if (write >= rows) return;                          // column is full: nothing to refill

            // New blocks queue up above the highest existing block so they enter the board one after another.
            float baseY = rows;                                 // just above the board
            for (int r = rows - 1; r >= 0; r--)                 // find the highest block of the column
            {
                Block top = cells[r * cols + c];
                if (top == null) continue;
                baseY = Mathf.Max(baseY, top.y + 1f);           // start above it (it may itself still be falling in)
                break;
            }

            int k = 0;                                          // how many new blocks were queued so far
            for (int r = write; r < rows; r++, k++)             // create one block per empty top cell
            {
                Block nb = pool.Get();
                nb.InitNormal(rng.Next(level.colors), r, c, baseY + k, sprites);   // seeded random colour, stacked start height
                nb.ApplyPosition(origin);
                cells[r * cols + c] = nb;
                StartFall(nb, r);                               // it falls down to its row
            }
        }

        // Makes a block fall towards 'targetRow' (or snaps it there if it is already at/below it).
        void StartFall(Block b, int targetRow)
        {
            b.targetY = targetRow;
            if (b.y > targetRow + 0.0001f)                      // it is above its target: it has to fall
            {
                if (b.state != BlockState.Falling)              // not already falling: start with zero speed
                {
                    b.state = BlockState.Falling;
                    b.velocity = 0f;
                    fallingCount++;
                }
                // already falling: keep the current speed, only the target changed
            }
            else                                                // already at its place
            {
                if (b.state == BlockState.Falling) { b.state = BlockState.Idle; fallingCount--; }
                b.velocity = 0f;
                b.y = targetRow;
                b.ApplyPosition(origin);
            }
        }

        // Recomputes the groups and switches every block to the icon that matches its group size.
        void RecomputeGroups()
        {
            finder.Find(cells);                                 // flood fill the whole board
            for (int i = 0; i < cells.Length; i++)
            {
                Block b = cells[i];
                if (b == null || b.kind != BlockKind.Normal) continue;   // only coloured blocks have group icons
                int tier = 0;                                   // default icon
                if (b.state == BlockState.Idle)                 // falling blocks are in no group and keep the default icon
                {
                    int g = finder.groupOf[i];
                    if (g >= 0) tier = settings.IconTier(finder.sizes[g]);   // icon from the group's size (A/B/C thresholds)
                }
                b.SetTier(tier, sprites);                       // only changes the sprite if it differs
            }
            groupsDirty = false;
        }
        
        void Update()
        {
            if (cells == null) return;                          // no level built yet
            float dt = Mathf.Min(Time.deltaTime, 0.05f);        // cap the step so a lag spike cannot teleport blocks

            if (fallingCount > 0) UpdateFalling(dt);            // move falling blocks (skipped when everything rests)
            if (animating.Count > 0) UpdateAnimations(dt);      // play pop / punch animations
            if (stage != ShuffleStage.None) UpdateShuffle(dt);  // run the shuffle if one is active

            if (groupsDirty) RecomputeGroups();                 // refresh icons after anything changed

            // Deadlocks are only checked when everything has settled (nothing falling, no shuffle, not locked).
            if (deadlockCheckPending && fallingCount == 0 && stage == ShuffleStage.None && !gameOver && !InputLocked)
            {
                deadlockCheckPending = false;
                if (finder.largest < 2) BeginShuffle();         // the biggest group has 1 block: nothing can be blasted
            }
        }

        // Moves every falling block down by gravity.
        void UpdateFalling(float dt)
        {
            // Rows are processed bottom-up, so the block below has already moved this frame.
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Block b = cells[r * cols + c];
                    if (b == null || b.state != BlockState.Falling) continue;

                    float v = Mathf.Min(b.velocity + settings.gravity * dt, settings.maxFallSpeed);   // accelerate, up to the limit
                    float y = b.y - v * dt;                     // new height

                    if (r > 0)                                  // never overlap the block below
                    {
                        Block below = cells[(r - 1) * cols + c];
                        if (below != null)
                        {
                            float floor = below.y + 1f;         // the lowest height we may have (one cell above the block below)
                            if (y < floor)
                            {
                                y = Mathf.Min(floor, b.y);      // stop on top of it
                                v = Mathf.Min(v, below.velocity);   // and do not move faster than it
                            }
                        }
                    }

                    if (y <= b.targetY)                         // reached its cell: land
                    {
                        y = b.targetY;
                        v = 0f;
                        b.state = BlockState.Idle;
                        fallingCount--;
                        groupsDirty = true;                     // it now counts for groups
                    }

                    b.velocity = v;
                    b.y = y;
                    b.ApplyPosition(origin);                    // move the Unity object
                }
            }
        }

        // Starts the shrink-and-disappear animation of a removed block.
        void StartPop(Block b)
        {
            if (b.anim == BlockAnim.None) animating.Add(b);     // not yet in the list (a punching block already is)
            b.anim = BlockAnim.Pop;
            b.animTime = 0f;
        }

        // Starts a small bump (hit box, or a tap on a block that cannot be blasted).
        void StartPunch(Block b)
        {
            if (b.anim != BlockAnim.None) return;               // already animating: ignore
            b.anim = BlockAnim.Punch;
            b.animTime = 0f;
            animating.Add(b);
        }

        // Advances all pop / punch animations; finished pops go back to the pool.
        void UpdateAnimations(float dt)
        {
            for (int i = animating.Count - 1; i >= 0; i--)      // backwards, because we may remove entries
            {
                Block b = animating[i];
                b.animTime += dt;

                if (b.anim == BlockAnim.Pop)
                {
                    float t = b.animTime / settings.popDuration;    // progress 0..1
                    if (t >= 1f)                                // finished: recycle the block
                    {
                        pool.Release(b);
                        RemoveAnimatingAt(i);
                        continue;
                    }
                    // grow a little during the first quarter, then shrink to zero
                    float s = t < 0.25f ? 1f + 0.2f * (t / 0.25f) : 1.2f * (1f - (t - 0.25f) / 0.75f);
                    b.transform.localScale = new Vector3(s, s, 1f);
                }
                else                                            // Punch
                {
                    float t = b.animTime / settings.punchDuration;
                    if (t >= 1f)                                // finished: restore the normal size
                    {
                        b.transform.localScale = Vector3.one;
                        b.anim = BlockAnim.None;
                        RemoveAnimatingAt(i);
                        continue;
                    }
                    float s = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);   // grow and shrink back along a sine curve
                    b.transform.localScale = new Vector3(s, s, 1f);
                }
            }
        }

        // Removes list entry i quickly by overwriting it with the last entry (order does not matter).
        void RemoveAnimatingAt(int i)
        {
            int last = animating.Count - 1;
            animating[i] = animating[last];
            animating.RemoveAt(last);
        }
        
        // The board has no blastable group: announce it and wait a moment before shuffling.
        void BeginShuffle()
        {
            stage = ShuffleStage.Waiting;
            stageTimer = 0f;
            ShuffleStarted?.Invoke();                           // the UI shows "shuffling!"
        }

        void UpdateShuffle(float dt)
        {
            stageTimer += dt;

            if (stage == ShuffleStage.Waiting)                  // short pause so the player can read the message
            {
                if (stageTimer >= settings.shuffleWait) RunSolver();
                return;
            }

            // Moving stage: ease every block from its old position to its new cell.
            float t = Mathf.Clamp01(stageTimer / settings.shuffleDuration);   // progress 0..1
            float e = t * t * (3f - 2f * t);                    // "smoothstep": slow start, fast middle, slow end
            for (int k = 0; k < shuffleCount; k++)
            {
                Block b = cells[normalCells[k]];
                b.x = Mathf.Lerp(b.fromX, b.col, e);            // blend between start and target
                b.y = Mathf.Lerp(b.fromY, b.row, e);
                b.ApplyPosition(origin);
            }

            if (t >= 1f)                                        // arrived
            {
                stage = ShuffleStage.None;
                groupsDirty = true;                             // new neighbours => new groups
            }
        }

        // Runs the ShuffleSolver on the current board and prepares the movement animation.
        void RunSolver()
        {
            int n = 0;                                          // how many coloured blocks there are
            for (int i = 0; i < cells.Length; i++)
            {
                Block b = cells[i];
                if (b != null && b.kind == BlockKind.Normal) normalCells[n++] = i;   // collect their cell indices
            }

            var result = solver.Solve(cells, normalCells, n, level.colors, rng);
            if (result == ShuffleSolver.Result.Unsolvable)      // boxes cut the board apart: nothing can be done
            {
                stage = ShuffleStage.None;
                gameOver = true;
                NoMovesLeft?.Invoke();
                return;
            }

            shuffleCount = n;
            for (int k = 0; k < n; k++)                         // the solver swapped references; update every block
            {
                int idx = normalCells[k];
                Block b = cells[idx];
                b.row = idx / cols;                             // its new logical cell
                b.col = idx - b.row * cols;
                b.fromX = b.x;                                  // start the glide from where it is drawn now
                b.fromY = b.y;
            }

            stage = ShuffleStage.Moving;
            stageTimer = 0f;
        }
    }
}
