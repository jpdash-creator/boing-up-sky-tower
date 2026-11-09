using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
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

        /// <summary>Pad geometry (reference px). The pads float (50 percent transparent) over the play field, which runs to the screen bottom (no bottom strip, owner 2026-11-02); RunLayout.BottomPadRef = PadBottom + JumpSize.</summary>
        public const float JumpSize = 280f, ArrowW = 230f, MoveMargin = 16f, JumpMargin = 24f, PadGap = 24f, PadBottom = 60f;   // tall arrows again (owner 2026-11-02), as before the strips
        /// <summary>Width the move pair takes from its screen edge, and the jump pad from the other edge.</summary>
        public const float MoveSpan = MoveMargin + 2f * ArrowW + PadGap, JumpSpan = JumpMargin + JumpSize;

        public static ControlPad Build(RectTransform parent, ArtCatalog art, bool leftHanded)
        {
            var pad = new ControlPad();
            Sprite plate = ToyKit.Button(2);   // chunky blue plate with a white chevron: icon-first controls (docs/16 section G)
            float jumpSize = Mathf.Max(JumpSize, UiKit.MinTouchRef);
            float size = jumpSize, gap = PadGap, margin = JumpMargin, bottom = PadBottom - 20f;   // + the 20 added at the Place calls   // the arrows are exactly as TALL as the jump button and share its baseline (owner list 25)
            float arrowW = ArrowW;   // narrower than tall so the row fits the narrowest safe width: 16 + 2 x 200 + 24 + chips + 24 + 230
            float moveMargin = MoveMargin;
            // Move pair on the near side of the thumb that is NOT jumping.
            float moveAnchor = leftHanded ? 1f : 0f;
            float jumpAnchor = leftHanded ? 0f : 1f;
            float dir = leftHanded ? -1f : 1f;

            pad.Left = Make(parent, "PadLeft", "Move left", plate, Glyphs.Left, false, new Vector2(moveAnchor, 0f), new Vector2(dir * moveMargin, bottom + 20f), size, arrowW);
            pad.Right = Make(parent, "PadRight", "Move right", plate, Glyphs.Left, true, new Vector2(moveAnchor, 0f), new Vector2(dir * (moveMargin + arrowW + gap), bottom + 20f), size, arrowW);
            pad.Jump = Make(parent, "PadJump", "Jump", ToyKit.Button(1), Glyphs.Up, false, new Vector2(jumpAnchor, 0f), new Vector2(-dir * margin, bottom + 20f), jumpSize);
            return pad;
        }

        /// <summary>Run-button opacity: 0.75 = 50 percent more visible than the old 0.5 (owner 2026-11-07: the see-through pads were easy to miss). Shared by the pads, pause, bought and hero power buttons.</summary>
        public const float ButtonAlpha = 0.75f;

        /// <summary>Extra touch area around every pad button (reference px, each side): a thumb that drifts a little off the plate keeps the move/jump held. Kept under half the 24 px gap so neighbours never overlap.</summary>
        public const float HitSlop = 11f;

        static HoldButton Make(RectTransform parent, string name, string a11y, Sprite sprite, Sprite glyph, bool flip, Vector2 anchor, Vector2 pos, float size, float width = -1f)
        {
            if (width <= 0f) width = size;
            Button b = UiKit.AddButton(parent, name, "", sprite, new Color(1f, 1f, 1f, 0.9f), new Vector2(width, size), null);
            ToyKit.AddGlyph(b.transform, glyph, 0.6f, flip);
            UiKit.Place((RectTransform)b.transform, anchor, pos, new Vector2(width, size));
            b.GetComponent<A11yLabel>().Text = a11y;
            Image plateImage = b.GetComponent<Image>();
            plateImage.raycastTarget = true;
            plateImage.raycastPadding = new Vector4(-HitSlop, -HitSlop, -HitSlop, -HitSlop);   // bigger touch area than the drawn plate
            b.gameObject.AddComponent<CanvasGroup>().alpha = ButtonAlpha;   // 25 percent see-through (owner 2026-11-07, was 50): the play field is still visible, the pads are not missed
            return b.gameObject.AddComponent<HoldButton>();
        }
    }
}
