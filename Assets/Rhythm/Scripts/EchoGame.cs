using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Echo
{
    /// <summary>
    /// ECHO — a rhythm memory game.
    /// One staff line. Blocks are holds, gaps are rests. Listen, then play it back from memory.
    /// </summary>
    public class EchoGame : MonoBehaviour
    {
        const float StageW = 1080f;
        const float StageH = 1920f;

        // ------------------------------------------------------------------ ui
        Canvas canvas;
        RectTransform stage;
        RectTransform hudRoot, promptRoot, bottomRoot, titleRoot, resultRoot, fxRoot, muteRoot, guideRoot;
        CanvasGroup titleGroup, resultGroup;
        RectTransform cardRt;

        Text roundLabel, scoreLabel, promptTitle, promptSub, countText, hintLabel;
        Text logo, subtitle, tapPrompt, bestLabel;
        Text resRound, resVerdict, resPercent, resPoints, resScore, resOffset, resContinue;
        Text muteLabel;
        Text guideLabel;

        Image[] hearts;
        Image[] resStars;
        Image countRing;
        RectTransform resLegend;
        TapeView tape, compareTape, demoTape;

        RectTransform[] starRts;
        Image[] starImgs;
        float[] starBase, starPhase, starSpeed;
        Image auroraA, auroraB;

        // --------------------------------------------------------------- audio
        AudioSource music, sfx, holdFx;
        AudioClip clipTick, clipGo, clipTap, clipSuccess, clipFail, clipLostHeart, targetClip;
        bool holdFading;
        readonly Dictionary<int, AudioClip> holdNoteBank = new Dictionary<int, AudioClip>();
        readonly List<AudioClip> holdNoteClips = new List<AudioClip>();

        // ---------------------------------------------------------------- game
        Rhythm target, demo;
        readonly List<Vector2> recordings = new List<Vector2>();
        List<Vector2> targetHolds = new List<Vector2>();
        int[] targetPitches = new int[0];
        int round = 1, score = 0, best = 0, heartsCount = 4, lastHearts = 4, clearedRounds = 0;
        bool roundPassed, inPlay, muted, endedInGameOver, roundIsRetry, guideMode, roundUsedGuide;
        float demoT = -0.6f;
        float lastShift;
        Rhythm retryRhythm;

        // Debug helpers (visible to the editor / dev builds).
        public string debugPhase = "title";
        public bool debugAutoPerfect = false;
        public bool debugCapture = false;
        public bool debugTap = false;

        // --------------------------------------------------------------- input
        bool downNow, downEdge, uiHeld, playHeld;
        Vector2 pointerPos;

        // ----------------------------------------------------------------- fx
        class Spark
        {
            public RectTransform rt;
            public Image img;
            public Vector2 pos, vel;
            public float age, life, size;
            public Color color;
            public bool alive;
        }

        class Confetti
        {
            public RectTransform rt;
            public Image img;
            public Vector2 pos, vel;
            public float angle, spin, age, life, w, h;
            public Color color;
            public bool alive;
        }

        readonly List<Spark> sparks = new List<Spark>();
        readonly List<Confetti> confetti = new List<Confetti>();
        float sparkTimer;
        float shakeT;
        float lastW, lastH;

        static readonly Color[] ConfettiColors =
        {
            UiKit.Cyan, UiKit.Violet, UiKit.Amber, UiKit.Green, Color.white
        };

        // =====================================================================
        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;

            muted = PlayerPrefs.GetInt("Echo.Muted", 0) == 1;
            AudioListener.volume = muted ? 0f : 1f;
            guideMode = PlayerPrefs.GetInt("Echo.Guide", 0) == 1;
            best = PlayerPrefs.GetInt("Echo.Best", 0);

            BuildCamera();
            BuildUi();
            InitAudio();
            ApplySafeArea();
            ShowTitle();
        }

        void Start()
        {
            StartCoroutine(Boot());
        }

        void BuildCamera()
        {
            Camera existing = Camera.main;
            if (existing != null) return;

            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.BgBottom;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.cullingMask = 0;
            cam.depth = -100f;
            go.AddComponent<AudioListener>();
        }

        // ================================================================ build
        void BuildUi()
        {
            UiKit.Init();

            GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(StageW, StageH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            // ------------------------------------------------------- backdrop
            RectTransform bg = UiKit.Stretch(canvas.transform, "Background");

            RectTransform gradRt = UiKit.Stretch(bg, "Gradient");
            Image grad = gradRt.gameObject.AddComponent<Image>();
            grad.sprite = UiKit.Gradient;
            grad.color = Color.white;
            grad.type = Image.Type.Simple;
            grad.preserveAspect = false;
            grad.raycastTarget = false;

            auroraA = UiKit.Image(UiKit.Node(bg, "AuroraCyan", -330, 430, 1100, 1100), UiKit.Glow, UiKit.Alpha(UiKit.Cyan, 0.07f), false);
            auroraB = UiKit.Image(UiKit.Node(bg, "AuroraViolet", 360, -430, 1000, 1000), UiKit.Glow, UiKit.Alpha(UiKit.Violet, 0.07f), false);

            RectTransform vinRt = UiKit.Stretch(bg, "Vignette");
            Image vin = vinRt.gameObject.AddComponent<Image>();
            vin.sprite = UiKit.Vignette;
            vin.color = Color.white;
            vin.type = Image.Type.Simple;
            vin.preserveAspect = false;
            vin.raycastTarget = false;

            // ----------------------------------------------------------- stage
            stage = UiKit.Node(canvas.transform, "Stage", 0f, 0f, StageW, StageH);
            BuildStars();
            BuildHud();
            BuildPrompt();
            BuildBottom();

            tape = new TapeView(stage, 0f, 40f, 1000f, 300f, UiKit.Pill64, 64f, 0.18f, 235f, true);

            BuildTitle();
            BuildResult();
            BuildFx();
            BuildMute();
        }

        void BuildStars()
        {
            RectTransform root = UiKit.Stretch(stage, "Stars");
            int count = 46;
            starRts = new RectTransform[count];
            starImgs = new Image[count];
            starBase = new float[count];
            starPhase = new float[count];
            starSpeed = new float[count];

            for (int i = 0; i < count; i++)
            {
                float size = Random.Range(2.5f, 6.5f);
                RectTransform rt = UiKit.Node(root, "Star", Random.Range(-535f, 535f), Random.Range(-945f, 945f), size, size);
                Image img = UiKit.Image(rt, UiKit.Circle, Color.white);
                starRts[i] = rt;
                starImgs[i] = img;
                starBase[i] = Random.Range(0.10f, 0.5f);
                starPhase[i] = Random.Range(0f, Mathf.PI * 2f);
                starSpeed[i] = Random.Range(0.4f, 1.4f);
                img.color = UiKit.Alpha(Color.white, starBase[i]);
            }
        }

        void BuildHud()
        {
            hudRoot = UiKit.Node(stage, "HUD", 0f, 0f, StageW, StageH);

            roundLabel = UiKit.Text(hudRoot, "ROUND 01", 36, UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.Place(roundLabel, -458f, 858f, 460f, 52f, TextAnchor.MiddleLeft);
            UiKit.AddSpacing(roundLabel, 5f);

            scoreLabel = UiKit.Text(hudRoot, "0", 56, UiKit.Ink, TextAnchor.MiddleRight);
            UiKit.Place(scoreLabel, 458f, 858f, 400f, 82f, TextAnchor.MiddleRight);

            hearts = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                RectTransform rt = UiKit.Node(hudRoot, "Heart" + i, 458f - (3 - i) * 46f, 792f, 34f, 34f);
                hearts[i] = UiKit.Image(rt, UiKit.Heart, UiKit.Red);
            }
        }

        void BuildPrompt()
        {
            promptRoot = UiKit.Node(stage, "Prompt", 0f, 0f, StageW, StageH);

            promptTitle = UiKit.Text(promptRoot, "LISTEN", 64, UiKit.Cyan, TextAnchor.MiddleCenter);
            UiKit.Place(promptTitle, 0f, 432f, 940f, 100f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(promptTitle, 9f);

            promptSub = UiKit.Text(promptRoot, "remember the blocks", 38, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(promptSub, 0f, 346f, 960f, 64f, TextAnchor.MiddleCenter);

            countRing = UiKit.Image(UiKit.Node(promptRoot, "CountRing", 0f, 418f, 260f, 260f), UiKit.Ring, UiKit.Alpha(UiKit.Cyan, 0f));
            countRing.gameObject.SetActive(false);

            countText = UiKit.Text(promptRoot, "3", 210, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Place(countText, 0f, 412f, 700f, 320f, TextAnchor.MiddleCenter);
            UiKit.AddShadow(countText, UiKit.Alpha(Color.black, 0.5f), 0f, -8f);
            countText.gameObject.SetActive(false);
        }

        void BuildBottom()
        {
            bottomRoot = UiKit.Node(stage, "Bottom", 0f, 0f, StageW, StageH);

            hintLabel = UiKit.Text(bottomRoot, "", 38, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(hintLabel, 0f, -620f, 1000f, 60f, TextAnchor.MiddleCenter);
        }

        void BuildTitle()
        {
            titleRoot = UiKit.Stretch(stage, "Title");
            titleGroup = UiKit.Group(titleRoot, 1f);

            UiKit.Image(UiKit.Node(titleRoot, "LogoGlow", 0f, 470f, 820f, 520f), UiKit.Glow, UiKit.Alpha(UiKit.Cyan, 0.06f));

            logo = UiKit.Text(titleRoot, "ECHO", 200, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Place(logo, 0f, 500f, 1000f, 280f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(logo, 30f);
            UiKit.AddShadow(logo, UiKit.Alpha(UiKit.CyanDeep, 0.55f), 0f, -8f);

            subtitle = UiKit.Text(titleRoot, "A RHYTHM MEMORY GAME", 34, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(subtitle, 0f, 348f, 1000f, 60f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(subtitle, 8f);

            demoTape = new TapeView(titleRoot, 0f, 70f, 780f, 260f, UiKit.Pill64, 60f, 0.2f, 190f, true);

            Text i1 = UiKit.Text(titleRoot, "LISTEN TO THE RHYTHM", 40, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Place(i1, 0f, -166f, 940f, 54f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(i1, 5f);
            i1.color = UiKit.Alpha(UiKit.Ink, 0.82f);

            Text i2 = UiKit.Text(titleRoot, "THEN HOLD THE SCREEN TO PLAY IT BACK", 36, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(i2, 0f, -220f, 990f, 54f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(i2, 3f);

            Text i3 = UiKit.Text(titleRoot, "EVERY ROUND GETS LONGER", 30, UiKit.Dim, TextAnchor.MiddleCenter);
            UiKit.Place(i3, 0f, -272f, 900f, 50f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(i3, 4f);

            tapPrompt = UiKit.Text(titleRoot, "TAP TO BEGIN", 56, UiKit.Cyan, TextAnchor.MiddleCenter);
            UiKit.Place(tapPrompt, 0f, -408f, 800f, 82f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(tapPrompt, 7f);

            bestLabel = UiKit.Text(titleRoot, "", 32, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(bestLabel, 0f, -500f, 800f, 52f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(bestLabel, 3f);
        }

        void BuildResult()
        {
            resultRoot = UiKit.Stretch(stage, "Result");
            resultGroup = UiKit.Group(resultRoot, 0f);

            RectTransform dimRt = UiKit.Stretch(resultRoot, "Dim");
            Image dim = dimRt.gameObject.AddComponent<Image>();
            dim.sprite = UiKit.White;
            dim.color = new Color(0.01f, 0.015f, 0.04f, 0.72f);
            dim.type = Image.Type.Simple;
            dim.raycastTarget = false;

            RectTransform rim = UiKit.Node(resultRoot, "CardRim", 0f, 0f, 972f, 1162f);
            UiKit.Image(rim, UiKit.Card, UiKit.Alpha(UiKit.Cyan, 0.16f), true);

            cardRt = UiKit.Node(resultRoot, "Card", 0f, 0f, 960f, 1150f);
            UiKit.Image(cardRt, UiKit.Card, UiKit.CardBg, true);

            resRound = UiKit.Text(cardRt, "ROUND 01", 34, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(resRound, 0f, 488f, 860f, 52f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(resRound, 6f);

            resVerdict = UiKit.Text(cardRt, "CLEARED", 56, UiKit.Green, TextAnchor.MiddleCenter);
            UiKit.Place(resVerdict, 0f, 426f, 900f, 84f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(resVerdict, 9f);

            resPercent = UiKit.Text(cardRt, "0%", 150, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Place(resPercent, 0f, 300f, 900f, 210f, TextAnchor.MiddleCenter);
            UiKit.AddShadow(resPercent, UiKit.Alpha(Color.black, 0.35f), 0f, -6f);

            resStars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform rt = UiKit.Node(cardRt, "ResultStar" + i, (i - 1) * 92f, 184f, 58f, 58f);
                resStars[i] = UiKit.Image(rt, UiKit.Star, UiKit.Alpha(UiKit.Dim, 0.3f));
            }

            compareTape = new TapeView(cardRt, 0f, 28f, 840f, 260f, UiKit.Pill48, 44f, 0.18f, 100f, true);

            resLegend = UiKit.Node(cardRt, "Legend", 0f, -142f, 700f, 40f);
            UiKit.Image(UiKit.Node(resLegend, "Tdot", -175f, 0f, 13f, 13f), UiKit.Circle, UiKit.Cyan);
            Text tl = UiKit.Text(resLegend, "TARGET", 28, UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.Place(tl, -161f, 0f, 160f, 40f, TextAnchor.MiddleLeft);
            UiKit.AddSpacing(tl, 2f);
            UiKit.Image(UiKit.Node(resLegend, "Ydot", 55f, 0f, 13f, 13f), UiKit.Circle, UiKit.Amber);
            Text yl = UiKit.Text(resLegend, "YOU", 28, UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.Place(yl, 69f, 0f, 110f, 40f, TextAnchor.MiddleLeft);
            UiKit.AddSpacing(yl, 2f);

            resOffset = UiKit.Text(cardRt, "TIMING  LOCKED", 32, UiKit.Dim, TextAnchor.MiddleCenter);
            UiKit.Place(resOffset, 0f, -190f, 800f, 46f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(resOffset, 3f);

            resPoints = UiKit.Text(cardRt, "+0", 54, UiKit.Green, TextAnchor.MiddleCenter);
            UiKit.Place(resPoints, 0f, -264f, 800f, 76f, TextAnchor.MiddleCenter);

            resScore = UiKit.Text(cardRt, "SCORE  0", 32, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(resScore, 0f, -316f, 800f, 46f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(resScore, 3f);

            resContinue = UiKit.Text(cardRt, "TAP TO CONTINUE", 40, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Place(resContinue, 0f, -474f, 820f, 68f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(resContinue, 4f);

            resultRoot.gameObject.SetActive(false);
        }

        void BuildFx()
        {
            fxRoot = UiKit.Stretch(stage, "FX");

            for (int i = 0; i < 44; i++)
            {
                RectTransform rt = UiKit.Node(fxRoot, "Spark", 0f, 0f, 8f, 8f);
                Image img = UiKit.Image(rt, UiKit.Spark, UiKit.Alpha(UiKit.Cyan, 0f));
                Spark s = new Spark();
                s.rt = rt;
                s.img = img;
                s.alive = false;
                rt.gameObject.SetActive(false);
                sparks.Add(s);
            }

            for (int i = 0; i < 56; i++)
            {
                RectTransform rt = UiKit.Node(fxRoot, "Confetti", 0f, 0f, 8f, 12f);
                Image img = UiKit.Image(rt, UiKit.White, Color.white);
                Confetti c = new Confetti();
                c.rt = rt;
                c.img = img;
                c.alive = false;
                rt.gameObject.SetActive(false);
                confetti.Add(c);
            }
        }

        void BuildMute()
        {
            muteRoot = UiKit.Node(stage, "Mute", 390f, -868f, 190f, 60f);
            UiKit.Image(muteRoot, UiKit.Pill48, UiKit.Alpha(UiKit.Ink, 0.055f), true);

            muteLabel = UiKit.Text(muteRoot, "SOUND ON", 26, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(muteLabel, 0f, 0f, 190f, 60f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(muteLabel, 3f);

            guideRoot = UiKit.Node(stage, "Guide", -390f, -868f, 190f, 60f);
            UiKit.Image(guideRoot, UiKit.Pill48, UiKit.Alpha(UiKit.Ink, 0.055f), true);
            guideLabel = UiKit.Text(guideRoot, "GUIDE OFF", 26, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(guideLabel, 0f, 0f, 190f, 60f, TextAnchor.MiddleCenter);
            UiKit.AddSpacing(guideLabel, 3f);

            RefreshMute();
            RefreshGuide();
        }

        // =============================================================== audio
        void InitAudio()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = false;
            music.spatialBlend = 0f;
            music.volume = 0.85f;

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            sfx.volume = 0.8f;

            holdFx = gameObject.AddComponent<AudioSource>();
            holdFx.playOnAwake = false;
            holdFx.loop = false;
            holdFx.spatialBlend = 0f;
            holdFx.volume = 0.85f;

            clipTick = Synth.Tick();
            clipGo = Synth.Go();
            clipTap = Synth.Tap();
            clipSuccess = Synth.Success();
            clipFail = Synth.Fail();
            clipLostHeart = Synth.LostHeart();

            demo = Rhythm.Generate(2, 90210);
        }

        // =============================================================== input
        void ReadInput()
        {
            bool pressed = false;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) pressed = true;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) pressed = true;
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) pressed = true;

            downEdge = pressed && !downNow;
            downNow = pressed;

            if (Pointer.current != null) pointerPos = Pointer.current.position.ReadValue();

            if (downEdge)
            {
                if (muteRoot != null && RectTransformUtility.RectangleContainsScreenPoint(muteRoot, pointerPos, null))
                {
                    ToggleMute();
                    uiHeld = true;
                }
                else if (guideRoot != null && RectTransformUtility.RectangleContainsScreenPoint(guideRoot, pointerPos, null))
                {
                    ToggleGuide();
                    uiHeld = true;
                }
                else
                {
                    uiHeld = false;
                }
            }
            if (!downNow) uiHeld = false;

            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) ToggleMute();
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) ToggleGuide();

            playHeld = downNow && !uiHeld;
        }

        void ToggleMute()
        {
            muted = !muted;
            AudioListener.volume = muted ? 0f : 1f;
            PlayerPrefs.SetInt("Echo.Muted", muted ? 1 : 0);
            PlayerPrefs.Save();
            RefreshMute();
        }

        void RefreshMute()
        {
            if (muteLabel == null) return;
            muteLabel.text = muted ? "MUTED" : "SOUND ON";
            muteLabel.color = muted ? UiKit.Alpha(UiKit.Red, 0.75f) : UiKit.Muted;
        }

        void ToggleGuide()
        {
            guideMode = !guideMode;
            PlayerPrefs.SetInt("Echo.Guide", guideMode ? 1 : 0);
            PlayerPrefs.Save();
            RefreshGuide();
            if (inPlay)
            {
                roundUsedGuide |= guideMode;
                if (promptTitle != null && promptTitle.text == "YOUR TURN")
                    promptSub.text = guideMode ? "follow the ghost blocks" : "hold to make a block";
            }
        }

        void RefreshGuide()
        {
            if (guideLabel == null) return;
            guideLabel.text = guideMode ? "GUIDE ON" : "GUIDE OFF";
            guideLabel.color = guideMode ? UiKit.Cyan : UiKit.Muted;
        }

        // ================================================================ frame
        void Update()
        {
            ReadInput();
            AnimateAmbient();
            UpdateSparks();
            UpdateConfetti();
            UpdateHum();
            UpdateDemo();
            UpdatePulse();

            if (Screen.width != lastW || Screen.height != lastH) ApplySafeArea();

            if (shakeT > 0f)
            {
                shakeT = Mathf.Max(0f, shakeT - Time.unscaledDeltaTime);
                float k = shakeT / 0.3f;
                stage.anchoredPosition = Random.insideUnitCircle * (9f * k);
            }
            else if (stage.anchoredPosition != Vector2.zero)
            {
                stage.anchoredPosition = Vector2.zero;
            }
        }

        void AnimateAmbient()
        {
            float t = Time.time;

            if (auroraA != null)
            {
                auroraA.rectTransform.anchoredPosition = new Vector2(-330f + Mathf.Sin(t * 0.11f) * 70f, 430f + Mathf.Cos(t * 0.09f) * 45f);
                auroraA.color = UiKit.Alpha(UiKit.Cyan, 0.065f + 0.02f * Mathf.Sin(t * 0.5f));
            }
            if (auroraB != null)
            {
                auroraB.rectTransform.anchoredPosition = new Vector2(360f + Mathf.Cos(t * 0.08f) * 80f, -430f + Mathf.Sin(t * 0.13f) * 50f);
                auroraB.color = UiKit.Alpha(UiKit.Violet, 0.065f + 0.02f * Mathf.Sin(t * 0.43f + 1f));
            }

            if (starImgs != null)
            {
                for (int i = 0; i < starImgs.Length; i++)
                    starImgs[i].color = UiKit.Alpha(Color.white, starBase[i] * (0.6f + 0.4f * Mathf.Sin(t * starSpeed[i] + starPhase[i])));
            }
        }

        void UpdateDemo()
        {
            if (demoTape == null || titleRoot == null || !titleRoot.gameObject.activeSelf) return;

            demoT += Time.unscaledDeltaTime * 0.9f;
            if (demoT > demo.length + 0.05f) demoT = -0.4f;
            demoTape.RenderLive(demo, demoT, true, null, -1f, false, false);
        }

        void UpdatePulse()
        {
            float t = Time.time;
            if (titleRoot != null && titleRoot.gameObject.activeSelf && tapPrompt != null)
                tapPrompt.color = UiKit.Alpha(UiKit.Cyan, 0.62f + 0.38f * Mathf.Sin(t * 3.4f));

            if (resultRoot != null && resultRoot.gameObject.activeSelf && resContinue != null)
            {
                Color c = resContinue.color;
                c.a = 0.68f + 0.32f * Mathf.Sin(t * 3.2f);
                resContinue.color = c;
            }
        }

        void UpdateHum()
        {
            if (!holdFading) return;
            holdFx.volume = Mathf.MoveTowards(holdFx.volume, 0f, Time.unscaledDeltaTime * 7f);
            if (holdFx.volume <= 0.001f)
            {
                holdFx.Stop();
                holdFading = false;
            }
        }

        void StartHum(float atTime)
        {
            // Play the target's own bell note at this point in the staff.
            AudioClip clip;
            if (!holdNoteBank.TryGetValue(TargetMidiAt(atTime), out clip)) return;

            holdFading = false;
            holdFx.clip = clip;
            holdFx.pitch = 1f;
            holdFx.volume = 0.85f;
            holdFx.Play();
            sparkTimer = 0f;
        }

        int TargetMidiAt(float t)
        {
            int n = Mathf.Min(targetHolds.Count, targetPitches.Length);
            if (n <= 0) return 60; // C4 fallback

            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector2 h = targetHolds[i];
                float d = t < h.x ? h.x - t : (t > h.y ? t - h.y : 0f);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return targetPitches[best];
        }

        void StopHum()
        {
            if (holdFx.isPlaying) holdFading = true;
            if (inPlay) sparkTimer = 0f;
        }

        void BuildHoldNoteBank()
        {
            holdFx.Stop();
            holdFading = false;

            for (int i = 0; i < holdNoteClips.Count; i++)
                if (holdNoteClips[i] != null) Destroy(holdNoteClips[i]);
            holdNoteClips.Clear();
            holdNoteBank.Clear();

            for (int i = 0; i < targetPitches.Length; i++)
            {
                int midi = targetPitches[i];
                if (holdNoteBank.ContainsKey(midi)) continue;
                AudioClip clip = Synth.BuildHoldNote(midi, 1.8f);
                holdNoteBank[midi] = clip;
                holdNoteClips.Add(clip);
            }
        }

        void ApplySafeArea()
        {
            if (canvas == null || stage == null) return;

            float scale = canvas.scaleFactor;
            if (scale <= 0.0001f) return;

            lastW = Screen.width;
            lastH = Screen.height;

            Rect sa = Screen.safeArea;
            float topUnits = (Screen.height - sa.yMax) / scale;
            float bottomUnits = sa.yMin / scale;

            RectTransform cr = (RectTransform)canvas.transform;
            float extra = Mathf.Max(0f, (cr.rect.height - StageH) * 0.5f);

            float shiftTop = Mathf.Max(0f, topUnits - extra - 40f);
            float shiftBottom = Mathf.Max(0f, bottomUnits - extra - 40f);

            if (hudRoot != null) hudRoot.anchoredPosition = new Vector2(0f, -shiftTop);
            if (bottomRoot != null) bottomRoot.anchoredPosition = new Vector2(0f, shiftBottom);
            if (muteRoot != null) muteRoot.anchoredPosition = new Vector2(390f, -868f + shiftBottom);
            if (guideRoot != null) guideRoot.anchoredPosition = new Vector2(-390f, -868f + shiftBottom);
        }

        // ================================================================ flow
        IEnumerator Boot()
        {
            yield return null;
            ShowTitle();

            // First run starts from the title screen.
            while (true)
            {
                if (debugTap)
                {
                    debugTap = false;
                    break;
                }
                if (downEdge && !uiHeld) break;
                yield return null;
            }

            sfx.PlayOneShot(clipTap, 0.55f);
            yield return HideTitle();

            // After a game over, "TAP TO PLAY AGAIN" drops straight into a new run.
            while (true)
            {
                yield return RunGame();
                yield return HideResultCard();
            }
        }

        void ShowTitle()
        {
            titleRoot.gameObject.SetActive(true);
            titleGroup.alpha = 1f;
            titleRoot.localScale = Vector3.one;
            demoT = -0.6f;

            hudRoot.gameObject.SetActive(false);
            promptRoot.gameObject.SetActive(false);
            bottomRoot.gameObject.SetActive(false);
            resultRoot.gameObject.SetActive(false);
            tape.HideAll();
            countText.gameObject.SetActive(false);
            countRing.gameObject.SetActive(false);

            bestLabel.text = best > 0 ? "BEST  " + best.ToString("N0") : "BEST  —";
            debugPhase = "title";
        }

        IEnumerator HideTitle()
        {
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.4f);
                titleGroup.alpha = 1f - UiKit.EaseOutCubic(k);
                titleRoot.localScale = Vector3.one * (1f + 0.05f * k);
                yield return null;
            }
            titleRoot.gameObject.SetActive(false);
            titleGroup.alpha = 0f;
        }

        IEnumerator RunGame()
        {
            score = 0;
            round = 1;
            heartsCount = 4;
            lastHearts = 4;
            clearedRounds = 0;
            endedInGameOver = false;
            retryRhythm = null;
            runningUI(true);
            UpdateHud(false);

            while (true)
            {
                yield return RoundRoutine(round);
                if (endedInGameOver) break;

                if (roundPassed)
                {
                    retryRhythm = null;
                    round++;
                }
                else if (heartsCount <= 0)
                {
                    break;
                }
                else
                {
                    retryRhythm = target;
                }

                yield return new WaitForSeconds(0.25f);
            }

            if (!endedInGameOver) yield return GameOverRoutine();
        }

        void runningUI(bool show)
        {
            // name kept short: bring the in-game chrome back after the title
            hudRoot.gameObject.SetActive(show);
            promptRoot.gameObject.SetActive(show);
            bottomRoot.gameObject.SetActive(show);
            if (show)
            {
                tape.Show();
            }
            else
            {
                tape.HideAll();
            }
        }

        IEnumerator RoundRoutine(int r)
        {
            round = r;
            roundPassed = false;
            recordings.Clear();
            resultRoot.gameObject.SetActive(false);
            runningUI(true);
            countText.gameObject.SetActive(false);
            countRing.gameObject.SetActive(false);

            roundIsRetry = retryRhythm != null;
            target = roundIsRetry ? retryRhythm : Rhythm.Generate(r);
            if (targetClip != null) Destroy(targetClip);
            int[] pitches;
            targetClip = Synth.BuildTrack(target, out pitches);
            targetPitches = pitches;
            targetHolds = target.Holds();
            BuildHoldNoteBank();

            tape.RenderLive(target, -0.6f, true, null, -1f, false, false);

            string roundTitle = "ROUND " + r.ToString("00") + (roundIsRetry ? "  RETRY" : "");
            string roundSub = roundIsRetry
                ? "same rhythm · listen again"
                : (r == 1 ? "listen, then repeat" : target.holdCount + " blocks · listen closely");
            roundLabel.text = roundTitle;
            UpdateHud(false);
            debugPhase = "intro";
            SetPrompt(roundTitle, roundSub, UiKit.Ink);
            hintLabel.text = "watch the staff · listen for the holds";
            yield return new WaitForSeconds(1.0f);

            yield return Countdown(3, UiKit.Cyan);

            SetPrompt("LISTEN", "remember the blocks", UiKit.Cyan);
            hintLabel.text = "watch the blocks · learn the timing";
            debugPhase = "listen";
            yield return new WaitForSeconds(0.15f);
            yield return ListenRoutine();

            SetPrompt("YOUR TURN", "repeat what you heard", UiKit.Amber);
            hintLabel.text = "hold anywhere to draw a block · release to finish";
            debugPhase = "ready";
            yield return new WaitForSeconds(0.85f);

            yield return Countdown(3, UiKit.Amber);

            SetPrompt("YOUR TURN", guideMode ? "follow the ghost blocks" : "hold to make a block", UiKit.Amber);
            debugPhase = "play";
            yield return PlayRoutine();

            StopHum();
            inPlay = false;

            float shift;
            float sim = RhythmScorer.Score(target, recordings, out shift);
            lastShift = shift;
            int stars = RhythmScorer.Stars(sim);
            roundPassed = sim >= RhythmScorer.PassThreshold;

            int pts = 0;
            if (roundPassed)
            {
                pts = Mathf.RoundToInt((400f + 1600f * sim * sim) * (1f + 0.2f * (r - 1)));
                if (stars == 3) pts = Mathf.RoundToInt(pts * 1.15f);
                score += pts;
                clearedRounds++;
                if (score > best)
                {
                    best = score;
                    PlayerPrefs.SetInt("Echo.Best", best);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                heartsCount = Mathf.Max(0, heartsCount - 1);
                StartCoroutine(ShakeScreen());
            }

            UpdateHud(!roundPassed);

            if (!roundPassed && heartsCount <= 0)
            {
                endedInGameOver = true;
                yield return GameOverRoutine();
                yield break;
            }

            yield return ShowResultCard(roundPassed, sim, shift, stars, pts);
            yield return HideResultCard();
        }

        IEnumerator Countdown(int from, Color tint)
        {
            DebugCapture("count", 3, 0.42f);
            promptTitle.gameObject.SetActive(false);
            promptSub.gameObject.SetActive(false);
            countText.gameObject.SetActive(true);
            countRing.gameObject.SetActive(true);

            for (int n = from; n >= 1; n--)
            {
                countText.text = n.ToString();
                countText.color = UiKit.Ink;
                countText.rectTransform.localScale = Vector3.one;
                countRing.color = UiKit.Alpha(tint, 0.16f);
                countRing.rectTransform.sizeDelta = new Vector2(240f, 240f);
                sfx.PlayOneShot(n == 1 ? clipGo : clipTick, 0.5f);

                float dur = 0.46f;
                float t = 0f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    float pop = 1f + 0.28f * (1f - UiKit.EaseOutCubic(Mathf.Clamp01(t / 0.1f)));
                    countText.rectTransform.localScale = Vector3.one * pop;
                    countText.color = UiKit.Alpha(UiKit.Ink, 1f - 0.28f * k);
                    countRing.rectTransform.sizeDelta = new Vector2(240f, 240f) * (1f + 0.55f * k);
                    countRing.color = UiKit.Alpha(tint, (1f - k) * 0.16f);
                    yield return null;
                }
            }

            countText.gameObject.SetActive(false);
            countRing.gameObject.SetActive(false);
            promptTitle.gameObject.SetActive(true);
            promptSub.gameObject.SetActive(true);
        }

        IEnumerator ListenRoutine()
        {
            DebugCapture("listen", 5, 1f);
            double startDsp = AudioSettings.dspTime + 0.25;
            music.clip = targetClip;
            music.volume = 0.85f;
            music.PlayScheduled(startDsp);

            while (true)
            {
                float t = (float)(AudioSettings.dspTime - startDsp);
                tape.RenderLive(target, t, true, null, -1f, false, false);
                if (t > target.length + 0.7f) break;
                yield return null;
            }

            music.Stop();
        }

        IEnumerator PlayRoutine()
        {
            inPlay = true;
            roundUsedGuide = guideMode;
            recordings.Clear();
            sparkTimer = 0f;
            DebugCapture("play", 4, 1f);

            if (debugAutoPerfect)
            {
                recordings.AddRange(target.Holds());
                inPlay = false;
                yield break;
            }

            double startDsp = AudioSettings.dspTime + 0.4;
            bool was = false;
            float openStart = -1f;

            while (true)
            {
                float t = (float)(AudioSettings.dspTime - startDsp);
                float tt = Mathf.Clamp(t, 0f, target.length);
                bool canHold = t >= 0f && t <= target.length;
                bool held = playHeld && canHold;

#if UNITY_EDITOR
                if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
                {
                    recordings.Clear();
                    recordings.AddRange(target.Holds());
                    StopHum();
                    was = false;
                    Debug.Log("[Echo] debug auto-perfect");
                    break;
                }
#endif

                if (held && !was)
                {
                    openStart = tt;
                    StartHum(tt);
                    tape.Pulse(UiKit.Amber);
                    SpawnSparkBurst();
                }
                else if (!held && was)
                {
                    CloseHold(openStart, tt);
                    StopHum();
                }
                was = held;

                tape.RenderLive(target, t, guideMode, recordings, was ? openStart : -1f, was, true, true);

                if (t > target.length + 0.4f) break;
                yield return null;
            }

            if (was) CloseHold(openStart, target.length);
            StopHum();
            inPlay = false;
            tape.RenderLive(target, target.length + 0.4f, guideMode, recordings, -1f, false, true, true);
        }

        void CloseHold(float s, float e)
        {
            s = Mathf.Clamp(s, 0f, target.length);
            e = Mathf.Clamp(e, 0f, target.length);
            if (e - s >= 0.045f) recordings.Add(new Vector2(s, e));
        }

        void SetPrompt(string title, string sub, Color color)
        {
            promptTitle.gameObject.SetActive(true);
            promptSub.gameObject.SetActive(true);
            promptTitle.text = title;
            promptSub.text = sub;
            promptTitle.color = color;
            promptSub.color = UiKit.Muted;
        }

        // ================================================================ score
        void UpdateHud(bool punch)
        {
            if (scoreLabel != null) scoreLabel.text = score.ToString("N0");

            for (int i = 0; i < hearts.Length; i++)
            {
                bool on = i < heartsCount;
                hearts[i].color = on ? UiKit.Red : UiKit.Alpha(UiKit.Dim, 0.4f);
                if (!punch) hearts[i].rectTransform.localScale = Vector3.one;
            }

            if (punch && heartsCount < lastHearts)
            {
                int idx = Mathf.Clamp(heartsCount, 0, hearts.Length - 1);
                StartCoroutine(HeartPunch(idx));
                sfx.PlayOneShot(clipLostHeart, 0.65f);
            }
            lastHearts = heartsCount;
        }

        IEnumerator HeartPunch(int i)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.35f);
                hearts[i].rectTransform.localScale = Vector3.one * (1f + 0.55f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            hearts[i].rectTransform.localScale = Vector3.one;
        }

        IEnumerator ShakeScreen()
        {
            shakeT = 0.3f;
            yield return null;
        }

        IEnumerator ShowResultCard(bool pass, float sim, float shift, int stars, int pts)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(sim * 100f), 0, 100);

            debugPhase = "result";
            bottomRoot.gameObject.SetActive(false);
            promptRoot.gameObject.SetActive(false);

            resultRoot.gameObject.SetActive(true);
            resultGroup.alpha = 0f;
            cardRt.localScale = Vector3.one * 0.9f;
            resLegend.gameObject.SetActive(true);
            resOffset.gameObject.SetActive(true);
            resPercent.fontSize = 150;
            resPoints.fontSize = 54;
            resVerdict.fontSize = 58;
            for (int i = 0; i < 3; i++)
            {
                resStars[i].gameObject.SetActive(true);
                resStars[i].transform.localScale = Vector3.one * 0.7f;
                resStars[i].color = i < stars ? UiKit.Amber : UiKit.Alpha(UiKit.Dim, 0.28f);
            }

            compareTape.Show();
            compareTape.RenderCompare(target, recordings, shift, 0f);
            compareTape.root.gameObject.SetActive(true);

            resRound.text = "ROUND " + round.ToString("00") + (roundIsRetry ? "  ·  RETRY" : "") + (roundUsedGuide ? "  ·  GUIDE" : "");
            resVerdict.text = !pass ? "MISSED" : (stars >= 3 ? "PERFECT" : (stars == 2 ? "GREAT" : "CLEARED"));
            resVerdict.color = !pass ? UiKit.Red : (stars >= 2 ? UiKit.Green : UiKit.Cyan);
            resPercent.text = "0%";
            resPercent.color = UiKit.Ink;
            resPoints.text = pass ? "+" + pts.ToString("N0") : "NO POINTS";
            resPoints.color = pass ? UiKit.Green : UiKit.Red;
            resScore.text = "SCORE  " + score.ToString("N0");
            if (recordings.Count == 0)
            {
                resOffset.text = "NO INPUT";
            }
            else
            {
                resOffset.text = Mathf.Abs(shift) > 0.07f
                    ? "TIMING  " + (shift > 0f ? "+" : "") + shift.ToString("0.00") + "s"
                    : "TIMING  LOCKED";
            }
            resOffset.color = UiKit.Dim;
            resContinue.text = pass ? "TAP TO CONTINUE" : (heartsCount > 0 ? "TAP TO RETRY THIS RHYTHM" : "TAP TO FINISH");
            resContinue.color = UiKit.Ink;

            if (pass) sfx.PlayOneShot(clipSuccess, 0.7f);
            else sfx.PlayOneShot(clipFail, 0.7f);

            float t = 0f;
            while (t < 0.95f)
            {
                t += Time.deltaTime;
                resultGroup.alpha = Mathf.Clamp01(t / 0.18f);
                cardRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, UiKit.EaseOutBack(t / 0.45f));
                compareTape.RenderCompare(target, recordings, shift, Mathf.Clamp01((t - 0.12f) / 0.68f));
                resPercent.text = Mathf.RoundToInt(pct * Mathf.Clamp01((t - 0.18f) / 0.55f)) + "%";
                yield return null;
            }

            resPercent.text = pct + "%";
            compareTape.RenderCompare(target, recordings, shift, 1f);
            DebugCapture("result", 1, 0.1f);

            for (int i = 0; i < stars; i++)
            {
                StartCoroutine(StarPop(i));
                yield return new WaitForSeconds(0.09f);
            }

            if (pass)
            {
                SpawnConfetti(46);
                yield return new WaitForSeconds(0.25f);
            }

            yield return WaitForTap(0.95f, 9999f);
        }

        IEnumerator StarPop(int i)
        {
            float t = 0f;
            while (t < 0.32f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.32f);
                resStars[i].transform.localScale = Vector3.one * (0.7f + 0.3f * UiKit.EaseOutBack(k)) * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
                resStars[i].color = UiKit.Alpha(UiKit.Amber, Mathf.Clamp01(k * 2f));
                yield return null;
            }
            resStars[i].transform.localScale = Vector3.one;
            resStars[i].color = UiKit.Amber;
        }

        IEnumerator HideResultCard()
        {
            if (!resultRoot.gameObject.activeSelf) yield break;

            float t = 0f;
            while (t < 0.22f)
            {
                t += Time.deltaTime;
                resultGroup.alpha = 1f - Mathf.Clamp01(t / 0.22f);
                yield return null;
            }
            resultRoot.gameObject.SetActive(false);
            resultGroup.alpha = 0f;
        }

        IEnumerator GameOverRoutine()
        {
            debugPhase = "gameover";
            bottomRoot.gameObject.SetActive(false);
            promptRoot.gameObject.SetActive(false);
            if (score > best)
            {
                best = score;
                PlayerPrefs.SetInt("Echo.Best", best);
                PlayerPrefs.Save();
            }

            resultRoot.gameObject.SetActive(true);
            resultGroup.alpha = 0f;
            cardRt.localScale = Vector3.one * 0.94f;

            compareTape.Show();
            compareTape.RenderCompare(target, recordings, lastShift, 1f);
            resLegend.gameObject.SetActive(true);
            resOffset.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++) resStars[i].gameObject.SetActive(false);

            resRound.text = "RUN COMPLETE";
            resVerdict.text = "GAME OVER";
            resVerdict.color = UiKit.Ink;
            resVerdict.fontSize = 58;
            resPercent.text = score.ToString("N0");
            resPercent.color = UiKit.Ink;
            resPercent.fontSize = 96;
            resPoints.text = "BEST  " + best.ToString("N0");
            resPoints.color = UiKit.Amber;
            resPoints.fontSize = 40;
            resScore.text = "ROUNDS CLEARED  " + clearedRounds.ToString("00");
            resContinue.text = "TAP TO PLAY AGAIN";
            resContinue.color = UiKit.Ink;

            sfx.PlayOneShot(clipLostHeart, 0.7f);

            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.45f);
                resultGroup.alpha = Mathf.Clamp01(t / 0.22f);
                cardRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, UiKit.EaseOutBack(k));
                yield return null;
            }

            DebugCapture("gameover", 1, 0.1f);
            yield return WaitForTap(1.0f, 9999f);
        }

        IEnumerator WaitForTap(float minTime, float maxTime)
        {
            float t = 0f;
            while (t < maxTime)
            {
                if (debugTap)
                {
                    debugTap = false;
                    sfx.PlayOneShot(clipTap, 0.4f);
                    yield break;
                }
                if (t >= minTime && downEdge && !uiHeld)
                {
                    sfx.PlayOneShot(clipTap, 0.4f);
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }
        }

        void DebugCapture(string name, int count, float interval)
        {
            if (!debugCapture) return;
            StartCoroutine(DebugCaptureRoutine(name, count, interval));
        }

        IEnumerator DebugCaptureRoutine(string name, int count, float interval)
        {
            for (int i = 0; i < count; i++)
            {
                ScreenCapture.CaptureScreenshot("Screenshots/dbg_" + name + "_" + i + ".png", 2);
                yield return new WaitForSeconds(interval);
            }
        }

        // ================================================================== fx
        void SpawnSpark()
        {
            for (int i = 0; i < sparks.Count; i++)
            {
                if (sparks[i].alive) continue;

                Spark s = sparks[i];
                Vector2 head = tape.root.anchoredPosition + new Vector2(tape.HeadX, 0f);
                s.pos = head + Random.insideUnitCircle * 9f;
                s.vel = new Vector2(Random.Range(-35f, 35f), Random.Range(-10f, 60f));
                s.age = 0f;
                s.life = Random.Range(0.4f, 0.9f);
                s.size = Random.Range(3f, 9f);
                s.color = Random.value < 0.55f ? UiKit.Amber : Color.white;
                s.alive = true;
                s.rt.gameObject.SetActive(true);
                s.rt.anchoredPosition = s.pos;
                s.rt.sizeDelta = Vector2.one * s.size;
                s.img.color = UiKit.Alpha(s.color, 0.9f);
                return;
            }
        }

        void SpawnSparkBurst()
        {
            for (int i = 0; i < 9; i++) SpawnSpark();
        }

        void UpdateSparks()
        {
            if (inPlay && playHeld)
            {
                sparkTimer -= Time.deltaTime;
                if (sparkTimer <= 0f)
                {
                    sparkTimer = 0.055f;
                    SpawnSpark();
                }
            }

            for (int i = 0; i < sparks.Count; i++)
            {
                Spark s = sparks[i];
                if (!s.alive) continue;

                s.age += Time.deltaTime;
                if (s.age >= s.life)
                {
                    s.alive = false;
                    s.rt.gameObject.SetActive(false);
                    continue;
                }

                float k = s.age / s.life;
                s.vel.y += 20f * Time.deltaTime;
                s.pos += s.vel * Time.deltaTime;
                s.rt.anchoredPosition = s.pos;
                s.rt.sizeDelta = Vector2.one * (s.size * (1f - 0.7f * k));
                s.img.color = UiKit.Alpha(s.color, (1f - k) * 0.95f);
            }
        }

        void SpawnConfetti(int n)
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < confetti.Count; j++)
                {
                    Confetti c = confetti[j];
                    if (c.alive) continue;

                    c.pos = new Vector2(Random.Range(-430f, 430f), 520f + Random.Range(-20f, 20f));
                    c.vel = new Vector2(Random.Range(-260f, 260f), Random.Range(140f, 540f));
                    c.angle = Random.Range(0f, 360f);
                    c.spin = Random.Range(-400f, 400f);
                    c.age = 0f;
                    c.life = Random.Range(2.1f, 3.4f);
                    c.w = Random.Range(6f, 13f);
                    c.h = Random.Range(6f, 16f);
                    c.color = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                    c.alive = true;
                    c.rt.gameObject.SetActive(true);
                    c.rt.sizeDelta = new Vector2(c.w, c.h);
                    c.rt.anchoredPosition = c.pos;
                    c.rt.localRotation = Quaternion.Euler(0f, 0f, c.angle);
                    c.img.color = c.color;
                    break;
                }
            }
        }

        void UpdateConfetti()
        {
            for (int i = 0; i < confetti.Count; i++)
            {
                Confetti c = confetti[i];
                if (!c.alive) continue;

                c.age += Time.deltaTime;
                if (c.age >= c.life)
                {
                    c.alive = false;
                    c.rt.gameObject.SetActive(false);
                    continue;
                }

                c.vel.y -= 1100f * Time.deltaTime;
                c.pos += c.vel * Time.deltaTime;
                c.angle += c.spin * Time.deltaTime;
                c.rt.anchoredPosition = c.pos;
                c.rt.localRotation = Quaternion.Euler(0f, 0f, c.angle);
                c.img.color = UiKit.Alpha(c.color, Mathf.Clamp01((c.life - c.age) / 0.5f));
            }
        }
    }
}
