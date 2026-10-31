using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Held / pressed state of one on-screen control (Left, Right or Jump).</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held;
        public bool PressedEdge;   // set on pointer down, consumed by the input reader

        public void OnPointerDown(PointerEventData e) { Held = true; PressedEdge = true; Haptics.Pulse(); }
        public void OnPointerUp(PointerEventData e) { Held = false; }
        public void OnPointerExit(PointerEventData e) { Held = false; }
        void OnDisable() { Held = false; PressedEdge = false; }
    }

    /// <summary>
    /// Always-visible touch controls: Left and Right (hold) on one side, Jump (tap) on the other. The left-handed setting swaps the sides.
    /// Buttons are large (CUX-4), semi-transparent so the play field stays visible, and live inside the safe area.
    /// </summary>
    public sealed class ControlPad
    {
        public HoldButton Left, Right, Jump;

        public static ControlPad Build(RectTransform parent, ArtCatalog art, bool leftHanded)
        {
            var pad = new ControlPad();
            Sprite plate = ToyKit.Button(2);   // chunky blue plate with a white chevron: icon-first controls (docs/16 section G)
            float jumpSize = 280f;
            float size = jumpSize, gap = 24f, margin = 24f, bottom = 40f;   // the arrows are exactly as TALL as the jump button and share its baseline (owner list 25)
            float arrowW = 230f;   // narrower than tall so the row fits the narrowest safe width: 16 + 2 x 230 + 24 + gap + 280 + 24 = 828 of 886 (9:21 + notch)
            float moveMargin = 16f;
            // Move pair on the near side of the thumb that is NOT jumping.
            float moveAnchor = leftHanded ? 1f : 0f;
            float jumpAnchor = leftHanded ? 0f : 1f;
            float dir = leftHanded ? -1f : 1f;

            pad.Left = Make(parent, "PadLeft", "Move left", plate, Glyphs.Left, false, new Vector2(moveAnchor, 0f), new Vector2(dir * moveMargin, bottom + 20f), size, arrowW);
            pad.Right = Make(parent, "PadRight", "Move right", plate, Glyphs.Left, true, new Vector2(moveAnchor, 0f), new Vector2(dir * (moveMargin + arrowW + gap), bottom + 20f), size, arrowW);
            pad.Jump = Make(parent, "PadJump", "Jump", ToyKit.Button(1), Glyphs.Up, false, new Vector2(jumpAnchor, 0f), new Vector2(-dir * margin, bottom + 20f), jumpSize);
            return pad;
        }

        static HoldButton Make(RectTransform parent, string name, string a11y, Sprite sprite, Sprite glyph, bool flip, Vector2 anchor, Vector2 pos, float size, float width = -1f)
        {
            if (width <= 0f) width = size;
            Button b = UiKit.AddButton(parent, name, "", sprite, new Color(1f, 1f, 1f, 0.9f), new Vector2(width, size), null);
            ToyKit.AddGlyph(b.transform, glyph, 0.6f, flip);
            UiKit.Place((RectTransform)b.transform, anchor, pos, new Vector2(width, size));
            b.GetComponent<A11yLabel>().Text = a11y;
            b.GetComponent<Image>().raycastTarget = true;
            return b.gameObject.AddComponent<HoldButton>();
        }
    }
}
