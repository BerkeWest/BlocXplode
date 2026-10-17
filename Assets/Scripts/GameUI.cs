using System;                                  
using UnityEngine;
using UnityEngine.EventSystems;                 
using UnityEngine.UI;

namespace BlocXplode
{
    public sealed class GameUI
    {
        public event Action PlayRequested;              // menu: Play / Continue
        public event Action ResetProgressRequested;     // menu: Reset progress
        public event Action MenuRequested;              // HUD or result screen: go to the main menu
        public event Action RetryRequested;             // result screen: restart the level
        public event Action NextRequested;              // result screen: go to the next level

        const float PopDuration = 0.35f;                // length of the pop-in animation of the result card

        readonly SpriteLibrary sprites;                 // pictures
        readonly UITheme theme;                         // colours
        readonly UIBuilder ui;                          // helper that creates panels / text / buttons

        RectTransform safeRoot;                         // parent of the HUD, shrunk to the phone's safe area

        // main menu
        GameObject menuPanel;                           // the whole menu
        Text menuLevelValue;                            // the big level number on the menu card
        Text playLabel;                                 // "PLAY" or "CONTINUE"
        RectTransform[] decorRects;                     // floating decoration blocks
        float[] decorPhase;                             // each block bobs with its own phase

        // HUD
        GameObject hudRoot;                             // the whole HUD
        GameObject toastRoot;                           // the "shuffling" message
        Text levelValue, movesValue, toastText;
        readonly GameObject[] goalSlots = new GameObject[(int)ObstacleType.Count];   // one slot per obstacle type
        readonly Text[] goalTexts = new Text[(int)ObstacleType.Count];               // its "remaining" number
        readonly int[] shownRemaining = new int[(int)ObstacleType.Count];            // what each number currently shows

        // result pop-up
        GameObject resultPanel;                         // dim overlay + card
        RectTransform resultPop;                        // the card and its banner; scaled for the pop-in animation
        Image resultBanner;                             // coloured title banner (green / red)
        Text resultTitle, resultMessage, resultDetail;
        UIBuilder.ButtonParts resultPrimary;            // "Next Level" / "Play Again" / "Restart"
        Image[] resultBlocks;                           // three decoration blocks on the card
        bool resultIsNext;                              // does the primary button go to the next level?
        float resultPopTimer;                           // seconds since the card appeared

        float toastTimer;                               // seconds the toast stays
        Rect lastSafeArea;                              // detects a changed safe area (rotation, split screen)
        int shownLevel = -1, shownMoves = -1;           // last displayed values (to skip redundant text updates)

        // True while the menu or a result pop-up covers the board: taps on the board must be ignored.
        public bool ModalOpen => menuPanel.activeSelf || resultPanel.activeSelf;

        public GameUI(SpriteLibrary spriteLibrary, UITheme uiTheme)
        {
            sprites = spriteLibrary;
            theme = uiTheme;
            ui = new UIBuilder(sprites, theme);
            Build();                                    // create every screen once; later we only show / hide them
        }

        public void ShowMenu(int levelNumber, bool hasProgress)
        {
            hudRoot.SetActive(false);                   // hide the game screens ...
            toastRoot.SetActive(false);
            resultPanel.SetActive(false);
            menuLevelValue.text = levelNumber.ToString();                 // the level the player will continue on
            playLabel.text = hasProgress ? "CONTINUE" : "PLAY";           // first time = Play, afterwards = Continue
            menuPanel.SetActive(true);                  // ... and show the menu
        }

        public void ShowHud()
        {
            menuPanel.SetActive(false);
            resultPanel.SetActive(false);
            hudRoot.SetActive(true);
        }

        public void SetLevel(int levelNumber)
        {
            if (levelNumber == shownLevel) return;      // unchanged: do not touch the text (avoids garbage)
            shownLevel = levelNumber;
            levelValue.text = levelNumber.ToString();
        }

        public void SetMoves(int moves)
        {
            if (moves == shownMoves) return;
            shownMoves = moves;
            movesValue.text = moves.ToString();
        }

