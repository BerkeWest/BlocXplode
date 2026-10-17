// ============================================================================================================
// SourceSpriteImportSettings.cs  (editor only)
// An AssetPostprocessor runs automatically every time Unity imports an asset. This one sets the import settings of every
// PNG in Assets/Art/Sprites so nobody has to click them in by hand: they become single Sprites with the right size,
// UI panels / buttons get 9-slice borders. These textures are only the INPUT of the sprite atlas; the atlas packs and
// compresses them, so the originals are never shipped in a build.
// ============================================================================================================

using UnityEditor;
using UnityEngine;

namespace BlocXplode.EditorTools
{
    public sealed class SourceSpriteImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/Sprites";          // PNG folder for the atlas

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder + "/")) return;        // not one of our sprites

            var importer = (TextureImporter)assetImporter;         
            importer.textureType = TextureImporterType.Sprite;      
            importer.spriteImportMode = SpriteImportMode.Single;    

            if (assetPath.EndsWith("/Pixel.png")) importer.spritePixelsPerUnit = 4f;
            else if (assetPath.Contains("/UI_")) importer.spritePixelsPerUnit = 128f;
            else importer.spritePixelsPerUnit = 256f;

            importer.mipmapEnabled = false;                         
            importer.alphaIsTransparency = true;                    
            importer.isReadable = false;                           
            importer.filterMode = FilterMode.Bilinear;             
            importer.wrapMode = TextureWrapMode.Clamp;              
            importer.textureCompression = TextureImporterCompression.Uncompressed;  

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;      
            settings.spriteAlignment = (int)SpriteAlignment.Center; 
            if (assetPath.EndsWith("/UI_Panel.png") || assetPath.EndsWith("/UI_Button.png"))
                settings.spriteBorder = new Vector4(40f, 40f, 40f, 40f);  
            importer.SetTextureSettings(settings);
        }
    }
}
