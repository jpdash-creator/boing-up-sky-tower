using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SkyHopper.Gameplay
{
    /// <summary>Accessibility label of an interactive element (bridged to the platform screen reader later). CUX-6.</summary>
    public sealed class A11yLabel : MonoBehaviour
    {
        public string Text;
    }

    /// <summary>Keeps a UI rect inside the device safe area (notches, rounded corners). CUX-3 / CUX-10.</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect last = new Rect(-1f, -1f, -1f, -1f);

        void Update() { Apply(); }
        void OnEnable() { Apply(); }

        public void Apply()
        {
            Rect sa = UiKit.SafeAreaNormalized();
            if (sa == last) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin, sa.yMin);
            rt.anchorMax = new Vector2(sa.xMax, sa.yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Code-built uGUI helpers. Reference resolution 1080 x 1920. Sizes follow the critical UX rules (CUX-3..5):
    /// at least 48 dp touch targets, at least 8 dp spacing, at least 16 sp text, text scale option, safe area.
    /// </summary>
    public static class UiKit
    {
        public static readonly Vector2 Reference = new Vector2(1080f, 1920f);

        /// <summary>Reference pixels per dp (a typical phone is about 411 dp wide: 1080 / 411).</summary>
        public const float PxPerDp = 1080f / 411f;
        public const float MinTouchPx = 48f * PxPerDp;      // about 126
        public const float MinSpacingPx = 8f * PxPerDp;     // about 21
        public const float MinBodyTextPx = 16f * PxPerDp;   // about 42
        public const float LargeTextPx = 24f * PxPerDp;     // about 63 (3:1 contrast is enough above this)

        /// <summary>Accessibility text scale (1.0 to 1.3), applied to every text created afterwards.</summary>
        public static float TextScale = 1f;

        /// <summary>Normalised (0..1) safe area. Tests replace this to simulate notches.</summary>
        public static Func<Rect> SafeAreaNormalized = () =>
        {
            Rect sa = Screen.safeArea;
            return new Rect(sa.x / Screen.width, sa.y / Screen.height, sa.width / Screen.width, sa.height / Screen.height);
        };

        static Font bodyFont, displayFont;

        /// <summary>Body text: Nunito Bold (OFL). Falls back to the built-in font if the asset is missing.</summary>
        public static Font DefaultFont
        {
            get
            {
                if (bodyFont == null) bodyFont = Resources.Load<Font>("Fonts/Nunito-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return bodyFont;
            }
        }

        /// <summary>Display text (titles, numbers, button labels): Lilita One (OFL).</summary>
        public static Font DisplayFont
        {
            get
            {
                if (displayFont == null) displayFont = Resources.Load<Font>("Fonts/LilitaOne-Regular") ?? DefaultFont;
                return displayFont;
            }
        }

        /// <summary>Sizes from this up use the display font automatically.</summary>
        public const int DisplayFromSize = 56;

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>A full-stretch child that follows the safe area. Put all interactive and text content under it.</summary>
        public static RectTransform SafeArea(Transform parent)
        {
            RectTransform rt = Rect(parent, "Safe");
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>().Apply();
            return rt;
        }

        /// <summary>Anchors an element to a corner/edge/centre point (pivot = anchor) and positions it by pixel offset and size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image AddImage(Transform parent, string name, Sprite sprite, Color color)
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = sprite == null && color == Color.white ? new Color(1f, 1f, 1f, 0.15f) : color;
            img.preserveAspect = sprite != null;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Font size is raised to the 16 sp minimum and multiplied by the accessibility text scale.</summary>
        public static Text AddText(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool display = false)
        {
            RectTransform rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = display || size >= DisplayFromSize ? DisplayFont : DefaultFont;
            t.text = text;
            t.fontSize = Mathf.RoundToInt(Mathf.Max(size, MinBodyTextPx) * TextScale);
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            // The outline grows with the text so big titles stay readable over bright art (CUX-5).
            float d = Mathf.Max(2.5f, t.fontSize * 0.045f);
            outline.effectColor = new Color(0.05f, 0.05f, 0.12f, 0.9f);
            outline.effectDistance = new Vector2(d, -d);
            return t;
        }

        /// <summary>Deep ink for labels on the glossy button art (white fails 4.5:1 on the pink and cyan buttons).</summary>
        public static readonly Color ButtonInk = new Color(0.10f, 0.06f, 0.14f, 1f);

        /// <summary>WCAG contrast ratio between two opaque colours (sRGB inputs).</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        static float Luminance(Color c)
        {
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        static float Lin(float v) { return v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f); }

        static Sprite pill;

        /// <summary>A white rounded rectangle (9-sliced): tint it for backings behind icons and numbers.</summary>
        public static Sprite PillSprite
        {
            get
            {
                if (pill != null) return pill;
                const int n = 64, r = 24;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Min(x + 0.5f, n - x - 0.5f), dy = Mathf.Min(y + 0.5f, n - y - 0.5f);
                        float d = dx < r && dy < r ? r - Mathf.Sqrt((r - dx) * (r - dx) + (r - dy) * (r - dy)) : Mathf.Min(dx, dy);
                        byte a = (byte)(Mathf.Clamp01(d) * 255f);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                pill = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return pill;
            }
        }

        public static Image AddPanel(Transform parent, string name, Color tint)
        {
            Image img = AddImage(parent, name, PillSprite, tint);
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            return img;
        }

        /// <summary>
        /// Clean popup frame drawn in code (never stretched): dark rim, cream body, the game's pink ribbon with the title, and a real round X.
        /// Returns the panel (named "Panel"); callers add their content under it. The close handler may be null (no X).
        /// </summary>
        public static RectTransform BuildPopupFrame(Transform parent, string title, ArtCatalog art, Vector2 size, Action onClose)
        {
            AudioManager.Play(Sfx.Popup);
            Image rim;
            if (art != null && art.PopupFrame != null)
            {
                // On-style 9-slice frame from the art set: corners keep their shape, only the flat middle stretches.
                rim = AddImage(parent, "Panel", art.PopupFrame, Color.white);
                rim.type = Image.Type.Sliced;
                rim.preserveAspect = false;
                rim.pixelsPerUnitMultiplier = 3.3f;   // 200 px corners are drawn about 60 units wide
                Place(rim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
                rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                rim = AddPanel(parent, "Panel", new Color(0.20f, 0.10f, 0.16f, 1f));
                Place(rim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
                rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                Image fill = AddPanel(rim.transform, "Fill", new Color(0.99f, 0.94f, 0.84f, 1f));
                Stretch(fill.rectTransform);
                fill.rectTransform.offsetMin = new Vector2(10f, 10f);
                fill.rectTransform.offsetMax = new Vector2(-10f, -10f);
            }

            // Compact card (owner, 2026-10-20): no ribbon. A big outlined title in the display font sits on the frame, and a round X icon in the corner.
            Text t = AddText(rim.transform, "Title", title, title.Length > 14 ? 60 : 84, new Color(0.60f, 0.16f, 0.30f, 1f), TextAnchor.MiddleCenter, true);
            var tout = t.GetComponent<Outline>();
            if (tout != null) { tout.effectColor = new Color(1f, 1f, 1f, 0.9f); tout.effectDistance = new Vector2(3f, -3f); }
            Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(-40f, -50f), new Vector2(560f, 130f));

            if (onClose != null)
            {
                Sprite icon = art != null ? art.CloseIcon : null;
                Button x = AddButton(rim.transform, "CloseX", icon != null ? "" : "X", icon != null ? icon : PillSprite, icon != null ? Color.white : new Color(0.96f, 0.80f, 0.55f, 1f), new Vector2(130f, 130f), onClose, 64, false);
                x.onClick.AddListener(() => AudioManager.Play(Sfx.Back));
                if (icon == null) x.GetComponent<Image>().type = Image.Type.Sliced;
                x.GetComponent<A11yLabel>().Text = "Close";
                Place((RectTransform)x.transform, new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(130f, 130f));
            }
            return rim.rectTransform;
        }

        public static readonly Color PanelInk = new Color(0.20f, 0.11f, 0.16f, 1f);

        /// <summary>Dark ink for text on the light cream popup panels (white text there fails the 4.5:1 contrast rule).</summary>
        public static Text OnPanel(Text t)
        {
            t.color = PanelInk;
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            return t;
        }

        /// <summary>An invisible touch target placed over art that has a baked-in close mark, so the mark is a real control.</summary>
        public static Button AddHitArea(Transform parent, string name, string label, Vector2 anchor, Vector2 position, Vector2 size, Action onClick)
        {
            Button b = AddButton(parent, name, null, null, new Color(1f, 1f, 1f, 0f), size, onClick);
            b.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
            b.GetComponent<A11yLabel>().Text = label;
            Place((RectTransform)b.transform, anchor, position, new Vector2(Mathf.Max(size.x, MinTouchPx), Mathf.Max(size.y, MinTouchPx)));
            return b;
        }

        /// <summary>Touch target. Size is raised to the 48 dp minimum so no button can be built too small.</summary>
        public static Button AddButton(Transform parent, string name, string label, Sprite sprite, Color tint, Vector2 size, Action onClick, int fontSize = 44, bool tapSound = true)
        {
            size = new Vector2(Mathf.Max(size.x, MinTouchPx), Mathf.Max(size.y, MinTouchPx));
            RectTransform rt = Rect(parent, name);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = sprite != null ? tint : new Color(tint.r, tint.g, tint.b, 0.9f);
            img.type = Image.Type.Simple;
            if (sprite != null && sprite.border.sqrMagnitude > 0.5f)
            {
                // 9-sliced art (the glossy buttons): the corners are drawn at about 38 percent of the button height, so a wide button is
                // never a stretched square (owner list 11).
                img.type = Image.Type.Sliced;
                float corner = 0.38f * Mathf.Min(size.x, size.y);
                float borderUnits = Mathf.Max(sprite.border.x, sprite.border.y) / (sprite.pixelsPerUnit / 100f);
                img.pixelsPerUnitMultiplier = Mathf.Max(1f, borderUnits / corner);
            }
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            if (tapSound) btn.onClick.AddListener(() => AudioManager.Play(Sfx.Tap));
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<A11yLabel>().Text = string.IsNullOrEmpty(label) ? name : label;
            if (!string.IsNullOrEmpty(label))
            {
                Text t = AddText(rt, "Label", label, fontSize, ButtonInk, TextAnchor.MiddleCenter, true);
                var o = t.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                Stretch((RectTransform)t.transform);
            }
            return btn;
        }

        public static void SetText(Text t, string value)
        {
            if (t != null && !string.Equals(t.text, value)) t.text = value;
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