        // Shows one goal entry (icon + remaining amount) for every obstacle type the level contains.
        public void ConfigureGoals(int[] totals)
        {
            for (int t = 0; t < goalSlots.Length; t++)
            {
                goalSlots[t].SetActive(totals[t] > 0);  // hide obstacle types that are not in this level
                shownRemaining[t] = -1;                 // force the number to refresh
            }
        }

        // Updates the "remaining" number of every obstacle type.
        public void SetGoalsRemaining(int[] remaining)
        {
            for (int t = 0; t < goalSlots.Length; t++)
            {
                if (remaining[t] == shownRemaining[t]) continue;
                shownRemaining[t] = remaining[t];
                goalTexts[t].text = remaining[t].ToString();
            }
        }

        public void ShowToast(string message, float seconds)
        {
            toastText.text = message;
            toastTimer = seconds;
            toastRoot.SetActive(true);
        }

        // Shows the victory (win = true) or defeat pop-up.
        // hasNextLevel : on a win, does another level exist? (false => the primary button says "Play Again")
        public void ShowResult(bool win, bool hasNextLevel, string message, string detail)
        {
            toastRoot.SetActive(false);
            resultBanner.color = win ? theme.win : theme.lose;                 // green or red banner
            resultTitle.text = win ? "LEVEL COMPLETE!" : "LEVEL FAILED";
            resultMessage.text = message;
            resultDetail.text = detail;

            resultIsNext = win && hasNextLevel;                                 // primary button destination
            resultPrimary.label.text = win ? (hasNextLevel ? "NEXT LEVEL" : "PLAY AGAIN") : "RESTART";
            resultPrimary.background.color = win ? theme.buttonPrimary : theme.buttonRetry;
            resultPrimary.icon.sprite = win && hasNextLevel ? sprites.IconNext : sprites.IconRetry;

            // decoration: bright special-icon blocks for a win, dull default blocks for a defeat
            int[] winColors = { 0, 2, 3 };                                      // yellow, green, pink
            int[] loseColors = { 5, 4, 1 };                                     // red, purple, blue
            for (int i = 0; i < resultBlocks.Length; i++)
            {
                resultBlocks[i].sprite = sprites.Get(win ? winColors[i] : loseColors[i], win ? 3 : 0);
                resultBlocks[i].color = win ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);   // grey tint on defeat
            }

            resultPopTimer = 0f;                                                // restart the pop-in animation
            resultPop.localScale = Vector3.one * 0.6f;
            resultPanel.SetActive(true);
        }

        // Called every frame by GameBootstrap with the unscaled frame time.
        public void Tick(float dt)
        {
            if (toastTimer > 0f)                        // hide the toast when its time is up
            {
                toastTimer -= dt;
                if (toastTimer <= 0f) toastRoot.SetActive(false);
            }

            if (menuPanel.activeSelf) AnimateMenu();    // floating blocks

            if (resultPanel.activeSelf && resultPopTimer < PopDuration)   // pop-in of the result card
            {
                resultPopTimer += dt;
                float t = Mathf.Clamp01(resultPopTimer / PopDuration);
                // "ease out back": overshoots a little past 1 and settles, for a springy feel
                const float c1 = 1.70158f;
                float u = t - 1f;
                float eased = 1f + (c1 + 1f) * u * u * u + c1 * u * u;
                resultPop.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, eased);
            }

