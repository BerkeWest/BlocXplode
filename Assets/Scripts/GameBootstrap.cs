using UnityEngine;

namespace BlocXplode
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        // The three data assets, assigned in the Inspector (Assets/Data/...). [SerializeField] exposes private fields.
        [Header("Data assets (ScriptableObjects)")]
        [SerializeField] GameSettings settings;     // numbers: gravity, durations, icon thresholds ...
        [SerializeField] UITheme theme;             // colours
        [SerializeField] LevelCatalog catalog;      // the ordered list of levels

        const string ProgressKey = "blocxplode.level";   // PlayerPrefs key: index of the level the player is on

        enum Phase { Menu, Playing, Ending, Result }     // Ending = last blast is playing out, Result = pop-up visible

        Camera cam;                         // the game camera
        BoardController board;              // gameplay logic and visuals of the grid
        GameUI ui;                          // all screens
        SpriteLibrary sprites;              // all sprites from the atlas

        Phase phase;                        // where the game flow currently is
        int levelIndex;                     // 0-based index of the level being played
        LevelData level;                    // the level being played
        int movesLeft;                      // remaining blasts
        readonly int[] totals = new int[(int)ObstacleType.Count];      // obstacles of each type in the level
        readonly int[] remaining = new int[(int)ObstacleType.Count];   // obstacles still to destroy
        bool endWin;                        // did the level end with a win?
        string endMessage;                  // text for the result pop-up
        float endTimer;                     // counts down the short delay before the pop-up
        int lastW, lastH;                   // last screen size, to refit the camera when it changes

        // Awake runs once when the scene starts, before any Update.
        void Awake()
        {
            // If a data asset was not assigned, create a default one in memory so the game still starts.
            if (settings == null) settings = ScriptableObject.CreateInstance<GameSettings>();
            if (theme == null) theme = ScriptableObject.CreateInstance<UITheme>();

            Application.targetFrameRate = settings.targetFrameRate;   // frame rate cap from the settings asset
            QualitySettings.vSyncCount = 0;                           // let targetFrameRate decide, not the monitor

            SetupCamera();
            sprites = SpriteLibrary.Load();                           // fetch all sprites from the atlas once

            var boardGo = new GameObject("Board");                    // the board lives on its own object
            board = boardGo.AddComponent<BoardController>();
            board.Init(sprites, settings, theme);
            board.Blasted += OnBlasted;                               // subscribe to board events
            board.ShuffleStarted += OnShuffleStarted;
            board.NoMovesLeft += OnNoMovesLeft;
            boardGo.SetActive(false);                                 // hidden until a level starts

            ui = new GameUI(sprites, theme);
            ui.PlayRequested += () => StartLevel(SavedLevelIndex());  // menu: Play / Continue
            ui.ResetProgressRequested += () =>                        // menu: forget the saved level
            {
                PlayerPrefs.DeleteKey(ProgressKey);
                PlayerPrefs.Save();
                ShowMenu();
            };
            ui.MenuRequested += ShowMenu;
            ui.RetryRequested += () => StartLevel(levelIndex);        // same level again (same seed = same start board)
            ui.NextRequested += () => StartLevel(levelIndex + 1);

            ShowMenu();
        }

        // Finds (or creates) the camera and sets it up for cheap 2D rendering.
        void SetupCamera()
        {
            cam = Camera.main;                                        // the camera tagged "MainCamera"
            if (cam == null)                                          // the scene had none: create one
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();                     // avoids the "no audio listener" warning
            }

            cam.orthographic = true;                                  // 2D: no perspective
            cam.clearFlags = CameraClearFlags.SolidColor;             // fill the background with one colour ...
            cam.backgroundColor = theme.background;                   // ... taken from the theme
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
            cam.allowHDR = false;                                     // cheaper render target
            cam.allowMSAA = false;                                    // 2D sprites do not need anti-aliasing
            cam.useOcclusionCulling = false;                          // pointless in 2D
        }
        
        // The level to continue on. If the saved level no longer exists in the catalog, the highest existing one is used.
        int SavedLevelIndex() => catalog == null ? 0 : catalog.ClampToExisting(PlayerPrefs.GetInt(ProgressKey, 0));

        void ShowMenu()
        {
            phase = Phase.Menu;
            board.gameObject.SetActive(false);                        // hide the board behind the menu
            ui.ShowMenu(SavedLevelIndex() + 1, PlayerPrefs.HasKey(ProgressKey));   // +1: players count levels from 1
        }

        // Starts (or restarts) the level with this 0-based index.
        void StartLevel(int index)
        {
            if (catalog != null && catalog.Count > 0)
            {
                levelIndex = catalog.ClampToExisting(index);          // never go past the last existing level
                level = catalog.Get(levelIndex);
            }
            else
            {
                Debug.LogError("The LevelCatalog is missing or empty - playing a built-in fallback level.");
                levelIndex = 0;
                level = LevelData.CreateFallback();
            }

            movesLeft = level.moves;                                  // each level has its own move limit
            level.CountObstacles(totals);                             // the goal: destroy every obstacle of the level
            System.Array.Copy(totals, remaining, totals.Length);      // nothing destroyed yet
            phase = Phase.Playing;

            // Remember where the player is, so "Continue" brings them back here even after quitting mid-level.
            PlayerPrefs.SetInt(ProgressKey, levelIndex);
            PlayerPrefs.Save();

            ui.ShowHud();
            ui.SetLevel(levelIndex + 1);
            ui.SetMoves(movesLeft);
            ui.ConfigureGoals(totals);
            ui.SetGoalsRemaining(remaining);

            board.gameObject.SetActive(true);
            board.InputLocked = false;
            board.Build(level);                                       // fill the board from the level asset

            CameraFitter.Fit(cam, level.rows, level.columns);         // size the view to this board
            lastW = Screen.width;
            lastH = Screen.height;
        }

        // The player blasted a group: spend a move and update the goal.
        void OnBlasted(int groupSize)
        {
            if (phase != Phase.Playing) return;

            movesLeft--;                                              // every blast costs one move
            int[] destroyed = board.ObstaclesDestroyedByLastBlast;    // obstacles this blast destroyed, per type
            bool allDone = true;                                      // assume the goal is reached until proven otherwise
            for (int t = 0; t < remaining.Length; t++)
            {
                remaining[t] = Mathf.Max(0, remaining[t] - destroyed[t]);   // subtract, never below zero
                if (remaining[t] > 0) allDone = false;                      // something is still left
            }
            ui.SetMoves(Mathf.Max(0, movesLeft));
            ui.SetGoalsRemaining(remaining);

            // The goal is checked FIRST: finishing it with the very last move is still a win.
            if (allDone) EndLevel(true, "All obstacles destroyed!");
            else if (movesLeft <= 0) EndLevel(false, "You ran out of moves.");
        }

        // A deadlock that no shuffle can fix (boxes split the board into single blocks).
        void OnNoMovesLeft()
        {
            if (phase == Phase.Playing) EndLevel(false, "No possible moves left.");
        }

        void OnShuffleStarted()
        {
            if (phase == Phase.Playing) ui.ShowToast("No moves left - shuffling!", 1.6f);
        }

        // Ends the level; the result pop-up appears after a short delay so the last blast can play out.
        void EndLevel(bool win, string message)
        {
            phase = Phase.Ending;
            board.InputLocked = true;                                 // no more taps
            endWin = win;
            endMessage = message;
            endTimer = settings.resultDelay;

            if (win)
            {
                // Saved even if that level does not exist yet: "Continue" falls back to the last level and moves on to
                // the new one automatically as soon as it is added to the catalog.
                PlayerPrefs.SetInt(ProgressKey, levelIndex + 1);
                PlayerPrefs.Save();
            }
        }

        void Update()
        {
            ui.Tick(Time.unscaledDeltaTime);                          // toast timer, menu / pop-up animations

            if (phase == Phase.Ending)                                // waiting to show the result
            {
                endTimer -= Time.unscaledDeltaTime;
                if (endTimer <= 0f)
                {
                    phase = Phase.Result;
                    bool hasNext = catalog != null && catalog.Exists(levelIndex + 1);   // asked from the catalog right now
                    string message = endWin && !hasNext ? "You finished every level!" : endMessage;

                    // second line of the pop-up: remaining moves on a win, remaining obstacles on a defeat
                    string detail;
                    if (endWin) detail = "Moves left: " + Mathf.Max(0, movesLeft);
                    else detail = "Boxes left: " + remaining[(int)ObstacleType.Box];
                    ui.ShowResult(endWin, hasNext, message, detail);
                }
            }

            if (phase == Phase.Menu) return;                          // nothing else to do on the menu

            if (Screen.width != lastW || Screen.height != lastH)      // window resized / phone rotated
            {
                lastW = Screen.width;
                lastH = Screen.height;
                CameraFitter.Fit(cam, level.rows, level.columns);
            }

            // Only read taps while playing and while no menu / pop-up is covering the board.
            if (phase == Phase.Playing && !ui.ModalOpen && ReadTap(out Vector2 screenPos))
            {
                // screen pixels -> world position on the z = 0 plane (the camera sits at z = -10)
                Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                board.TryTap(world);
            }
        }

        // Returns true on the frame the player pressed the mouse button / touched the screen, with its position.
        static bool ReadTap(out Vector2 position)
        {

            if (Input.GetMouseButtonDown(0))                          // touches are simulated as mouse clicks by default
            {
                position = Input.mousePosition;
                return true;
            }
            position = default;                                       // no tap this frame
            return false;
        }
    }
}
