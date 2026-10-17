// ============================================================================================================
// LevelDataEditor.cs  (editor only)
// A custom Inspector for LevelData assets. Select a level asset and you get the normal fields (rows, columns, colours,
// moves, seed) plus a clickable grid to paint the obstacle layout:
//     click a cell:  empty  ->  B (full health box, 2 HP)  ->  b (low health box, 1 HP)  ->  empty
// ============================================================================================================

using UnityEditor;
using UnityEngine;

namespace BlocXplode.EditorTools
{
    [CustomEditor(typeof(LevelData))]
    public sealed class LevelDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // the ordinary fields
            serializedObject.Update();                                         // load the asset's current values
            DrawPropertiesExcluding(serializedObject, "m_Script", "layout");   // draw every field automatically
            serializedObject.ApplyModifiedProperties();                        // write edits back

            var level = (LevelData)target;                                     // the asset being inspected

            // check if the layout has the right size
            if (level.layout == null || level.layout.Length != level.rows)
            {
                Undo.RecordObject(level, "Resize level layout");               // makes the change undoable with Ctrl+Z
                level.EnsureLayout();
                EditorUtility.SetDirty(level);                                 // tell Unity the asset must be saved
            }

            // the clickable layout grid
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Obstacle layout", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Click a cell: empty -> b (low health, 1 HP) -> B (full health, 2 HP) -> empty",
                EditorStyles.miniLabel);

            for (int r = level.rows - 1; r >= 0; r--)                          
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < level.columns; c++)
                {
                    int health = level.GetBoxHealth(r, c);                     // box health data
                    var previous = GUI.backgroundColor;                        // remember previous
                    GUI.backgroundColor = health == 2 ? new Color(0.9f, 0.55f, 0.15f)       // full health: orange
                                        : health == 1 ? new Color(1f, 0.85f, 0.4f)          // low health: light yellow
                                        : Color.white;                                       // empty: white
                    string label = health == 2 ? "B" : health == 1 ? "b" : "";
                    if (GUILayout.Button(label, GUILayout.Width(28), GUILayout.Height(28)))
                    {
                        Undo.RecordObject(level, "Edit level layout");
                        level.SetBoxHealth(r, c, health == 0 ? 1 : health == 1 ? 2 : 0);   // cycle the cell
                        EditorUtility.SetDirty(level);
                    }
                    GUI.backgroundColor = previous;
                }
                EditorGUILayout.EndHorizontal();
            }

            // warnings
            EditorGUILayout.Space();
            int boxes = level.BoxCount();
            EditorGUILayout.HelpBox("Goal: destroy all " + boxes + " box(es). The goal is always every obstacle of the layout.",
                MessageType.Info);
            if (boxes == 0)
                EditorGUILayout.HelpBox("A level needs at least one obstacle, otherwise it is won immediately.", MessageType.Warning);
        }
    }
}
