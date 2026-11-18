using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Echo
{
    /// <summary>Sprite factory, palette, text helpers and a couple of small UI effects.</summary>
    public static class UiKit
    {
        // ------------------------------------------------------------------ palette
        public static readonly Color Ink = Hex("EAF0FF");
        public static readonly Color Muted = Hex("8792B6");
        public static readonly Color Dim = Hex("4B5578");
        public static readonly Color Cyan = Hex("5CE1E6");
        public static readonly Color CyanDeep = Hex("1E7F93");
        public static readonly Color Violet = Hex("8B7CFF");
        public static readonly Color Amber = Hex("FFB35C");
        public static readonly Color Green = Hex("6FE7A8");
        public static readonly Color Red = Hex("FF6B81");
        public static readonly Color CardBg = Hex("12182D");
        public static readonly Color BgTop = Hex("0B1026");
        public static readonly Color BgBottom = Hex("080A18");

        // ------------------------------------------------------------------- sprites
        public static Font FontRef;
        public static Sprite White;
        public static Sprite Pill64;
        public static Sprite Pill48;
        public static Sprite Card;
        public static Sprite Circle;
        public static Sprite Glow;
        public static Sprite Ring;
        public static Sprite Star;
        public static Sprite Heart;
        public static Sprite Gradient;
        public static Sprite Vignette;
        public static Sprite Spark;

        static bool ready;

        /// <summary>
        /// Enter Play Mode Options can be set to skip the domain reload. Static caches would then
        /// survive while Unity has already destroyed the runtime-generated textures, so clear the
        /// cache before every play session and let Init rebuild the sprites.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlayModeCache()
        {
            ready = false;
        }

        public static void Init()
        {
            if (ready) return;
            ready = true;

            try { FontRef = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { FontRef = null; }
            if (FontRef == null)
            {
                try { FontRef = Font.CreateDynamicFontFromOSFont(new string[] { "DejaVu Sans", "Liberation Sans", "Arial" }, 48); }
                catch { }
            }

            White = Solid("White", 4, 4);
            Pill64 = Sliced("Pill64", RoundRect(128, 64, 32f), 32f);
            Pill48 = Sliced("Pill48", RoundRect(96, 48, 12f), 12f);
            Card = Sliced("Card", RoundRect(256, 256, 64f), 64f);
            Circle = Plain("Circle", CircleTex(128, 58f));
            Glow = Plain("Glow", GlowTex(128));
            Ring = Plain("Ring", RingTex(128));
            Star = Plain("Star", Polygon(PolyStar(64f, 26f, 5), 128));
            Heart = Plain("Heart", HeartTex(128));
            Gradient = Plain("Gradient", GradientTex(8, 512));
            Vignette = Plain("Vignette", VignetteTex(256));
            Spark = Plain("Spark", GlowTex(64));
        }

        // --------------------------------------------------------------------- rect
        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(RectTransform parent, Sprite sprite, Color color)
        {
            Image img = parent.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.type = sprite != null && sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            return img;
        }

        public static Image Image(RectTransform parent, Sprite sprite, Color color, bool sliced)
        {
            Image img = Image(parent, sprite, color);
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            return img;
        }

        public static Text Text(Transform parent, string text, int size, Color color, TextAnchor align)
        {
            RectTransform rt = Node(parent, "Text", 0f, 0f, 100f, size * 1.5f);
            return TextOn(rt, text, size, color, align);
        }

        static Text TextOn(RectTransform rt, string text, int size, Color color, TextAnchor align)
        {
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = FontRef;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text Place(Text t, float x, float y, float w, float h, TextAnchor align)
        {
            RectTransform rt = t.rectTransform;
            rt.pivot = new Vector2(align == TextAnchor.MiddleLeft ? 0f : (align == TextAnchor.MiddleRight ? 1f : 0.5f), 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            t.alignment = align;
            return t;
        }

        public static void AddSpacing(Text t, float spacing)
        {
            LetterSpacing ls = t.gameObject.AddComponent<LetterSpacing>();
            ls.spacing = spacing;
            ls.enabled = Mathf.Abs(spacing) > 0.001f;
        }

        public static void AddShadow(Graphic g, Color color, float x, float y)
        {
            Shadow sh = g.gameObject.AddComponent<Shadow>();
            sh.effectColor = color;
            sh.effectDistance = new Vector2(x, y);
            sh.useGraphicAlpha = true;
        }

        public static CanvasGroup Group(RectTransform rt, float alpha)
        {
            CanvasGroup g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            g.interactable = false;
            g.blocksRaycasts = false;
            return g;
        }

        // ------------------------------------------------------------------- easing
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static Color Hex(string hex)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString("#" + hex, out c)) return c;
            return Color.white;
        }

        // ----------------------------------------------------------------- textures
        static Sprite Solid(string name, int w, int h)
        {
            Texture2D tex = NewTex(w, h);
            Color32[] px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static Sprite Plain(string name, Texture2D tex)
        {
            tex.name = name;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static Sprite Sliced(string name, Texture2D tex, float border)
        {
            tex.name = name;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static Texture2D NewTex(int w, int h)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        static Texture2D RoundRect(int w, int h, float radius)
        {
            Texture2D tex = NewTex(w, h);
            Color32[] px = new Color32[w * h];
            float hw = w * 0.5f;
            float hh = h * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - hw) - (hw - radius - 1f);
                    float dy = Mathf.Abs(y + 0.5f - hh) - (hh - radius - 1f);
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float d = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D CircleTex(int size, float radius)
        {
            Texture2D tex = NewTex(size, size);
            Color32[] px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D GlowTex(int size)
        {
            Texture2D tex = NewTex(size, size);
            Color32[] px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - c) / c;
                    float dy = (y + 0.5f - c) / c;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - r, 2.4f) * 0.85f;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D RingTex(int size)
        {
            Texture2D tex = NewTex(size, size);
            Color32[] px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - c * 0.72f) / (c * 0.06f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 190f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D GradientTex(int w, int h)
        {
            Texture2D tex = NewTex(w, h);
            Color32[] px = new Color32[w * h];
            Color bottom = Hex("070912");
            Color mid = Hex("171031");
            Color top = Hex("101A3A");

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color c = t < 0.55f
                    ? Color.Lerp(bottom, mid, t / 0.55f)
                    : Color.Lerp(mid, top, (t - 0.55f) / 0.45f);
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D VignetteTex(int size)
        {
            Texture2D tex = NewTex(size, size);
            Color32[] px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - c) / c;
                    float dy = (y + 0.5f - c) / c;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.05f, r)) * 0.62f;
                    px[y * size + x] = new Color32(2, 3, 8, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D HeartTex(int size)
        {
            List<Vector2> raw = new List<Vector2>();
            for (int i = 0; i < 100; i++)
            {
                float t = i / 100f * Mathf.PI * 2f;
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                raw.Add(new Vector2(x, y));
            }
            return Polygon(raw, size, 0.92f);
        }

        static List<Vector2> PolyStar(float outer, float inner, int points)
        {
            List<Vector2> pts = new List<Vector2>();
            for (int i = 0; i < points * 2; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / points;
                float r = (i % 2 == 0) ? outer : inner;
                pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            return pts;
        }

        static Texture2D Polygon(List<Vector2> pts, int size, float fill = 0.86f)
        {
            // Normalise into the texture.
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                minX = Mathf.Min(minX, pts[i].x);
                maxX = Mathf.Max(maxX, pts[i].x);
                minY = Mathf.Min(minY, pts[i].y);
                maxY = Mathf.Max(maxY, pts[i].y);
            }
            float span = Mathf.Max(maxX - minX, maxY - minY);
            float scale = (size * fill) / Mathf.Max(0.0001f, span);
            float cx = (minX + maxX) * 0.5f;
            float cy = (minY + maxY) * 0.5f;

            Vector2[] p = new Vector2[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                p[i] = new Vector2((pts[i].x - cx) * scale + size * 0.5f, (pts[i].y - cy) * scale + size * 0.5f);

            Texture2D tex = NewTex(size, size);
            Color32[] px = new Color32[size * size];
            const int ss = 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            Vector2 sample = new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                            if (Inside(p, sample)) inside++;
                        }
                    }
                    float a = inside / (float)(ss * ss);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            int j = poly.Length - 1;
            for (int i = 0; i < poly.Length; i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
                j = i;
            }
            return inside;
        }
    }

    /// <summary>A simple per-character tracking effect for uGUI Text.</summary>
    public class LetterSpacing : BaseMeshEffect
    {
        public float spacing;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Mathf.Abs(spacing) < 0.001f) return;

            int count = vh.currentVertCount;
            if (count == 0) return;

            int quads = count / 4;
            UIVertex vert = UIVertex.simpleVert;
            float offset = (quads - 1) * 0.5f;

            for (int q = 0; q < quads; q++)
            {
                float shift = (q - offset) * spacing;
                for (int k = 0; k < 4; k++)
                {
                    int i = q * 4 + k;
                    vh.PopulateUIVertex(ref vert, i);
                    vert.position.x += shift;
                    vh.SetUIVertex(vert, i);
                }
            }
        }
    }
}
