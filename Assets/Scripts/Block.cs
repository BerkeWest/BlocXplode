using UnityEngine;

namespace BlocXplode
{
    public enum BlockKind : byte { Normal, Box }                
    public enum BlockState : byte { Idle, Falling }             
    public enum BlockAnim : byte { None, Pop, Punch }         

    public sealed class Block
    {
        public readonly GameObject gameObject;                  // the Unity object that is shown on screen
        public readonly Transform transform;                    // cached so we never call gameObject.transform (slower)
        public readonly SpriteRenderer spriteRenderer;          // cached renderer that displays the sprite

        public BlockKind kind;                                  // normal block or box
        public BlockState state;                                // idle or falling
        public BlockAnim anim;                                  // current small animation

        public int color;                                       // 0..5 colour index, -1 for boxes
        public int health;                                      // boxes only: 2 = full, 1 = low
        public int row, col;                                    // the LOGICAL cell: where the block is / is going to

        public float x, y;                                      // the VISUAL position in grid units
        public float velocity;                                  // current fall speed in cells per second
        public float targetY;                                   // the row height the block is falling towards
        public float fromX, fromY;                              // where a shuffle movement starts
        public float animTime;                                  // seconds the current animation has been running

        int shownColor = -1;                                    // colour of the sprite currently assigned (-1 = none)
        int shownTier = -1;                                     // icon tier of the sprite currently assigned

        // Created once by the pool; wraps an already created GameObject and SpriteRenderer.
        public Block(GameObject go, SpriteRenderer sr)
        {
            gameObject = go;                                    // remember the object
            transform = go.transform;                           // cache its transform
            spriteRenderer = sr;                                // cache its renderer
        }

        // Prepares a (recycled) block to be a normal coloured block at a cell.
        public void InitNormal(int color, int row, int col, float startY, SpriteLibrary sprites)
        {
            kind = BlockKind.Normal;                            // it is a coloured block
            this.color = color;                                 // remember its colour
            health = 0;                                         // only boxes have health
            this.row = row;                                     // its logical cell
            this.col = col;
            x = col;                                            // the visual x is the column
            y = startY;                                         // the visual y may start above the board (it falls in)
            targetY = row;                                      // it will fall down to its row
            ResetCommon();                                      // reset state, animation, scale ...
            SetTier(0, sprites);                                // start with the default icon
        }

        // Prepares a (recycled) block to be a box obstacle with the given health (1 or 2).
        public void InitBox(int row, int col, int hp, SpriteLibrary sprites)
        {
            kind = BlockKind.Box;
            color = -1;                                         // boxes have no colour
            health = hp;                                        // 2 = full health, 1 = low health
            this.row = row;
            this.col = col;
            x = col;
            y = row;                                            // boxes never fall, so they start on their cell
            targetY = row;
            ResetCommon();
            RefreshBoxSprite(sprites);                          // pick intact or damaged sprite from the health
        }

        // Resets everything a recycled block might still remember from its previous life.
        void ResetCommon()
        {
            state = BlockState.Idle;
            anim = BlockAnim.None;
            velocity = 0f;
            animTime = 0f;
            shownColor = -1;                                    // forces the next SetTier to assign a sprite
            shownTier = -1;
            transform.localScale = Vector3.one;                 // an old pop animation may have shrunk it
        }

        // Shows the icon for a group-size tier. It only touches the renderer when the picture really changes.
        public void SetTier(int tier, SpriteLibrary sprites)
        {
            if (kind != BlockKind.Normal) return;               // boxes do not use tiers
            if (tier == shownTier && color == shownColor) return;   // already showing this sprite: do nothing
            shownTier = tier;                                   // remember what we show now
            shownColor = color;
            spriteRenderer.sprite = sprites.Get(color, tier);   // assign the new sprite
        }

        // Shows the full-health or damaged box sprite depending on the box health.
        public void RefreshBoxSprite(SpriteLibrary sprites)
        {
            spriteRenderer.sprite = health >= 2 ? sprites.BoxIntact : sprites.BoxDamaged;
        }

        // Moves the Unity object to match x / y. 'origin' is the world position of the bottom-left cell.
        public void ApplyPosition(Vector2 origin)
        {
            transform.localPosition = new Vector3(origin.x + x, origin.y + y, 0f);   // grid units -> world position
        }
    }
}
