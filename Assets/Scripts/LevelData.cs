using System;                                  
using System.Text;                            
using UnityEngine;

namespace BlocXplode
{
    [CreateAssetMenu(fileName = "Level_New", menuName = "BlocXplode/Level")]
    public sealed class LevelData : ScriptableObject
    {
        // Level data ranges
        public const int MinRows = 2, MaxRows = 10;
        public const int MinColumns = 2, MaxColumns = 10;
        public const int MinColors = 1, MaxColors = 6;

        // Default level data
        public int rows = 8;                   
        public int columns = 8;                
        public int colors = 4;                  
        public int moves = 20;                 
        public int seed = 1;                   


        public string[] layout;
        
        public int BoxCount()
        {
            int n = 0;                                                     
            for (int r = 0; r < rows; r++)                                    
                for (int c = 0; c < columns; c++)                             
                    if (GetBoxHealth(r, c) > 0) n++;                          
            return n;
        }

        public void CountObstacles(int[] counts)
        {
            Array.Clear(counts, 0, counts.Length);                           
            counts[(int)ObstacleType.Box] = BoxCount();                       
        }

        public int GetBoxHealth(int row, int col)
        {
            if (layout == null) return 0;                                     
            int i = rows - 1 - row;                                           
            if (i < 0 || i >= layout.Length || layout[i] == null) return 0;   
            if (col < 0 || col >= layout[i].Length) return 0;                 
            char ch = layout[i][col];                                        
            return ch == 'B' ? 2 : ch == 'b' ? 1 : 0;                         
        }

        public void SetBoxHealth(int row, int col, int health)
        {
            EnsureLayout();                                                  
            int i = rows - 1 - row;                                          
            char[] chars = layout[i].ToCharArray();                           
            chars[col] = health >= 2 ? 'B' : health == 1 ? 'b' : '.';         
            layout[i] = new string(chars);                                   
        }

        // Clamps every value into its range
        public void Sanitize()
        {
            rows = Mathf.Clamp(rows, MinRows, MaxRows);                      
            columns = Mathf.Clamp(columns, MinColumns, MaxColumns);           
            colors = Mathf.Clamp(colors, MinColors, MaxColors);              
            moves = Mathf.Max(1, moves);                                     
            EnsureLayout();                                                  
        }

        public void EnsureLayout()
        {
            var fixedLayout = new string[rows];                               
            var sb = new StringBuilder(columns);                           
            for (int i = 0; i < rows; i++)                                   
            {
                string old = layout != null && i < layout.Length && layout[i] != null ? layout[i] : "";   
                sb.Length = 0;                                               
                for (int j = 0; j < columns; j++)                             
                {
                    char ch = j < old.Length ? old[j] : '.';                  
                    sb.Append(ch == 'B' || ch == 'b' ? ch : '.');             
                }
                fixedLayout[i] = sb.ToString();                              
            }
            layout = fixedLayout;                                             
        }

        void OnValidate() { Sanitize(); }

        // A tiny built-in level used only if the catalog is empty, so the game never crashes
        public static LevelData CreateFallback()
        {
            var level = CreateInstance<LevelData>();                          
            level.Sanitize();                                                
            level.SetBoxHealth(3, 3, 2);                                      
            level.SetBoxHealth(3, 4, 1);                                     
            level.moves = 15;
            return level;
        }
    }
}
