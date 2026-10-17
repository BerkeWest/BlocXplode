// ============================================================================================================
// SpriteAtlasBuilder.cs  (editor only)
// Creates the Unity Sprite Atlas (version 2) "Assets/Resources/GameSpriteAtlas.spriteatlasv2" from the folder
// Assets/Art/Sprites. It runs automatically the first time the project opens (when the atlas asset does not exist)
// and can be re-run with Tools > BlocXplode > Rebuild Sprite Atlas. It also switches the project to Sprite Atlas V2 so the
// atlas is packed in Play mode too (Project Settings > Editor > Sprite Atlas > Mode).
// ============================================================================================================

using System.IO;                               
using UnityEditor;
using UnityEditor.U2D;                        
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;              

namespace BlocXplode.EditorTools
{
    [InitializeOnLoad]                          
    public static class SpriteAtlasBuilder
    {
        const string AtlasPath = "Assets/Resources/GameSpriteAtlas.spriteatlasv2";

        static SpriteAtlasBuilder()
        {
            EditorApplication.delayCall += EnsureAtlas;
        }

        [MenuItem("Tools/BlocXplode/Rebuild Sprite Atlas")]
        static void Rebuild() { Build(); }

        static void EnsureAtlas()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;   

            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2 &&
                EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2Build)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            if (!File.Exists(AtlasPath)) Build();                        
        }

        static void Build()
        {
            var folder = AssetDatabase.LoadAssetAtPath<Object>(SourceSpriteImportSettings.Folder);  
            if (folder == null)
            {
                Debug.LogError("Sprite atlas source folder not found: " + SourceSpriteImportSettings.Folder);
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SourceSpriteImportSettings.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);   
            }

            Directory.CreateDirectory("Assets/Resources");               

            var atlasAsset = new SpriteAtlasAsset();                       
            atlasAsset.Add(new Object[] { folder });                      
            SpriteAtlasAsset.Save(atlasAsset, AtlasPath);                 
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);

            var atlasImporter = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
            if (atlasImporter != null)
            {
                var packing = atlasImporter.packingSettings;               
                packing.enableRotation = false;                            
                packing.enableTightPacking = false;                        
                packing.padding = 4;                                       
                atlasImporter.packingSettings = packing;

                var texture = atlasImporter.textureSettings;
                texture.generateMipMaps = false;                           
                texture.readable = false;                                  
                texture.filterMode = FilterMode.Bilinear;
                atlasImporter.textureSettings = texture;

                var platform = atlasImporter.GetPlatformSettings("DefaultTexturePlatform");   
                platform.maxTextureSize = 2048;                            
                platform.textureCompression = TextureImporterCompression.CompressedHQ;        
                atlasImporter.SetPlatformSettings(platform);

                atlasImporter.SaveAndReimport();                           
            }

            Debug.Log("Sprite atlas created: " + AtlasPath);
        }
    }
}
