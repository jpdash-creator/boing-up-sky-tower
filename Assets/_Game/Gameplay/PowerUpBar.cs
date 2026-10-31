using System;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The in-run power-up buttons (docs/15 FR-PWR-3, section 9.5; reworked 2026-10-31): ONE round button per owned type (up to the 7 types:
    /// Magnet, Shield, Wall Stick, Double Jump, Slow Time, Rocket, Flight), no loadout. They sit in a compact side block above the jump
    /// button on the jump side (mirrored by the left-handed option through the HUD placement): one column up to 3 types, two columns from 4,
    /// filled from the bottom right. A button is 150 px (about 57 dp) with 24 px (about 9 dp) between neighbours, so every target is at least
    /// 48 dp with at least 8 dp between targets, and the block ends 24 px above the jump button.
    /// Each button shows the owned count ("x12", "x1.2K" for big stocks) on a dark pill at the bottom; while the effect runs a second dark pill
    /// at the top shows the remaining seconds ("29s") and the ring around the button drains with them. A tap while it runs adds the full
    /// base time (stacking): the seconds jump up and the ring is refilled against the new total. States never rely on colour alone:
    /// ready = icon and count, active = seconds text and a draining ring, used up = a cross and a disabled look.
    /// Refresh allocates only when a displayed value changes (CUX-1).
    /// </summary>
    public sealed class PowerUpBar
    {
        public const float ButtonSize = 150f;
        public const float Gap = 28f;
        /// <summary>Distance of the lowest row above the bottom of the safe area: just above the jump button (60 + 280) plus a gap.</summary>
        public const float BottomOffset = 364f;
        /// <summary>Distance of the block from the side edge: the same as the jump button, so the right edges line up.</summary>
        public const float SideOffset = 24f;
        public const int MaxButtons = 7;

        sealed class Slot
        {
            public PowerUp Type;
            public Button Button;
            public Image Background;
            public Text Code, Count, Secs;
            public RectTransform SecsPill;
            public Image Ring;
            public A11yLabel Label;
            public int LastUnits = -1, LastSecs = -1, LastState = -1, LastFill = -1;
            public int PeakTicks;
        }

        const int Ready = 0, Active = 1, Spent = 2;

        readonly Slot[] slots = new Slot[MaxButtons];
        RunSession session;
        public int ButtonCount { get; private set; }

        /// <summary>Called with the PowerUp type id when a button is pressed (the run controller latches it into the next tick's input).</summary>
        public Action<int> OnPress;

        /// <summary>Columns of the block for a number of buttons: one up to 3, two from 4.</summary>
        public static int ColumnsFor(int buttons)
        {
            return buttons <= 3 ? 1 : 2;
        }

        /// <summary>Position of button k (0 = bottom right) as an offset from the bottom corner of the jump side, before the left-handed mirror.</summary>
        public static Vector2 PositionFor(int index, int buttons)
        {
            int cols = ColumnsFor(buttons);
            int col = index % cols, row = index / cols;
            return new Vector2(-(SideOffset + col * (ButtonSize + Gap)), BottomOffset + row * (ButtonSize + Gap));
        }

        /// <summary>
        /// Builds one button for every power-up type the player owned when the run started. <paramref name="place"/> positions a button the
        /// same way the HUD places everything else (it applies the left-handed mirror).
        /// </summary>
        public void Build(RectTransform root, RunSession run, Action<RectTransform, Vector2, Vector2, Vector2> place)
        {
            session = run;
            int owned = 0;
            for (int i = (int)PowerUp.Flight; i <= (int)PowerUp.SlowTime; i++) if (run.OwnedAtStart((PowerUp)i)) owned++;
            int visible = 0;
            // the cheapest/most used first (nearest to the thumb): Magnet, Shield, Wall Stick, Double Jump, Slow Time, Rocket, Flight
            PowerUp[] order = { PowerUp.Magnet, PowerUp.Shield, PowerUp.WallStick, PowerUp.DoubleJump, PowerUp.SlowTime, PowerUp.Rocket, PowerUp.Flight };
            for (int k = 0; k < order.Length; k++)
            {
                PowerUp t = order[k];
                if (!run.OwnedAtStart(t)) continue;
                PowerUp type = t;
                Slot s = new Slot { Type = t };
                s.Button = UiKit.AddButton(root, "Power" + t, "", Theme.RoundSprite, Color.white, new Vector2(ButtonSize, ButtonSize), () => { if (OnPress != null) OnPress((int)type); }, 38, false);
                s.Background = s.Button.GetComponent<Image>();
                s.Background.type = Image.Type.Sliced;
                s.Background.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / (ButtonSize * 0.5f));
                s.Label = s.Button.GetComponent<A11yLabel>();
                RectTransform rt = (RectTransform)s.Button.transform;
                place(rt, new Vector2(1f, 0f), PositionFor(visible, owned), new Vector2(ButtonSize, ButtonSize));

                s.Code = UiKit.AddText(rt, "Code", PowerUpUi.Code(t), 38, Theme.Ink, TextAnchor.MiddleCenter, true);
                s.Code.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline o1 = s.Code.GetComponent<Outline>(); if (o1 != null) o1.enabled = false;
                UiKit.Place((RectTransform)s.Code.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(ButtonSize - 12f, 50f));

                // count: a dark pill at the bottom (readable on any art, CUX-5)
                Image countPill = UiKit.AddImage(rt, "CountBacking", Theme.RoundSprite, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.88f));
                countPill.type = Image.Type.Sliced;
                countPill.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 24f);
                UiKit.Place(countPill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(ButtonSize * 0.84f, 48f));
                s.Count = UiKit.AddText(countPill.transform, "Count", "", 36, Color.white, TextAnchor.MiddleCenter, true);
                s.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline o2 = s.Count.GetComponent<Outline>(); if (o2 != null) o2.enabled = false;
                UiKit.Stretch((RectTransform)s.Count.transform);

                // remaining seconds: a second pill at the top, only while the effect runs
                Image secsPill = UiKit.AddImage(rt, "SecsBacking", Theme.RoundSprite, new Color(0.98f, 0.8f, 0.2f, 0.96f));
                secsPill.type = Image.Type.Sliced;
                secsPill.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 24f);
                UiKit.Place(secsPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(ButtonSize * 0.84f, 48f));
                s.SecsPill = secsPill.rectTransform;
                s.Secs = UiKit.AddText(secsPill.transform, "Secs", "", 36, Theme.Ink, TextAnchor.MiddleCenter, true);
                s.Secs.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline o3 = s.Secs.GetComponent<Outline>(); if (o3 != null) o3.enabled = false;
                UiKit.Stretch((RectTransform)s.Secs.transform);
                secsPill.gameObject.SetActive(false);

                Sprite iconArt = PowerUpUi.IconSprite(t);
                if (iconArt != null)
                {
                    // Real art: the glossy icon is the button face; the code text is dropped.
                    s.Background.type = Image.Type.Simple;
                    s.Background.sprite = iconArt;
                    s.Background.preserveAspect = true;
                    s.Code.gameObject.SetActive(false);
                }

                // The timer is a ring AROUND the button (nothing covers the icon): a dim full ring behind, and a radial fill that drains clockwise.
                Image ringBack = UiKit.AddImage(rt, "RingBack", Glyphs.Ring, new Color(0.09f, 0.11f, 0.24f, 0.45f));
                RingStretch(ringBack.rectTransform);
                Image ring = UiKit.AddImage(rt, "Ring", Glyphs.Ring, Color.white);
                RingStretch(ring.rectTransform);
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = true;
                ring.fillAmount = 1f;
                s.Ring = ring;

                slots[visible] = s;
                visible++;
            }
            ButtonCount = visible;
        }

        static void RingStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-6f, -6f);
            rt.offsetMax = new Vector2(6f, 6f);
        }

        /// <summary>"x7", "x999", "x1.2K", "x3.4M": short enough for the pill, never clipped. The spoken label carries the exact number.</summary>
        public static string FormatCount(int n)
        {
            if (n < 1000) return "x" + n;
            if (n < 1000000) return "x" + (n / 1000) + "." + ((n % 1000) / 100) + "K";
            return "x" + (n / 1000000) + "." + ((n % 1000000) / 100000) + "M";
        }

        /// <summary>Updates the buttons from the simulation state. Call once per frame.</summary>
        public void Refresh(in SimState st, SimConfig cfg)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Slot s = slots[i];
                if (s == null) continue;
                int units = session != null ? session.PowerStock(s.Type) : 0;
                int ticks = PowerUps.ActiveTicks(in st, s.Type);
                int total = PowerUps.TotalTicks(cfg, s.Type);
                int state = ticks > 0 ? Active : (units > 0 ? Ready : Spent);
                int secs = ticks > 0 ? (int)Math.Min(((long)ticks + SimConfig.TicksPerSecond - 1) / SimConfig.TicksPerSecond, 999999L) : 0;
                if (ticks > s.PeakTicks) s.PeakTicks = Math.Max(ticks, total);   // a stacking tap raises the ring's full mark
                if (ticks <= 0) s.PeakTicks = 0;

                if (state != s.LastState || units != s.LastUnits || secs != s.LastSecs)
                {
                    bool stateChanged = state != s.LastState;
                    s.LastState = state;
                    s.LastUnits = units;
                    s.LastSecs = secs;
                    if (state == Spent) UiKit.SetText(s.Count, "X");   // used up: a cross, not just a grey button
                    else UiKit.SetText(s.Count, FormatCount(units));
                    if (state == Active) UiKit.SetText(s.Secs, secs + "s");
                    if (stateChanged) s.SecsPill.gameObject.SetActive(state == Active);
                    s.Background.color = state == Spent ? new Color(0.75f, 0.77f, 0.84f, 0.7f) : (state == Active ? new Color(1f, 0.93f, 0.6f, 1f) : new Color(1f, 1f, 1f, 0.95f));
                    s.Button.interactable = units > 0;   // an active button stays tappable while units are left: a tap stacks more time
                    string spoken = PowerUpUi.Spoken(s.Type, units) + (state == Active ? ", active, " + secs + " seconds left, tap to add more time" : (state == Spent ? ", used up" : ""));
                    if (s.Label != null) s.Label.Text = spoken;
                }
                // the ring drains with the remaining time against the largest total seen (a full ring while ready, empty when used up)
                float frac = state == Active ? (s.PeakTicks > 0 ? Mathf.Clamp01(ticks / (float)s.PeakTicks) : 1f) : (state == Ready ? 1f : 0f);
                int fill64 = Mathf.RoundToInt(frac * 64f);
                if (fill64 != s.LastFill) { s.LastFill = fill64; s.Ring.fillAmount = fill64 / 64f; s.Ring.color = state == Active ? new Color(1f, 0.84f, 0.2f, 1f) : new Color(0.55f, 0.95f, 0.65f, 1f); }
            }
        }

        /// <summary>Shows or hides all buttons (hidden during the level intro so they never sit over its text).</summary>
        public void SetVisible(bool visible)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i] != null) slots[i].Button.gameObject.SetActive(visible);
            Visible = visible;
        }

        public bool Visible { get; private set; }

        /// <summary>Test and audit access: the root rectangle of the built button (0 = bottom right).</summary>
        public RectTransform ButtonRect(int index)
        {
            return index >= 0 && index < slots.Length && slots[index] != null ? (RectTransform)slots[index].Button.transform : null;
        }

        /// <summary>Test access: the power-up type of the built button.</summary>
        public PowerUp ButtonType(int index)
        {
            return index >= 0 && index < slots.Length && slots[index] != null ? slots[index].Type : PowerUp.None;
        }
    }
}
