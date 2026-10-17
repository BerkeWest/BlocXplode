using UnityEngine;

namespace BlocXplode
{
    public static class CameraFitter
    {
        public const float HudFraction = 0.14f;     // the top 14% of the screen is reserved for the HUD
        const float Margin = 0.6f;                  // empty space (in cells) around the board

        // The board is centred on the world origin and each cell is 1 world unit big.
        public static void Fit(Camera cam, int rows, int cols)
        {
            float aspect = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));  // width / height of the screen
            float usable = 1f - HudFraction;                                 // screen fraction left for the board

            float heightForRows = (rows + 2f * Margin) / usable;             // world height needed to show all rows
            float heightForCols = (cols + 2f * Margin) / aspect;             // world height needed to show all columns
            float h = Mathf.Max(heightForRows, heightForCols);               // satisfy both: take the bigger one

            cam.orthographic = true;                                         // 2D camera without perspective
            cam.orthographicSize = h * 0.5f;                                 // orthographicSize is HALF the visible height

            // The board centre must sit in the middle of the area below the HUD, i.e. at 'usable / 2' of the screen
            // height measured from the bottom. The camera centre is at 0.5, so move the camera by the difference.
            float shift = (0.5f - usable * 0.5f) * h;
            cam.transform.position = new Vector3(0f, shift, -10f);           // z = -10 looks at the z = 0 plane
        }
    }
}
