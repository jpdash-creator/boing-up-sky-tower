using System;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The interface chrome of Boing Up: Sky Tower (design direction "daylight glass", 2026-10-20): the painted world art stays the hero and the interface
    /// is calm and crisp around it. Surfaces are soft rounded cards and frosted glass chips, text is ink on light surfaces and white on glass,
    /// the accent is a warm coral with a sky-blue companion, and tabs are a segmented pill bar. All sprites are drawn in code (rounded, 9-sliced,
    /// never stretched) so there is nothing to import. Tokens live here; screens do not hand-pick colours or radii.
    /// </summary>
    public static class Theme
    {
        // ---- colour tokens ----
        public static readonly Color Ink = new Color(0.09f, 0.11f, 0.24f, 1f);              // text on light surfaces (contrast above 12:1 on white)
        public static readonly Color InkSoft = new Color(0.30f, 0.34f, 0.50f, 1f);          // secondary text on light surfaces (about 6:1 on white)
        public static readonly Color Surface = new Color(1f, 1f, 1f, 0.97f);                // popup and card body
        public static readonly Color SurfaceDim = new Color(0.93f, 0.95f, 1f, 1f);          // inset areas on a card
        public static readonly Color Glass = new Color(0.06f, 0.09f, 0.22f, 0.58f);          // frosted bars and chips over the art
        public static readonly Color GlassStroke = new Color(1f, 1f, 1f, 0.26f);            // 2 px rim of a glass chip
        public static readonly Color Accent = new Color(1f, 0.36f, 0.55f, 1f);              // primary action and selected tab (coral pink)
        public static readonly Color AccentDeep = new Color(0.82f, 0.18f, 0.40f, 1f);       // text-safe deeper shade of the accent on white (about 5:1)
        public static readonly Color Sky = new Color(0.16f, 0.60f, 1f, 1f);                 // secondary accent
        public static readonly Color Gold = new Color(1f, 0.78f, 0.24f, 1f);                // currency and stars
        public static readonly Color GoodGreen = new Color(0.17f, 0.74f, 0.47f, 1f);
        public static readonly Color OnGlass = new Color(1f, 1f, 1f, 1f);
        public static readonly Color OnGlassSoft = new Color(0.86f, 0.90f, 1f, 1f);         // secondary text on glass (about 9:1 on the glass colour)

        // ---- shape and spacing tokens (canvas units on the 1080 x 1920 reference) ----
        public const float RadiusCard = 44f;
        public const float RadiusChip = 36f;
        public const float Gap = 16f, GapL = 28f, Margin = 28f;

        static Sprite round, ring, shadow, gradientDown;

        /// <summary>Draws all surface sprites now (they are generated on first use): call it at boot so no overlay or card hitches a run (CUX-1).</summary>
        public static void Warmup()
        {
            Sprite a = RoundSprite, b = RingSprite, c = ShadowSprite, d = GradientDownSprite;
            Glyphs.Warmup();
            if (a == null || b == null || c == null || d == null) Debug.LogWarning("Theme sprites missing");
        }

        /// <summary>White rounded rectangle, 9-sliced: tint it.</summary>
        public static Sprite RoundSprite
        {
            get
            {
                if (round != null) return round;
                round = Build(128, 44, false);
                return round;
            }
        }

        /// <summary>A 2 px white ring (hollow rounded rectangle), 9-sliced: lay it over a glass chip for its rim.</summary>
        public static Sprite RingSprite
        {
            get
            {
                if (ring != null) return ring;
                ring = Build(128, 44, true);
                return ring;
            }
        }

        /// <summary>Soft drop shadow (blurred rounded rectangle), 9-sliced: draw it behind a card, a little larger and lower than the card.</summary>
        public static Sprite ShadowSprite
        {
            get
            {
                if (shadow != null) return shadow;
                const int n = 96, r = 34, border = 40;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Min(x + 0.5f, n - x - 0.5f), dy = Mathf.Min(y + 0.5f, n - y - 0.5f);
                        float d = dx < r && dy < r ? r - Mathf.Sqrt((r - dx) * (r - dx) + (r - dy) * (r - dy)) : Mathf.Min(dx, dy);
                        float a = Mathf.Clamp01(d / 30f);          // 30 px soft edge
                        a = a * a * (3f - 2f * a);
                        px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                shadow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                return shadow;
            }
        }

        /// <summary>Vertical fade: opaque at the top to transparent at the bottom (a top bar that melts into the art instead of a hard band).</summary>
        public static Sprite GradientDownSprite
        {
            get
            {
                if (gradientDown != null) return gradientDown;
                const int h = 64;
                var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[h];
                for (int y = 0; y < h; y++)
                {
                    float t = y / (float)(h - 1);                  // 0 bottom, 1 top
                    float a = Mathf.SmoothStep(0f, 1f, t);
                    px[y] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply();
                gradientDown = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
                return gradientDown;
            }
        }

        static Sprite Build(int n, int r, bool hollow)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            const float stroke = 2.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Min(x + 0.5f, n - x - 0.5f), dy = Mathf.Min(y + 0.5f, n - y - 0.5f);
                    float d = dx < r && dy < r ? r - Mathf.Sqrt((r - dx) * (r - dx) + (r - dy) * (r - dy)) : Mathf.Min(dx, dy);
                    float a = Mathf.Clamp01(d);                     // 1 px anti-aliased edge
                    if (hollow) a *= 1f - Mathf.Clamp01(d - stroke);   // keep only the outer 2.5 px
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        static Image Sliced(Transform parent, string name, Sprite sprite, Color color, float radius)
        {
            Image img = UiKit.AddImage(parent, name, sprite, color);
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            // The sprite's corner is 44 sprite px at 100 ppu = 44 canvas units; scale it to the wanted radius.
            img.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / radius);
            return img;
        }

        /// <summary>A frosted glass chip or bar: translucent dark ink with a faint white rim. Children go under the returned image.</summary>
        public static Image GlassPanel(Transform parent, string name, float radius = RadiusChip)
        {
            Sprite pill = ToyKit.Get("plate_pill");
            if (pill != null) return ToyKit.Sliced(parent, name, pill, radius * 2f);
            Image img = Sliced(parent, name, RoundSprite, Glass, radius);
            Image rim = Sliced(img.transform, "Rim", RingSprite, GlassStroke, radius);
            UiKit.Stretch(rim.rectTransform);
            return img;
        }

        /// <summary>
        /// A light card with a soft drop shadow. Returns the card image; its parent is a box (named name + "Box") that holds the shadow behind the
        /// card, so place the BOX (card.transform.parent) and add content under the card.
        /// </summary>
        public static Image Card(Transform parent, string name, float radius = RadiusCard)
        {
            RectTransform box = UiKit.Rect(parent, name + "Box");
            Image sh = Sliced(box, name + "Shadow", ShadowSprite, new Color(0.04f, 0.07f, 0.2f, 0.30f), radius);
            sh.rectTransform.anchorMin = Vector2.zero;
            sh.rectTransform.anchorMax = Vector2.one;
            sh.rectTransform.offsetMin = new Vector2(-30f, -50f);
            sh.rectTransform.offsetMax = new Vector2(30f, 14f);
            Image card = KitSurface(box, name, radius);
            UiKit.Stretch(card.rectTransform);
            return card;
        }

        /// <summary>Adds the shadow and the light card as the first children of a host rectangle (content added later draws above them).</summary>
        public static Image CardBacking(RectTransform host, float radius = RadiusCard)
        {
            Image sh = Sliced(host, "Shadow", ShadowSprite, new Color(0.04f, 0.07f, 0.2f, 0.30f), radius);
            sh.rectTransform.anchorMin = Vector2.zero;
            sh.rectTransform.anchorMax = Vector2.one;
            sh.rectTransform.offsetMin = new Vector2(-30f, -50f);
            sh.rectTransform.offsetMax = new Vector2(30f, 14f);
            Image card = KitSurface(host, "Surface", radius);
            UiKit.Stretch(card.rectTransform);
            return card;
        }

        /// <summary>True for the decorative surface sprites drawn by this class (the layout audit does not treat them as content images).</summary>
        static Sprite skyGradient;

        /// <summary>A calm sky-blue gradient with a few soft clouds: the background of Settings and the level screen (owner issue 14).</summary>
        public static RectTransform SkyBackground(Transform parent)
        {
            if (skyGradient == null)
            {
                const int h = 128;
                var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                {
                    float t = y / (h - 1f);   // 0 bottom, 1 top
                    Color c = Color.Lerp(new Color(0.80f, 0.92f, 1f), new Color(0.34f, 0.66f, 0.98f), t);
                    tex.SetPixel(0, y, c);
                    tex.SetPixel(1, y, c);
                }
                tex.Apply();
                skyGradient = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
            }
            RectTransform root = UiKit.Rect(parent, "Sky");
            UiKit.Stretch(root);
            Image g = UiKit.AddImage(root, "Gradient", skyGradient, Color.white);
            g.preserveAspect = false;
            UiKit.Stretch(g.rectTransform);
            // Soft clouds: wide white ellipses at fixed fractions of the screen.
            float[][] clouds = { new[] { 0.18f, 0.80f, 520f, 150f, 0.55f }, new[] { 0.82f, 0.62f, 640f, 170f, 0.45f }, new[] { 0.30f, 0.38f, 600f, 160f, 0.4f }, new[] { 0.78f, 0.16f, 700f, 190f, 0.5f } };
            for (int i = 0; i < clouds.Length; i++)
            {
                Image c = UiKit.AddImage(root, "Cloud" + i, RoundSprite, new Color(1f, 1f, 1f, clouds[i][4]));
                c.type = Image.Type.Sliced;
                c.preserveAspect = false;
                c.pixelsPerUnitMultiplier = 0.35f;
                c.rectTransform.anchorMin = c.rectTransform.anchorMax = new Vector2(clouds[i][0], clouds[i][1]);
                c.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                c.rectTransform.sizeDelta = new Vector2(clouds[i][2], clouds[i][3]);
            }
            root.SetAsFirstSibling();
            return root;
        }

        /// <summary>The cream kit panel (toy-plate look) or, without the kit art, the plain light rounded surface.</summary>
        static Image KitSurface(Transform parent, string name, float radius)
        {
            Sprite panel = ToyKit.Get("panel_cream");
            if (panel != null) return ToyKit.Sliced(parent, name, panel, radius * 1.4f);
            return Sliced(parent, name, RoundSprite, Surface, radius);
        }

        public static bool IsSurfaceSprite(Sprite s)
        {
            return ToyKit.IsKitSprite(s) || s != null && (s == round || s == ring || s == shadow || s == gradientDown);
        }

        /// <summary>
        /// Segmented tab bar: a glass pill holding N equal tabs; the selected tab is a coral pill with white text, the others are soft white text.
        /// Each tab is a real button of at least 48 dp. Returns the buttons; call the selection again to rebuild (screens are rebuilt on change).
        /// </summary>
        public static Button[] TabBar(RectTransform parent, string[] labels, int selected, Action<int> onSelect, float height, int fontSize = 40)
        {
            var bar = GlassPanel(parent, "TabBar", height * 0.5f);
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(-2f * Margin, height);
            const float pad = 8f;
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                bool on = i == selected;
                Button b = UiKit.AddButton(bar.transform, "Tab" + (i + 1), "", null, Color.white, new Vector2(100f, height - 2f * pad), () => onSelect(idx), fontSize);
                b.GetComponent<A11yLabel>().Text = labels[i] + " tab" + (on ? " (selected)" : "");
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i / (float)labels.Length, 0f);
                rt.anchorMax = new Vector2((i + 1) / (float)labels.Length, 1f);
                rt.offsetMin = new Vector2(i == 0 ? pad : pad * 0.5f, pad);
                rt.offsetMax = new Vector2(i == labels.Length - 1 ? -pad : -pad * 0.5f, -pad);
                Image bg = b.GetComponent<Image>();
                bg.sprite = RoundSprite;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / ((height - 2f * pad) * 0.5f));
                bg.color = on ? Gold : new Color(1f, 1f, 1f, 0f);
                Text t = UiKit.AddText(rt, "Label", labels[i], fontSize, on ? Ink : OnGlassSoft, TextAnchor.MiddleCenter, true);
                var o = t.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                UiKit.Stretch((RectTransform)t.transform);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                buttons[i] = b;
            }
            return buttons;
        }
    }
}
