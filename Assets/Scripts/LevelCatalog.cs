// ============================================================================================================
// LevelCatalog.cs
// A ScriptableObject holding the ORDERED list of levels (Assets/Data/LevelCatalog.asset).
// Level 1 is element 0, level 2 is element 1 and so on. To add a level: create a LevelData asset and add it to the
// list (the catalog Inspector has a button that does both). The game asks the catalog whether "the next level
// exists", so it automatically plays beyond level 10 as soon as an 11th level is in the list.
// ============================================================================================================

using UnityEngine;

namespace BlocXplode
{
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "BlocXplode/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] LevelData[] levels;                                

        public int Count => levels == null ? 0 : levels.Length;               
        
        public bool Exists(int index)
        {
            return levels != null && index >= 0 && index < levels.Length && levels[index] != null;
        }

        public LevelData Get(int index) { return levels[index]; }
        
        public int ClampToExisting(int index)
        {
            index = Mathf.Min(index, Count - 1);                               
            while (index > 0 && !Exists(index)) index--;             
            return Mathf.Max(0, index);                                  
        }
    }
}
