using System;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The in-run power-up buttons (docs/15 FR-PWR-3, section 9.5): up to three round buttons in a column above the jump button, on the jump
    /// side (mirrored by the left-handed option through the HUD placement). Each shows its code, the units left (or the seconds left while it
    /// runs) and a bar that drains while the effect is active. States never rely on colour alone: ready = code and "xN", active = seconds and
    /// a draining bar, used up = a cross. Refresh allocates only when a displayed value changes (CUX-1).
    /// </summary>
    public sealed class PowerUpBar
    {
        public const float ButtonSize = 168f;
        public const float Gap = 24f;
        /// <summary>Distance of the lowest button above the bottom of the safe area: just above the jump button (40 + 20 + 280) plus a gap.</summary>
        public const float BottomOffset = 364f;

        sealed class Slot
        {
            public PowerUp Type;
            public Button Button;
            public Image Background;
            public Text Code, Count;
            public RectTransform Fill;
            public A11yLabel Label;
            public int LastUnits = -1, LastSecs = -1, LastState = -1, LastFill = -1;
        }

        const int Ready = 0, Active = 1, Spent = 2;

        readonly Slot[] slots = new Slot[PowerUps.SlotCount];
        public int ButtonCount { get; private set; }

        /// <summary>Called with the slot index when a button is pressed (the run controller latches it into the next tick's input).</summary>
        public Action<int> OnPress;

        /// <summary>
        /// Builds the buttons for the loadout of the session's start state. <paramref name="place"/> positions a button the same way the HUD
        /// places everything else (it applies the left-handed mirror).
        /// </summary>
        public void Build(RectTransform root, RunSession session, Action<RectTransform, Vector2, Vector2, Vector2> place)
        {
            int visible = 0;
            for (int i = 0; i < PowerUps.SlotCount; i++)
            {
                PowerUp t = session.State.SlotType(i);
                if (t == PowerUp.None) continue;
                int index = i;
                Slot s = new Slot { Type = t };
                s.Button = UiKit.AddButton(root, "Power" + (i + 1), "", Theme.RoundSprite, Color.white, new Vector2(ButtonSize, ButtonSize), () => { if (OnPress != null) OnPress(index); }, 42, false);
                s.Background = s.Button.GetComponent<Image>();
                s.Background.type = Image.Type.Sliced;
                s.Background.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / (ButtonSize * 0.5f));
                s.Label = s.Button.GetComponent<A11yLabel>();
                RectTransform rt = (RectTransform)s.Button.transform;
                place(rt, new Vector2(1f, 0f), new Vector2(-80f, BottomOffset + visible * (ButtonSize + Gap)), new Vector2(ButtonSize, ButtonSize));

                s.Code = UiKit.AddText(rt, "Code", PowerUpUi.Code(t), 42, Theme.Ink, TextAnchor.MiddleCenter, true);
                s.Code.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline o1 = s.Code.GetComponent<Outline>(); if (o1 != null) o1.enabled = false;
                UiKit.Place((RectTransform)s.Code.transform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(ButtonSize - 12f, 56f));

                s.Count = UiKit.AddText(rt, "Count", "", 42, Theme.Ink, TextAnchor.MiddleCenter, true);
                s.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline o2 = s.Count.GetComponent<Outline>(); if (o2 != null) o2.enabled = false;
                UiKit.Place((RectTransform)s.Count.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(ButtonSize - 12f, 56f));

                Sprite iconArt = PowerUpUi.IconSprite(t);
                if (iconArt != null)
                {
                    // Real art: the glossy icon is the button face; the code text is dropped and the count sits on a dark pill (readable on any art, CUX-5).
                    s.Background.type = Image.Type.Simple;
                    s.Background.sprite = iconArt;
                    s.Background.preserveAspect = true;
                    s.Code.gameObject.SetActive(false);
                    Image pill = UiKit.AddImage(rt, "CountBacking", Theme.RoundSprite, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.86f));
                    pill.type = Image.Type.Sliced;
                    pill.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 28f);
                    UiKit.Place(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(ButtonSize * 0.62f, 56f));
                    s.Count.transform.SetParent(pill.transform, false);
                    s.Count.color = Color.white;
                    UiKit.Stretch((RectTransform)s.Count.transform);
                }

                Image track = UiKit.AddImage(rt, "Track", null, new Color(0.09f, 0.11f, 0.24f, 0.28f));
                UiKit.Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(ButtonSize - 40f, 14f));
                Image fill = UiKit.AddImage(track.transform, "Fill", null, Theme.Ink);
                s.Fill = fill.rectTransform;
                s.Fill.anchorMin = Vector2.zero;
                s.Fill.anchorMax = Vector2.one;
                s.Fill.offsetMin = Vector2.zero;
                s.Fill.offsetMax = Vector2.zero;

                slots[i] = s;
                visible++;
            }
            ButtonCount = visible;
        }

        /// <summary>Updates the buttons from the simulation state. Call once per frame.</summary>
        public void Refresh(in SimState st, SimConfig cfg)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Slot s = slots[i];
                if (s == null) continue;
                int units = st.SlotUnits(i);
                int ticks = PowerUps.ActiveTicks(in st, s.Type);
                int total = PowerUps.TotalTicks(cfg, s.Type);
                int state = ticks > 0 ? Active : (units > 0 ? Ready : Spent);
                int secs = ticks > 0 ? (ticks + SimConfig.TicksPerSecond - 1) / SimConfig.TicksPerSecond : 0;
                if (s.Type == PowerUp.DoubleJump && ticks > 0) secs = st.DoubleJumps;   // shows the air jumps left, not seconds

                if (state != s.LastState || units != s.LastUnits || secs != s.LastSecs)
                {
                    s.LastState = state;
                    s.LastUnits = units;
                    s.LastSecs = secs;
                    if (state == Active) UiKit.SetText(s.Count, s.Type == PowerUp.DoubleJump ? "J" + secs : secs + "s");
                    else if (state == Ready) UiKit.SetText(s.Count, "x" + units);
                    else UiKit.SetText(s.Count, "X");   // used up: a cross, not just a grey button
                    s.Background.color = state == Ready ? new Color(1f, 1f, 1f, 0.92f) : (state == Active ? new Color(1f, 0.86f, 0.35f, 0.95f) : new Color(0.75f, 0.77f, 0.84f, 0.7f));
                    s.Button.interactable = state != Spent;   // an active button stays full colour; pressing it again does nothing in the sim
                    string spoken = PowerUpUi.Spoken(s.Type, units) + (state == Active ? ", active" : (state == Spent ? ", used up" : ""));
                    if (s.Label != null) s.Label.Text = spoken;
                }
                // the bar drains with the remaining time (a full bar while ready, empty when used up)
                float frac = state == Active ? (total > 0 ? Mathf.Clamp01(ticks / (float)total) : 1f) : (state == Ready ? 1f : 0f);
                int fill64 = Mathf.RoundToInt(frac * 64f);
                if (fill64 != s.LastFill) { s.LastFill = fill64; s.Fill.anchorMax = new Vector2(fill64 / 64f, 1f); }
            }
        }

        /// <summary>Shows or hides all buttons (hidden during the level intro so they never sit over its text).</summary>
        public void SetVisible(bool visible)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i] != null) slots[i].Button.gameObject.SetActive(visible);
            Visible = visible;
        }

        public bool Visible { get; private set; }

        /// <summary>Test and audit access: the root rectangles of the built buttons.</summary>
        public RectTransform ButtonRect(int slot)
        {
            return slot >= 0 && slot < slots.Length && slots[slot] != null ? (RectTransform)slots[slot].Button.transform : null;
        }
    }
}
