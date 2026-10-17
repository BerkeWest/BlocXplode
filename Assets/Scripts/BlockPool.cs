using System.Collections.Generic;       
using UnityEngine;
using UnityEngine.Rendering;           

namespace BlocXplode
{
    public sealed class BlockPool
    {
        readonly Stack<Block> free;     // the switched-off blocks waiting to be reused
        readonly Transform parent;      // all blocks are children of the board object to keep the hierarchy tidy

        // 'prewarm' blocks are created immediately so the first level does not have to create any.
        public BlockPool(Transform parent, int prewarm)
        {
            this.parent = parent;
            free = new Stack<Block>(prewarm * 2);               // reserve space so the stack never has to grow
            for (int i = 0; i < prewarm; i++) Release(Create()); // create blocks and put them straight into the pool
        }

        // Gives out a block: a recycled one if available, a new one otherwise.
        public Block Get()
        {
            Block b = free.Count > 0 ? free.Pop() : Create();   // reuse if possible
            b.gameObject.SetActive(true);                       // switch the object on so it is drawn
            return b;
        }

        // Takes a block back: switch it off and store it for later.
        public void Release(Block b)
        {
            b.state = BlockState.Idle;                          // forget falling
            b.anim = BlockAnim.None;                            // forget animations
            b.transform.localScale = Vector3.one;               // undo a pop animation's shrinking
            b.gameObject.SetActive(false);                      // hide it
            free.Push(b);                                       // keep it for reuse
        }

        // Builds a brand new block object with a configured SpriteRenderer.
        Block Create()
        {
            var go = new GameObject("Block");                   // empty object
            go.transform.SetParent(parent, false);              // put it under the board (false = keep local position)
            var sr = go.AddComponent<SpriteRenderer>();         // component that draws a sprite
            sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // clipped by the board mask (hides blocks above the board)
            sr.shadowCastingMode = ShadowCastingMode.Off;       // 2D sprites need no shadows ...
            sr.receiveShadows = false;
            sr.lightProbeUsage = LightProbeUsage.Off;           // ... and no light/reflection probe lookups
            sr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return new Block(go, sr);                           // wrap them in our Block class
        }
    }
}
