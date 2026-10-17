using UnityEngine;                          

namespace BlocXplode                        
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "BlocXplode/Game Settings")]
    public sealed class GameSettings : ScriptableObject    
    {
        [Header("Performance")]                             
        [Range(30, 120)] public int targetFrameRate = 60;   

        [Header("Falling blocks")]
        public float gravity = 40f;                        
        public float maxFallSpeed = 26f;                  

        [Header("Animations (seconds)")]
        public float popDuration = 0.18f;                   
        public float punchDuration = 0.16f;                 

        [Header("Deadlock shuffle (seconds)")]
        public float shuffleWait = 0.7f;                   
        public float shuffleDuration = 0.5f;                

        [Header("Game flow (seconds)")]
        public float resultDelay = 0.9f;                    

        [Header("Icon thresholds - identical for every level")]
        public int iconThresholdA = 4;                      
        public int iconThresholdB = 6;                     
        public int iconThresholdC = 8;                      

        public int IconTier(int groupSize)
        {
            if (groupSize > iconThresholdC) return 3;       
            if (groupSize > iconThresholdB) return 2;
            if (groupSize > iconThresholdA) return 1;
            return 0;                                       
        }
        
        void OnValidate()
        {
            iconThresholdA = Mathf.Max(1, iconThresholdA);                   
            iconThresholdB = Mathf.Max(iconThresholdA + 1, iconThresholdB);  
            iconThresholdC = Mathf.Max(iconThresholdB + 1, iconThresholdC);  
        }
    }
}
