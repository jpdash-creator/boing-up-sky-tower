using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Keeps a name tag inside its container (shifts it sideways near the left and right edges), so no letter is cut by a mask,
    /// the screen edge or the notch (issue list 24, point 1). It only moves when the container size changes (no per-frame work otherwise).
    /// </summary>
    public sealed class TagKeepInside : MonoBehaviour
    {
        public RectTransform Container;
        public float Margin = TagKit.EdgeMargin;

        static readonly Vector3[] corners = new Vector3[4];
        Vector2 basePos;
        Vector2 lastSize = new Vector2(-1f, -1f);
        int settleFrames = 4;

        /// <summary>Remembers the current anchored position as the unshifted one. Call after the tag has been placed.</summary>
        public void Capture()
        {
            basePos = ((RectTransform)transform).anchoredPosition;
            lastSize = new Vector2(-1f, -1f);
            settleFrames = 4;
        }

        void LateUpdate()
        {
            if (Container == null) return;
            Vector2 size = Container.rect.size;
            if (size == lastSize && settleFrames <= 0) return;
            if (settleFrames > 0) settleFrames--;
            lastSize = size;
            var rt = (RectTransform)transform;
            rt.anchoredPosition = basePos;
            rt.GetWorldCorners(corners);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float x = Container.InverseTransformPoint(corners[i]).x;
                if (x < lo) lo = x;
                if (x > hi) hi = x;
            }
            Rect cr = Container.rect;
            float dx = 0f;
            if (hi > cr.xMax - Margin) dx = (cr.xMax - Margin) - hi;
            if (lo + dx < cr.xMin + Margin) dx = (cr.xMin + Margin) - lo;   // too wide for both sides: the left edge wins
            if (Mathf.Abs(dx) < 0.01f) return;
            float s = Container.lossyScale.x > 0.0001f ? rt.lossyScale.x / Container.lossyScale.x : 1f;
            if (s < 0.0001f) s = 1f;
            rt.anchoredPosition = basePos + new Vector2(dx / s, 0f);
        }
    }

    /// <summary>
    /// Text on a dark or light backing ("name tags"). The backing is always computed from the measured text, never a fixed number:
    /// width = text width + padding (at least 12 dp each side, at least 6 dp top and bottom) + outline and italic overhang; solid (alpha at least 0.92),
    /// small rounded corners; a long label wraps to two lines instead of shrinking. Text is never below 16 sp.
    /// </summary>
    public static class TagKit
    {
        public const float PadX = 12f * UiKit.PxPerDp;     // about 32
        public const float PadY = 6f * UiKit.PxPerDp;      // about 16
        public const float MinFillAlpha = 0.92f;
        public const float EdgeMargin = 44f;               // tags keep this far from the left and right edge of their container
        public const float MapTextMaxWidth = 480f;         // longer map names wrap to two lines
        const float Slack = 2f;

        static TextGenerator gen;

        /// <summary>The pixel size UiKit.AddText gives a label of this base size (or the 16 sp floor when it is not scaled).</summary>
        public static int FontSizeFor(int baseSize, bool scaled)
        {
            float s = Mathf.Max(baseSize, UiKit.MinBodyTextPx);
            return Mathf.RoundToInt(scaled ? s * UiKit.TextScale : s);
        }

        static Font FontFor(int baseSize, bool display)
        {
            return display || baseSize >= UiKit.DisplayFromSize ? UiKit.DisplayFont : UiKit.DefaultFont;
        }

        /// <summary>Extra room each side for the text outline and the bold/italic overhang of the glyphs.</summary>
        public static float Overhang(int fontSize)
        {
            return Mathf.Max(1.6f, fontSize * 0.024f) + fontSize * 0.08f;
        }

        static TextGenerationSettings Settings(Font font, int fontSize, HorizontalWrapMode h, Vector2 extents)
        {
            return new TextGenerationSettings
            {
                font = font,
                fontSize = fontSize,
                fontStyle = FontStyle.Normal,
                color = Color.white,
                scaleFactor = 1f,
                lineSpacing = 1f,
                richText = false,
                textAnchor = TextAnchor.MiddleCenter,
                alignByGeometry = false,
                resizeTextForBestFit = false,
                updateBounds = false,
                horizontalOverflow = h,
                verticalOverflow = VerticalWrapMode.Overflow,
                generationExtents = extents,
                pivot = new Vector2(0.5f, 0.5f),
                generateOutOfBounds = false,
            };
        }

        /// <summary>Measures a label the way a Text built by UiKit.AddText would show it: one line, or wrapped at <paramref name="maxTextWidth"/>.</summary>
        public static void Measure(string label, int baseSize, bool display, bool scaled, float maxTextWidth, out float textW, out float textH, out bool wrapped)
        {
            Font font = FontFor(baseSize, display);
            int fs = FontSizeFor(baseSize, scaled);
            if (gen == null) gen = new TextGenerator();
            if (string.IsNullOrEmpty(label)) label = " ";
            font.RequestCharactersInTexture(label, fs, FontStyle.Normal);
            float w = gen.GetPreferredWidth(label, Settings(font, fs, HorizontalWrapMode.Overflow, Vector2.zero));
            wrapped = w > maxTextWidth;
            TextGenerationSettings s = wrapped ? Settings(font, fs, HorizontalWrapMode.Wrap, new Vector2(maxTextWidth, 0f)) : Settings(font, fs, HorizontalWrapMode.Overflow, Vector2.zero);
            textH = Mathf.Ceil(gen.GetPreferredHeight(label, s));
            textW = Mathf.Ceil(wrapped ? maxTextWidth : w);
        }

        /// <summary>Width of the widest single word of a label (plus a little slack). A wrap width below this would split a word.</summary>
        public static float WidestWord(string label, int baseSize, bool display, bool scaled)
        {
            Font font = FontFor(baseSize, display);
            int fs = FontSizeFor(baseSize, scaled);
            if (gen == null) gen = new TextGenerator();
            float widest = 0f;
            foreach (string word in (label ?? "").Split(' '))
            {
                if (word.Length == 0) continue;
                font.RequestCharactersInTexture(word, fs, FontStyle.Normal);
                widest = Mathf.Max(widest, gen.GetPreferredWidth(word, Settings(font, fs, HorizontalWrapMode.Overflow, Vector2.zero)));
            }
            return Mathf.Ceil(widest) + 4f;
        }

        /// <summary>Size of the backing that <see cref="Build"/> makes for a label (for planners that must know it before the tag exists).</summary>
        public static Vector2 BackingSize(string label, int baseSize, bool display, bool scaled, float maxTextWidth, float padX = PadX, float padY = PadY)
        {
            float tw, th;
            bool wrapped;
            Measure(label, baseSize, display, scaled, maxTextWidth, out tw, out th, out wrapped);
            float oh = Overhang(FontSizeFor(baseSize, scaled));
            return new Vector2(Mathf.Max(56f, Mathf.Ceil(tw + 2f * (padX + oh) + 2f * Slack)), Mathf.Max(56f, Mathf.Ceil(th + 2f * (padY + oh))));
        }

        /// <summary>
        /// A solid rounded backing sized to its text, with the label centred on it. Returns the backing (its sizeDelta is the final size;
        /// place it with UiKit.Place using that size). The label child is named "Name". <paramref name="scaled"/> false keeps the text at the 16 sp floor.
        /// </summary>
        public static Image Build(Transform parent, string name, string label, int baseSize, Color textColor, Color fill, float maxTextWidth, out Text text,
            bool display = false, bool scaled = true, float padX = PadX, float padY = PadY)
        {
            float tw, th;
            bool wrapped;
            Measure(label, baseSize, display, scaled, maxTextWidth, out tw, out th, out wrapped);
            int fs = FontSizeFor(baseSize, scaled);
            float oh = Overhang(fs);
            fill.a = Mathf.Max(fill.a, MinFillAlpha);
            Image back = UiKit.AddPanel(parent, name, fill);
            back.raycastTarget = false;
            text = UiKit.AddText(back.transform, "Name", label, baseSize, textColor, TextAnchor.MiddleCenter, display);
            if (text.fontSize != fs)
            {
                text.fontSize = fs;
                var outline = text.GetComponent<Outline>();
                if (outline != null) { float d = Mathf.Max(1.6f, fs * 0.024f); outline.effectDistance = new Vector2(d, -d); }
            }
            text.horizontalOverflow = wrapped ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(tw + 2f * oh + 2f * Slack, th + 2f * oh);
            back.rectTransform.sizeDelta = new Vector2(Mathf.Max(56f, Mathf.Ceil(tw + 2f * (padX + oh) + 2f * Slack)), Mathf.Max(56f, Mathf.Ceil(th + 2f * (padY + oh))));
            return back;
        }

        /// <summary>Shifts the (already placed) tag inward when it would reach the left or right edge of the container.</summary>
        public static TagKeepInside KeepInside(RectTransform tag, RectTransform container, float margin = EdgeMargin)
        {
            var k = tag.gameObject.AddComponent<TagKeepInside>();
            k.Container = container;
            k.Margin = margin;
            k.Capture();
            return k;
        }
    }
}