            if (Screen.safeArea != lastSafeArea) ApplySafeArea();   // phone rotated / notch changed
        }

        // Makes the menu decoration blocks bob up and down.
        void AnimateMenu()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < decorRects.Length; i++)
                decorRects[i].anchoredPosition = new Vector2(0f, Mathf.Sin(time * 1.4f + decorPhase[i]) * 14f);
        }

        void Build()
        {
            // The EventSystem delivers touches to buttons. Create one if the scene has none.
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();                             // classic Input Manager
            }

            // The Canvas is the surface all UI is drawn on.
            var canvasGo = new GameObject("UI", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;      // drawn on top of the game, no camera needed
            var scaler = canvasGo.AddComponent<CanvasScaler>();     // makes UI scale with the screen resolution
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f); // designed for a vertical full-HD phone
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;                       // balance between width and height scaling
            canvasGo.AddComponent<GraphicRaycaster>();              // lets the canvas receive clicks
            var canvasRect = (RectTransform)canvasGo.transform;

            safeRoot = UIBuilder.Rect("SafeArea", canvasRect, Vector2.zero, Vector2.one);   // HUD parent (notch aware)
            ApplySafeArea();

            BuildHud();
            BuildToast();
            BuildMenu(canvasRect);
            BuildResult(canvasRect);

            hudRoot.SetActive(false);                   // everything starts hidden; the menu is shown by GameBootstrap
            toastRoot.SetActive(false);
            resultPanel.SetActive(false);
        }

        void BuildHud()
        {
            // The HUD occupies the top HudFraction of the screen (the camera keeps the board below it).
            var hud = UIBuilder.Rect("Hud", safeRoot, new Vector2(0f, 1f - CameraFitter.HudFraction), Vector2.one);
            hudRoot = hud.gameObject;

            // Round-ish menu button with a house icon.
            ui.Button(hud, "", new Vector2(0.03f, 0.12f), new Vector2(0.19f, 0.88f), theme.buttonSecondary,
                () => MenuRequested?.Invoke(), 40, sprites.IconHome);

            movesValue = Chip(hud, "Moves", "MOVES", new Vector2(0.22f, 0.10f), new Vector2(0.46f, 0.90f), theme.accent);
            levelValue = Chip(hud, "Level", "LEVEL", new Vector2(0.48f, 0.10f), new Vector2(0.71f, 0.90f), Color.white);

            // Goal chip: caption on top, below it one "icon + remaining number" slot per obstacle type.
            // It is created WITHOUT a big value of its own: the slots show the numbers, so nothing sits behind them.
            RectTransform goalChip = CreateChip(hud, "Goal", "GOAL", new Vector2(0.73f, 0.10f), new Vector2(0.97f, 0.90f));
            int types = (int)ObstacleType.Count;
            for (int t = 0; t < types; t++)
            {
                // slots share the chip's width equally
                var slot = UIBuilder.Rect("Goal" + (ObstacleType)t, goalChip,
                    new Vector2((float)t / types, 0.02f), new Vector2((t + 1f) / types, 0.64f));
                var iconRect = UIBuilder.Rect("Icon", slot, new Vector2(0.06f, 0f), new Vector2(0.52f, 1f));
                iconRect.localScale = Vector3.one * 0.7f;           // the goal icon is drawn at 70% of its size
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = ObstacleIcon((ObstacleType)t);        // ONE icon per obstacle type, whatever its health
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                goalTexts[t] = ui.Label(UIBuilder.Rect("Remaining", slot, new Vector2(0.54f, 0f), Vector2.one),
                    "0", 64, TextAnchor.MiddleLeft, theme.textPrimary);
                goalSlots[t] = slot.gameObject;
                slot.gameObject.SetActive(false);                   // shown by ConfigureGoals when the level has this obstacle
            }
        }

        // Which picture represents an obstacle type in the goal panel.
        Sprite ObstacleIcon(ObstacleType type)
        {
            switch (type)
            {
                case ObstacleType.Box: return sprites.BoxIntact;    // boxes always use the full-health picture
                default: return sprites.Pixel;                      // unknown future obstacle: placeholder
            }
        }

        // Creates a rounded chip (background + small caption) and returns its rectangle.
        RectTransform CreateChip(RectTransform parent, string name, string caption, Vector2 aMin, Vector2 aMax)
        {
            var chip = UIBuilder.Rect(name + "Chip", parent, aMin, aMax);
            ui.Panel(chip, theme.panel, false);                                       // rounded background
            ui.Label(UIBuilder.Rect("Caption", chip, new Vector2(0.05f, 0.64f), new Vector2(0.95f, 0.97f)),
                caption, 30, TextAnchor.MiddleCenter, theme.textDim);                 // small title
            return chip;
        }

        // A chip with a big value below the caption (moves, level). Returns the value text.
        Text Chip(RectTransform parent, string name, string caption, Vector2 aMin, Vector2 aMax, Color valueColor)
        {
            RectTransform chip = CreateChip(parent, name, caption, aMin, aMax);
            return ui.Label(UIBuilder.Rect("Value", chip, new Vector2(0f, 0.02f), new Vector2(1f, 0.68f)),
                "0", 78, TextAnchor.MiddleCenter, valueColor);                        // big number
        }

        void BuildToast()
        {
            var rect = UIBuilder.Rect("Toast", safeRoot, new Vector2(0.10f, 0.755f), new Vector2(0.90f, 0.835f));
            ui.Panel(rect, new Color(0.05f, 0.03f, 0.15f, 0.92f), false);
            toastText = ui.Label(rect, "", 42, TextAnchor.MiddleCenter, theme.accent);
            toastRoot = rect.gameObject;
        }

        void BuildMenu(RectTransform canvasRect)
        {
            var root = UIBuilder.Rect("MainMenu", canvasRect, Vector2.zero, Vector2.one);
            ui.Solid(root, theme.background, true);                     // opaque background that also blocks clicks
            menuPanel = root.gameObject;

            // Floating decoration blocks around the edges (position, colour, rotation).
            float[,] deco =
            {
                { 0.13f, 0.93f, 0, 14 }, { 0.50f, 0.95f, 3, -8 }, { 0.87f, 0.92f, 2, -16 },
                { 0.07f, 0.56f, 1, 10 }, { 0.93f, 0.57f, 5, -12 },
                { 0.14f, 0.09f, 4, -12 }, { 0.50f, 0.05f, 1, 8 }, { 0.86f, 0.10f, 0, 15 }
            };
            int count = deco.GetLength(0);
            decorRects = new RectTransform[count];
            decorPhase = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 center = new Vector2(deco[i, 0], deco[i, 1]);
                var rt = UIBuilder.Rect("Deco" + i, root, center, center);   // min == max anchors = a point
                rt.sizeDelta = new Vector2(170f, 170f);                      // fixed size in canvas pixels
                rt.localRotation = Quaternion.Euler(0f, 0f, deco[i, 3]);     // tilt
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprites.Get((int)deco[i, 2], 0);
                img.color = new Color(1f, 1f, 1f, 0.55f);                    // translucent so it stays in the background
                img.raycastTarget = false;
                decorRects[i] = rt;
                decorPhase[i] = i * 0.9f;                                    // different phase = not all bobbing together
            }

            // Logo: "BlocXplode" with a differently coloured X. Rich text tags colour parts of one string.
            string gold = ColorUtility.ToHtmlStringRGB(theme.accent);
            string orange = ColorUtility.ToHtmlStringRGB(theme.accentAlt);
            var title = ui.Label(UIBuilder.Rect("Title", root, new Vector2(0f, 0.70f), new Vector2(1f, 0.86f)),
                "<color=#" + gold + ">Bloc</color><color=#" + orange + ">X</color><color=#" + gold + ">plode</color>",
                132, TextAnchor.MiddleCenter, Color.white);
            var outline = title.gameObject.AddComponent<Outline>();          // dark outline makes the logo pop
            outline.effectColor = new Color(0.12f, 0.05f, 0.25f, 1f);
            outline.effectDistance = new Vector2(5f, -5f);

            ui.Label(UIBuilder.Rect("Tagline", root, new Vector2(0f, 0.645f), new Vector2(1f, 0.70f)),
                "Tap groups. Blast the boxes!", 40, TextAnchor.MiddleCenter, theme.textDim);

            // Level card: small caption + the big number of the level the player continues on.
            var card = UIBuilder.Rect("LevelCard", root, new Vector2(0.27f, 0.42f), new Vector2(0.73f, 0.60f));
            ui.Panel(card, theme.panel, false);
            ui.Label(UIBuilder.Rect("Caption", card, new Vector2(0f, 0.68f), new Vector2(1f, 0.96f)),
                "LEVEL", 36, TextAnchor.MiddleCenter, theme.textDim);
            menuLevelValue = ui.Label(UIBuilder.Rect("Value", card, new Vector2(0f, 0.04f), new Vector2(1f, 0.72f)),
                "1", 110, TextAnchor.MiddleCenter, theme.accent);

            // Big green play button.
            var play = ui.Button(root, "PLAY", new Vector2(0.16f, 0.26f), new Vector2(0.84f, 0.375f),
                theme.buttonPrimary, () => PlayRequested?.Invoke(), 64, sprites.IconPlay);
            playLabel = play.label;                                          // text changes between PLAY and CONTINUE

            // Small, quiet reset button.
            ui.Button(root, "Reset progress", new Vector2(0.32f, 0.16f), new Vector2(0.68f, 0.22f),
                theme.buttonQuiet, () => ResetProgressRequested?.Invoke(), 30);
        }

        void BuildResult(RectTransform canvasRect)
        {
            var dim = UIBuilder.Rect("ResultPanel", canvasRect, Vector2.zero, Vector2.one);
            ui.Solid(dim, theme.dim, true);                             // darkens the game and swallows clicks
            resultPanel = dim.gameObject;

            // Everything that pops in lives under 'pop' so one scale animation moves the whole card.
            resultPop = UIBuilder.Rect("Pop", dim, Vector2.zero, Vector2.one);

            var card = UIBuilder.Rect("Card", resultPop, new Vector2(0.07f, 0.28f), new Vector2(0.93f, 0.70f));
            ui.Panel(card, theme.panel, true);

            // Coloured banner overlapping the top edge of the card, holds the title.
            var bannerRect = UIBuilder.Rect("Banner", resultPop, new Vector2(0.14f, 0.645f), new Vector2(0.86f, 0.765f));
            resultBanner = ui.Panel(bannerRect, theme.win, false);
            resultTitle = ui.Label(UIBuilder.Rect("Title", bannerRect, new Vector2(0f, 0.1f), Vector2.one),
                "", 58, TextAnchor.MiddleCenter, Color.white);

            // Three decoration blocks.
            resultBlocks = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = 0.17f + i * 0.22f;
                var rt = UIBuilder.Rect("Block" + i, card, new Vector2(x, 0.58f), new Vector2(x + 0.22f, 0.84f));
                rt.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 9f);   // fan out slightly
                var img = rt.gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                resultBlocks[i] = img;
            }

            resultMessage = ui.Label(UIBuilder.Rect("Message", card, new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.57f)),
                "", 48, TextAnchor.MiddleCenter, theme.textPrimary);
            resultDetail = ui.Label(UIBuilder.Rect("Detail", card, new Vector2(0.05f, 0.27f), new Vector2(0.95f, 0.38f)),
                "", 36, TextAnchor.MiddleCenter, theme.textDim);

            // Primary action (next level / restart) and secondary action (menu).
            resultPrimary = ui.Button(card, "NEXT LEVEL", new Vector2(0.05f, 0.05f), new Vector2(0.56f, 0.24f),
                theme.buttonPrimary, OnResultPrimary, 40, sprites.IconNext);
            ui.Button(card, "MENU", new Vector2(0.60f, 0.05f), new Vector2(0.95f, 0.24f),
                theme.buttonSecondary, () => MenuRequested?.Invoke(), 40, sprites.IconHome);
        }

        // The primary result button either goes on to the next level or replays the current one.
        void OnResultPrimary()
        {
            if (resultIsNext) NextRequested?.Invoke();
            else RetryRequested?.Invoke();
        }

        // Shrinks the HUD parent to the phone's safe area so nothing hides behind a notch or rounded corner.
        void ApplySafeArea()
        {
            Rect sa = Screen.safeArea;                  // safe pixel rectangle reported by the device
            lastSafeArea = sa;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            safeRoot.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);   // convert pixels to 0..1
            safeRoot.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        }
    }
}
