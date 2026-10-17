// ============================================================================================================
// SpriteLibrary.cs
// Loads every sprite of the game from ONE Unity Sprite Atlas and stores them in fields so the rest of the game can
// ask "give me the red block with the second icon" without knowing anything about file names.
// All sprites (blocks, boxes, UI panels, icons, the white pixel) share one texture => the GPU can draw most of the
// screen in a single batch.
// ============================================================================================================

using System;                           
using UnityEngine;
using UnityEngine.U2D;                  

namespace BlocXplode
{
    public sealed class SpriteLibrary
    {
        public const int MaxColors = 6;                         // the game has 6 block colours
        public const int Tiers = 4;                             // every colors has 4 tiers

        const string AtlasResourceName = "GameSpriteAtlas";     
        
        static readonly string[] ColorNames = { "Yellow", "Blue", "Green", "Pink", "Purple", "Red" };
        static readonly string[] TierNames = { "Default", "A", "B", "C" };

        readonly Sprite[] colorSprites = new Sprite[MaxColors * Tiers];   // 6 colours x 4 tiers in one flat array

        public Sprite BoxIntact { get; private set; }       // box with full health 
        public Sprite BoxDamaged { get; private set; }      // box after one hit 
        public Sprite Pixel { get; private set; }           // plain white square, tinted for backdrops and masks
        public Sprite Panel { get; private set; }           // rounded 9-slice panel for cards and chips
        public Sprite Button { get; private set; }          // rounded 9-slice button with a 3D lower edge
        public Sprite IconHome { get; private set; }        // home icon
        public Sprite IconPlay { get; private set; }        // play  icon
        public Sprite IconRetry { get; private set; }       // retry icon
        public Sprite IconNext { get; private set; }        // arrow icon

        // Returns the sprite of a block colour (0..5) and icon tier (0..3).
        public Sprite Get(int color, int tier) => colorSprites[color * Tiers + tier];

        // Loads the atlas and fetches each sprite exactly once.
        public static SpriteLibrary Load()
        {
            var atlas = Resources.Load<SpriteAtlas>(AtlasResourceName);     // find the atlas inside Resources folder
            if (atlas == null)                                              // if it doesnt exist
                throw new InvalidOperationException(
                    "Sprite atlas 'Resources/" + AtlasResourceName + "' was not found. In the editor use Tools > BlocXplode > Rebuild Sprite Atlas.");

            var result = new SpriteLibrary();                               // the object we fill and return
            for (int c = 0; c < MaxColors; c++)                             // every colour
                for (int t = 0; t < Tiers; t++)                             // every icon tier
                    result.colorSprites[c * Tiers + t] = Fetch(atlas, ColorNames[c] + "_" + TierNames[t]);   // example "Blue_A"

            result.BoxIntact = Fetch(atlas, "Box1");                        // full health box
            result.BoxDamaged = Fetch(atlas, "Box0");                       // damaged box
            result.Pixel = Fetch(atlas, "Pixel");
            result.Panel = Fetch(atlas, "UI_Panel");
            result.Button = Fetch(atlas, "UI_Button");
            result.IconHome = Fetch(atlas, "UI_IconHome");
            result.IconPlay = Fetch(atlas, "UI_IconPlay");
            result.IconRetry = Fetch(atlas, "UI_IconRetry");
            result.IconNext = Fetch(atlas, "UI_IconNext");
            return result;
        }

        // SpriteAtlas.GetSprite creates a NEW Sprite object on every call, so we call it once per sprite, never per frame.
        static Sprite Fetch(SpriteAtlas atlas, string name)
        {
            Sprite sprite = atlas.GetSprite(name);                          // ask the atlas for the sprite by name
            if (sprite == null) throw new InvalidOperationException("Sprite '" + name + "' is missing from the sprite atlas.");
            return sprite;
        }
    }
}
