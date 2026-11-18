using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Echo
{
    /// <summary>
    /// The staff. In live mode the tape scrolls under a fixed playhead; holds are blocks, rests are the bare line.
    /// In compare mode both performances are laid over the same line so differences are obvious.
    /// </summary>
    public class TapeView
    {
        class Block
        {
            public RectTransform rt;
            public Image fill;
            public Image glow;
        }

        public RectTransform root;

        public float Width { get { return width; } }
        public float HeadX { get { return -width * 0.5f + width * headFrac; } }

        readonly float width;
        readonly float height;
        readonly Sprite blockSprite;
        readonly float blockHeight;
        readonly float headFrac;
        readonly float pxPerSec;
        readonly bool sliced;

        Image line;
        Image lineGlow;
        Image finishLine;
        Image playheadLine;
        Image playheadGlow;
        Image playheadDot;
        Image pulseRing;

        Block[] targetBlocks;
        Block[] playerBlocks;
        Image[] bands;
        Image[] ticks;

        float pulseT = 10f;
        Color pulseColor = Color.white;

        const int BlockPool = 14;
        const int BandPool = 18;
        const int TickPool = 40;

        public TapeView(Transform parent, float x, float y, float w, float h,
                        Sprite blockSprite, float blockHeight, float headFraction, float scrollSpeed, bool sliced)
        {
            this.blockSprite = blockSprite;
            this.blockHeight = blockHeight;
            this.headFrac = headFraction;
            this.pxPerSec = scrollSpeed;
            this.sliced = sliced;
            width = w;
            height = h;

            root = UiKit.Node(parent, "Tape", x, y, w, h);

            lineGlow = UiKit.Image(UiKit.Node(root, "LineGlow", 0f, 0f, w + 60f, 64f), UiKit.Glow, UiKit.Alpha(UiKit.Cyan, 0.05f), false);

            line = UiKit.Image(UiKit.Node(root, "Line", 0f, 0f, w, 3f), UiKit.White, UiKit.Alpha(Color.white, 0.22f), false);

            // ghost blocks in the lane where a hold is missing
            bands = new Image[BandPool];
            for (int i = 0; i < BandPool; i++)
            {
                RectTransform rt = UiKit.Node(root, "Band", 0f, 0f, 64f, 44f);
                rt.pivot = new Vector2(0f, 0.5f);
                bands[i] = UiKit.Image(rt, blockSprite, UiKit.Alpha(UiKit.Red, 0.10f), sliced);
                rt.gameObject.SetActive(false);
            }

            targetBlocks = new Block[BlockPool];
            for (int i = 0; i < BlockPool; i++) targetBlocks[i] = MakeBlock("TargetBlock");

            playerBlocks = new Block[BlockPool];
            for (int i = 0; i < BlockPool; i++) playerBlocks[i] = MakeBlock("PlayerBlock");

            ticks = new Image[TickPool];
            for (int i = 0; i < TickPool; i++)
            {
                RectTransform rt = UiKit.Node(root, "Tick", 0, -52, 2, 16);
                ticks[i] = UiKit.Image(rt, UiKit.White, UiKit.Alpha(Color.white, 0.14f), false);
                rt.gameObject.SetActive(false);
            }

            finishLine = UiKit.Image(UiKit.Node(root, "Finish", 0f, 0f, 2f, 98f), UiKit.White, UiKit.Alpha(Color.white, 0.22f), false);

            playheadGlow = UiKit.Image(UiKit.Node(root, "PlayheadGlow", 0f, 0f, 116f, 300f), UiKit.Glow, UiKit.Alpha(UiKit.Cyan, 0.24f), false);
            playheadLine = UiKit.Image(UiKit.Node(root, "Playhead", 0f, 0f, 4f, 160f), UiKit.White, UiKit.Alpha(Color.white, 0.82f), false);
            playheadDot = UiKit.Image(UiKit.Node(root, "PlayheadDot", 0f, 0f, 14f, 14f), UiKit.Circle, Color.white, false);
            pulseRing = UiKit.Image(UiKit.Node(root, "Pulse", 0, 0, 40, 40), UiKit.Ring, UiKit.Alpha(Color.white, 0f), false);
        }

        Block MakeBlock(string name)
        {
            RectTransform rt = UiKit.Node(root, name, 0, 0, 40, blockHeight);
            rt.pivot = new Vector2(0f, 0.5f);

            RectTransform glowRt = UiKit.Node(rt, "Glow", 0, 0, 40, blockHeight);
            glowRt.anchorMin = Vector2.zero;
            glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-5f, -9f);
            glowRt.offsetMax = new Vector2(5f, 9f);
            Image glow = UiKit.Image(glowRt, blockSprite, UiKit.Alpha(UiKit.Cyan, 0.15f), sliced);

            RectTransform fillRt = UiKit.Node(rt, "Fill", 0, 0, 40, blockHeight);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image fill = UiKit.Image(fillRt, blockSprite, UiKit.Cyan, sliced);

            Block b = new Block();
            b.rt = rt;
            b.fill = fill;
            b.glow = glow;
            rt.gameObject.SetActive(false);
            return b;
        }

        void SetBlock(Block b, bool active, float x, float y, float w, float h, Color color, float glowAlpha)
        {
            if (!active || w < 1f)
            {
                if (b.rt.gameObject.activeSelf) b.rt.gameObject.SetActive(false);
                return;
            }
            if (!b.rt.gameObject.activeSelf) b.rt.gameObject.SetActive(true);
            b.rt.sizeDelta = new Vector2(w, h);
            b.rt.anchoredPosition = new Vector2(x, y);
            b.fill.color = color;
            b.glow.color = UiKit.Alpha(color, glowAlpha);
        }

        void HideBlocks(Block[] blocks)
        {
            for (int i = 0; i < blocks.Length; i++)
                if (blocks[i].rt.gameObject.activeSelf) blocks[i].rt.gameObject.SetActive(false);
        }

        void HideBands()
        {
            for (int i = 0; i < bands.Length; i++)
                if (bands[i].gameObject.activeSelf) bands[i].gameObject.SetActive(false);
        }

        void HideTicks()
        {
            for (int i = 0; i < ticks.Length; i++)
                if (ticks[i].gameObject.activeSelf) ticks[i].gameObject.SetActive(false);
        }

        public void HideAll()
        {
            HideBlocks(targetBlocks);
            HideBlocks(playerBlocks);
            HideBands();
            HideTicks();
            SetPlayhead(false, Color.white);
            root.gameObject.SetActive(false);
        }

        public void Show()
        {
            root.gameObject.SetActive(true);
        }

        public void Pulse(Color color)
        {
            pulseT = 0f;
            pulseColor = color;
        }

        void SetPlayhead(bool active, Color tint)
        {
            playheadGlow.gameObject.SetActive(active);
            playheadLine.gameObject.SetActive(active);
            playheadDot.gameObject.SetActive(active);
            pulseRing.gameObject.SetActive(active && pulseT < 0.55f);
            if (!active) return;

            float bob = 1f + 0.03f * Mathf.Sin(Time.time * 7f);
            playheadLine.color = UiKit.Alpha(Color.white, 0.82f);
            playheadDot.color = Color.white;
            playheadGlow.color = UiKit.Alpha(tint, 0.20f);
            playheadGlow.rectTransform.localScale = new Vector3(1f, bob, 1f);

            if (pulseT < 0.55f)
            {
                pulseT += Time.deltaTime;
                float k = Mathf.Clamp01(pulseT / 0.55f);
                float s = 0.35f + k * 2.8f;
                pulseRing.rectTransform.sizeDelta = new Vector2(40f, 40f) * s;
                pulseRing.color = UiKit.Alpha(pulseColor, (1f - k) * 0.55f);
            }
            else
            {
                pulseRing.gameObject.SetActive(false);
            }

            // little sparks pulse on the line while a hold is active
            float g = 0.55f + 0.45f * Mathf.Sin(Time.time * 6f);
            playheadDot.rectTransform.sizeDelta = Vector2.one * (12f + 2f * g);
        }

        // ------------------------------------------------------------------ live

        public void RenderLive(Rhythm rhythm, float t, bool showTarget, List<Vector2> playerHolds,
                               float openStart, bool holding, bool playerTurn, bool ghostTarget = false)
        {
            root.gameObject.SetActive(true);
            HideBands();

            float headX = -width * 0.5f + width * headFrac;
            bool targetNow = showTarget && !ghostTarget && rhythm.IsHoldAt(t);

            // playhead glow colour follows what is happening right now
            Color tint = holding ? UiKit.Amber : (targetNow ? UiKit.Cyan : UiKit.Alpha(UiKit.Cyan, 0.55f));
            playheadGlow.rectTransform.anchoredPosition = new Vector2(headX, 0f);
            playheadLine.rectTransform.anchoredPosition = new Vector2(headX, 0f);
            playheadDot.rectTransform.anchoredPosition = new Vector2(headX, 0f);
            pulseRing.rectTransform.anchoredPosition = new Vector2(headX, 0f);
            SetPlayhead(true, tint);

            bool activeHold = targetNow;
            line.color = UiKit.Alpha(Color.white, activeHold ? 0.5f : 0.22f);
            lineGlow.color = UiKit.Alpha(UiKit.Cyan, activeHold ? 0.11f : 0.05f);

            // target blocks
            int used = 0;
            if (showTarget)
            {
                for (int i = 0; i < rhythm.segments.Count && used < targetBlocks.Length; i++)
                {
                    RhythmSegment s = rhythm.segments[i];
                    if (!s.hold) continue;
                    float x = headX + (s.start - t) * pxPerSec;
                    float w = s.Duration * pxPerSec;
                    if (x > width * 0.5f + 90f || x + w < -width * 0.5f - 90f) continue;

                    bool hot = !ghostTarget && t >= s.start && t < s.end;
                    Color c = ghostTarget
                        ? UiKit.Alpha(UiKit.Cyan, 0.30f)
                        : (hot ? Color.Lerp(UiKit.Cyan, Color.white, 0.35f) : UiKit.Alpha(UiKit.Cyan, 0.92f));
                    float glowA = ghostTarget ? 0.02f : (hot ? 0.55f : 0.16f);
                    SetBlock(targetBlocks[used], true, x, 0f, w, blockHeight, c, glowA);
                    used++;
                }
            }
            for (int i = used; i < targetBlocks.Length; i++)
                SetBlock(targetBlocks[i], false, 0, 0, 0, 0, Color.white, 0f);

            // player recording
            used = 0;
            if (playerTurn)
            {
                for (int i = 0; i < playerHolds.Count && used < playerBlocks.Length - 1; i++)
                {
                    Vector2 h = playerHolds[i];
                    float x = headX + (h.x - t) * pxPerSec;
                    float w = (h.y - h.x) * pxPerSec;
                    SetBlock(playerBlocks[used], true, x, 0f, Mathf.Max(w, 4f), blockHeight * 0.82f, UiKit.Alpha(UiKit.Amber, 0.92f), 0.22f);
                    used++;
                }
                if (holding && openStart >= 0f)
                {
                    float x = headX + (openStart - t) * pxPerSec;
                    float w = Mathf.Max(4f, (t - openStart) * pxPerSec);
                    SetBlock(playerBlocks[used], true, x, 0f, w, blockHeight * 0.82f, Color.Lerp(UiKit.Amber, Color.white, 0.25f), 0.5f);
                    used++;
                }
            }
            for (int i = used; i < playerBlocks.Length; i++)
                SetBlock(playerBlocks[i], false, 0, 0, 0, 0, Color.white, 0f);

            // ruler ticks every 0.5s
            int tick = 0;
            for (float tt = 0f; tt <= rhythm.length + 0.01f && tick < ticks.Length; tt += 0.5f)
            {
                float x = headX + (tt - t) * pxPerSec;
                if (x < -width * 0.5f - 20f || x > width * 0.5f + 20f) continue;
                bool major = Mathf.Abs(tt % 1f) < 0.01f;
                Image img = ticks[tick];
                img.gameObject.SetActive(true);
                img.rectTransform.anchoredPosition = new Vector2(x, major ? -56f : -52f);
                img.rectTransform.sizeDelta = new Vector2(major ? 2f : 2f, major ? 22f : 14f);
                img.color = UiKit.Alpha(Color.white, major ? 0.22f : 0.11f);
                tick++;
            }
            for (int i = tick; i < ticks.Length; i++)
                if (ticks[i].gameObject.activeSelf) ticks[i].gameObject.SetActive(false);

            // the tape's end scrolls in so the player knows when the attempt is over
            float endX = headX + (rhythm.length - t) * pxPerSec;
            bool endVisible = endX > -width * 0.5f - 30f && endX < width * 0.5f + 30f;
            finishLine.gameObject.SetActive(endVisible);
            if (endVisible)
            {
                finishLine.rectTransform.anchoredPosition = new Vector2(endX, 0f);
                finishLine.color = UiKit.Alpha(Color.white, 0.22f);
            }
        }

        // --------------------------------------------------------------- compare

        public void RenderCompare(Rhythm rhythm, List<Vector2> holds, float shift, float reveal)
        {
            root.gameObject.SetActive(true);
            SetPlayhead(false, Color.white);
            finishLine.gameObject.SetActive(false);
            line.color = UiKit.Alpha(Color.white, 0.28f);
            lineGlow.color = UiKit.Alpha(UiKit.Cyan, 0.04f);

            float pad = 26f;
            float pps = (width - pad * 2f) / Mathf.Max(0.1f, rhythm.length);
            float x0 = -width * 0.5f + pad;
            float th = Mathf.Clamp01(reveal * 1.7f);
            float ph = Mathf.Clamp01((reveal - 0.22f) * 1.45f);
            float bh = 44f;
            float yT = 36f;
            float yP = -36f;

            // aligned copy of the player's holds
            List<Vector2> shifted = new List<Vector2>(holds.Count);
            for (int i = 0; i < holds.Count; i++)
            {
                float s = holds[i].x - shift;
                float e = holds[i].y - shift;
                if (e > 0f && s < rhythm.length) shifted.Add(new Vector2(Mathf.Max(0f, s), Mathf.Min(rhythm.length, e)));
            }

            // target lane
            int used = 0;
            for (int i = 0; i < rhythm.segments.Count && used < targetBlocks.Length; i++)
            {
                RhythmSegment s = rhythm.segments[i];
                if (!s.hold) continue;
                SetBlock(targetBlocks[used], true, x0 + s.start * pps, yT, s.Duration * pps, bh,
                         UiKit.Alpha(UiKit.Cyan, 0.95f * th), 0.20f * th);
                used++;
            }
            for (int i = used; i < targetBlocks.Length; i++) SetBlock(targetBlocks[i], false, 0, 0, 0, 0, Color.white, 0f);

            // player lane
            used = 0;
            for (int i = 0; i < shifted.Count && used < playerBlocks.Length; i++)
            {
                Vector2 h = shifted[i];
                SetBlock(playerBlocks[used], true, x0 + h.x * pps, yP, (h.y - h.x) * pps, bh,
                         UiKit.Alpha(UiKit.Amber, 0.95f * ph), 0.22f * ph);
                used++;
            }
            for (int i = used; i < playerBlocks.Length; i++) SetBlock(playerBlocks[i], false, 0, 0, 0, 0, Color.white, 0f);

            // mismatch shading
            HideBands();
            if (reveal > 0.45f && bands.Length > 0)
            {
                float bandA = Mathf.Clamp01((reveal - 0.45f) / 0.4f) * 0.30f;
                int bi = 0;
                bool inBand = false;
                float bandStart = 0f;
                float step = 0.01f;
                int n = Mathf.Max(1, Mathf.RoundToInt(rhythm.length / step));
                for (int i = 0; i <= n && bi < bands.Length; i++)
                {
                    float tt = Mathf.Min(i * step, rhythm.length);
                    bool targetHold = rhythm.IsHoldAt(tt);
                    bool playerHold = RhythmScorer.IsHold(shifted, tt);
                    bool missing = targetHold && !playerHold;
                    if (missing && !inBand)
                    {
                        inBand = true;
                        bandStart = tt;
                    }
                    else if (!missing && inBand)
                    {
                        float w = (tt - bandStart) * pps;
                        if (w > 2f && bi < bands.Length)
                        {
                            Image band = bands[bi];
                            band.gameObject.SetActive(true);
                            band.rectTransform.anchoredPosition = new Vector2(x0 + bandStart * pps, -36f);
                            band.rectTransform.sizeDelta = new Vector2(Mathf.Max(w, 2f), 44f);
                            band.color = UiKit.Alpha(UiKit.Red, bandA);
                            bi++;
                        }
                        inBand = false;
                    }
                }
                if (inBand && bi < bands.Length)
                {
                    float w = (rhythm.length - bandStart) * pps;
                    if (w > 2f)
                    {
                        Image band = bands[bi];
                        band.gameObject.SetActive(true);
                        band.rectTransform.anchoredPosition = new Vector2(x0 + bandStart * pps, -36f);
                        band.rectTransform.sizeDelta = new Vector2(Mathf.Max(w, 2f), 44f);
                        band.color = UiKit.Alpha(UiKit.Red, bandA);
                    }
                }
            }

            // one tick per second
            HideTicks();
            int tick = 0;
            for (float tt = 0f; tt <= rhythm.length + 0.01f && tick < ticks.Length; tt += 1f)
            {
                Image img = ticks[tick];
                img.gameObject.SetActive(true);
                img.rectTransform.anchoredPosition = new Vector2(x0 + tt * pps, -86f);
                img.rectTransform.sizeDelta = new Vector2(2f, 12f);
                img.color = UiKit.Alpha(Color.white, 0.16f);
                tick++;
            }
        }
    }
}
