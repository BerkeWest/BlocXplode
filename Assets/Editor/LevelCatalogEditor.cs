// ============================================================================================================
// LevelCatalogEditor.cs  (editor only)
// Adds a "Create new level" button under the level list of the LevelCatalog asset. One click creates the next
// numbered LevelData asset in Assets/Data/Levels and appends it to the catalog, so the game plays it after the last level.
// ============================================================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlocXplode.EditorTools
{
    [CustomEditor(typeof(LevelCatalog))]
    public sealed class LevelCatalogEditor : Editor
    {
        const string LevelFolder = "Assets/Data/Levels";

        public override void OnInspectorGUI()
        {
            // default level list
            DrawDefaultInspector();                                    

            var catalog = (LevelCatalog)target;
            EditorGUILayout.Space();

            // empty level warning
            for (int i = 0; i < catalog.Count; i++)
                if (!catalog.Exists(i))
                    EditorGUILayout.HelpBox("Slot " + i + " (Level " + (i + 1) + ") is empty. Levels after an empty slot cannot be reached.",
                        MessageType.Warning);

            // add new level button
            if (GUILayout.Button("Create new level (adds it to the end of the list)", GUILayout.Height(30)))
                CreateLevel(catalog);
        }

        static void CreateLevel(LevelCatalog catalog)
        {
            // make sure the folder exists
            Directory.CreateDirectory(LevelFolder);

            // find a free file name
            int number = catalog.Count + 1;
            string path;
            do { path = LevelFolder + "/Level_" + number.ToString("000") + ".asset"; number++; }
            while (File.Exists(path));

            // create new level
            var level = ScriptableObject.CreateInstance<LevelData>();  
            level.Sanitize();                                          
            AssetDatabase.CreateAsset(level, path);                    

            // append it to the catalog's list through a SerializedObject (keeps undo and saving correct)
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("levels");
            list.arraySize++;                                          
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = level;   
            so.ApplyModifiedProperties();

            // save it and start editing new level
            AssetDatabase.SaveAssets();                              
            Selection.activeObject = level;                           
            EditorGUIUtility.PingObject(level);                       
        }
    }
}
