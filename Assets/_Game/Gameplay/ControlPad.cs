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
            Sprite plate = art != null ? ArtCatalog.Pick(art.Buttons, 2) : null;   // silver plate: neutral under any region art
            float size = 220f, gap = 36f, margin = 24f, bottom = 40f;
            float jumpSize = 280f;
            // Move pair on the near side of the thumb that is NOT jumping.
            float moveAnchor = leftHanded ? 1f : 0f;
            float jumpAnchor = leftHanded ? 0f : 1f;
            float dir = leftHanded ? -1f : 1f;

            pad.Left = Make(parent, "PadLeft", "<", "Move left", plate, new Vector2(moveAnchor, 0f), new Vector2(dir * margin, bottom), size, 120);
            pad.Right = Make(parent, "PadRight", ">", "Move right", plate, new Vector2(moveAnchor, 0f), new Vector2(dir * (margin + size + gap), bottom), size, 120);
            pad.Jump = Make(parent, "PadJump", "JUMP", "Jump", art != null ? ArtCatalog.Pick(art.Buttons, 1) : null, new Vector2(jumpAnchor, 0f), new Vector2(-dir * margin, bottom + 20f), jumpSize, 64);
            return pad;
        }

        static HoldButton Make(RectTransform parent, string name, string label, string a11y, Sprite sprite, Vector2 anchor, Vector2 pos, float size, int font)
        {
            Button b = UiKit.AddButton(parent, name, label, sprite, new Color(1f, 1f, 1f, 0.78f), new Vector2(size, size), null, font);
            UiKit.Place((RectTransform)b.transform, anchor, pos, new Vector2(size, size));
            b.GetComponent<A11yLabel>().Text = a11y;
            b.GetComponent<Image>().raycastTarget = true;
            return b.gameObject.AddComponent<HoldButton>();
        }
    }
}
