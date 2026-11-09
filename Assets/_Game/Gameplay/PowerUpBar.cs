using System;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The in-run power buttons (docs/15 FR-PWR-3, section 9.5; merged with the hero powers 2026-11-09, owner Bug List 6): ONE round button per power TYPE that the player owns
    /// (bought) OR that the chosen hero has as a FREE hero power. Hero powers are not a separate bar any more: a free hero power sits on the button of its own type, next to the bought
    /// count of that type. The buttons are split into two groups by type (<see cref="PowerGroups"/>): LEFT group = Flight, Rocket, Double Jump; RIGHT group = Magnet, Shield, Slow Time,
    /// Wall Stick. Each group is ONE column floating over the field on its own side edge (3 and 4 buttons at most); the left-handed option mirrors the whole HUD, so the groups swap sides
    /// with it. A button is 150 px (about 57 dp) with 28 px (about 10 dp) between neighbours, so every target is at least 48 dp with at least 8 dp between targets, and the lowest button
    /// starts above the pads (<see cref="BottomOffset"/>).
    /// Each button shows the BOUGHT count ("x12", "x1.2K" for big stocks, "X" when used up) on a dark pill at the bottom (only for types the player bought). A FREE hero power is shown by the
    /// TOP pill with the word "FREE" (green) while it is ready, or the cooldown seconds (slate) while it cools down; while the effect runs the top pill shows the remaining seconds (gold) and
    /// the ring around the button drains with them.
    /// USING ORDER (RunSession.TakePowerUnits): FREE first, then bought. A tap uses the free hero power when it is ready and its effect is not running (no unit is taken, the rarity
    /// cooldown starts); otherwise it uses one bought unit (and stacks onto a running effect). Free or bought, the effect is identical (same strength and duration).
    /// States never rely on colour alone: ready = icon and count, FREE = the word, active = seconds text and a draining ring, used up = a cross and a disabled look.
    /// Refresh allocates only when a displayed value changes (CUX-1).
    /// </summary>
    public sealed class PowerUpBar
    {
        public const float ButtonSize = 150f;
        public const float Gap = 28f;
        /// <summary>Distance of the lowest row above the bottom of the safe area: just above the pads (20 + 230 jump button + 20, RunLayout.BottomPadRef) plus a gap, so the buttons never overlap a pad. These buttons float over the play field at the sides.</summary>
        public const float BottomOffset = 364f;
        /// <summary>Distance of a group from its side edge: the same as the jump button, so the outer edges line up.</summary>
        public const float SideOffset = 24f;
        public const int MaxButtons = PowerUps.TypeCount;

        sealed class Slot
        {
            public PowerUp Type;
            public bool LeftGroup;
            public bool Bought;        // the player owned this type when the run started (the bought count pill is shown)
            public int HeroIdx = -1;   // index in RunSession.Hero when the hero has this power for free, else -1
            public Button Button;
            public Image Background;
            public Text Code, Count, Secs;
            public RectTransform SecsPill;
            public Image SecsPillImage;
            public Image Ring;
            public A11yLabel Label;
            public int LastUnits = -1, LastSecs = -1, LastState = -1, LastRing = -1, LastFree = -1, LastCool = -1, LastInter = -1;
            public int PeakTicks;
        }

        const int Ready = 0, Active = 1, Spent = 2;
        const int FreeNone = 0, FreeReady = 1, FreeCool = 2;

        readonly Slot[] slots = new Slot[MaxButtons];
        RunSession session;
        public int ButtonCount { get; private set; }
        /// <summary>Buttons in the left group (Flight, Rocket, Double Jump) and in the right group (Magnet, Shield, Slow Time, Wall Stick). The slots are the left group bottom up, then the right group bottom up.</summary>
        public int LeftCount { get; private set; }
        public int RightCount { get; private set; }

        /// <summary>Called with the PowerUp type id when a button is pressed (the run controller latches it into the next tick's input).</summary>
        public Action<int> OnPress;

        /// <summary>Position of button k of a group (0 = lowest) as an offset from the bottom corner of its side, before the left-handed mirror.</summary>
        public static Vector2 PositionFor(int index, bool leftGroup)
        {
            return new Vector2(leftGroup ? SideOffset : -SideOffset, BottomOffset + index * (ButtonSize + Gap));
        }

        /// <summary>Horizontal anchor of a group: 0 = left edge, 1 = right edge, before the left-handed mirror.</summary>
        public static float AnchorX(bool leftGroup)
        {
            return leftGroup ? 0f : 1f;
        }

        /// <summary>Distance of a group's inner edge from its side edge of the safe area (reference px), for the layout tests.</summary>
        public static float ReachFromEdge { get { return SideOffset + ButtonSize; } }

        /// <summary>
        /// Builds one button for every power type the player owned when the run started or that the hero has for free. <paramref name="place"/> positions a button the
        /// same way the HUD places everything else (it applies the left-handed mirror).
        /// </summary>
        public void Build(RectTransform root, RunSession run, Action<RectTransform, Vector2, Vector2, Vector2> place)
        {
            session = run;
            bool[] shown = new bool[PowerUps.MaxType + 1];
            for (int i = (int)PowerUp.Flight; i <= (int)PowerUp.SlowTime; i++)
            {
                PowerUp t = (PowerUp)i;
                shown[i] = run.OwnedAtStart(t) || (run.Hero != null && run.Hero.IndexOf(t) >= 0);
            }
            PowerUp[] buf = new PowerUp[MaxButtons];
            int built = 0;
            for (int g = 0; g < 2; g++)
            {
                bool leftGroup = g == 0;
                int n = PowerGroups.Collect(leftGroup, shown, buf);
                for (int k = 0; k < n && built < MaxButtons; k++)
                {
                    slots[built] = BuildSlot(root, run, buf[k], leftGroup, k, place);
                    built++;
                }
                if (leftGroup) LeftCount = n; else RightCount = n;
            }
            ButtonCount = built;
        }

        Slot BuildSlot(RectTransform root, RunSession run, PowerUp t, bool leftGroup, int indexInGroup, Action<RectTransform, Vector2, Vector2, Vector2> place)
        {
            PowerUp type = t;
            Slot s = new Slot { Type = t, LeftGroup = leftGroup, Bought = run.OwnedAtStart(t), HeroIdx = run.Hero != null ? run.Hero.IndexOf(t) : -1 };
            s.Button = UiKit.AddButton(root, "Power" + t, "", Theme.RoundSprite, Color.white, new Vector2(ButtonSize, ButtonSize), () => { if (OnPress != null) OnPress((int)type); }, 38, false);
            s.Button.gameObject.AddComponent<CanvasGroup>().alpha = ControlPad.ButtonAlpha;
            s.Background = s.Button.GetComponent<Image>();
            s.Background.type = Image.Type.Sliced;
            s.Background.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / (ButtonSize * 0.5f));
            s.Label = s.Button.GetComponent<A11yLabel>();
            RectTransform rt = (RectTransform)s.Button.transform;
            place(rt, new Vector2(AnchorX(leftGroup), 0f), PositionFor(indexInGroup, leftGroup), new Vector2(ButtonSize, ButtonSize));

            s.Code = UiKit.AddText(rt, "Code", PowerUpUi.Code(t), 38, Theme.Ink, TextAnchor.MiddleCenter, true);
            s.Code.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline o1 = s.Code.GetComponent<Outline>(); if (o1 != null) o1.enabled = false;
            UiKit.Place((RectTransform)s.Code.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(ButtonSize - 12f, 50f));

            // bought count: a dark pill at the bottom (readable on any art, CUX-5); only for types the player bought
            Image countPill = UiKit.AddImage(rt, "CountBacking", Theme.RoundSprite, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.88f));
            countPill.type = Image.Type.Sliced;
            countPill.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 24f);
            UiKit.Place(countPill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(ButtonSize * 0.84f, 48f));
            s.Count = UiKit.AddText(countPill.transform, "Count", "", 36, Color.white, TextAnchor.MiddleCenter, true);
            s.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline o2 = s.Count.GetComponent<Outline>(); if (o2 != null) o2.enabled = false;
            UiKit.Stretch((RectTransform)s.Count.transform);
            countPill.gameObject.SetActive(s.Bought);

            // top pill: remaining seconds while the effect runs (gold), else the FREE hero power ("FREE" green when ready, cooldown seconds slate while cooling down)
            Image secsPill = UiKit.AddImage(rt, "SecsBacking", Theme.RoundSprite, new Color(0.98f, 0.8f, 0.2f, 0.96f));
            secsPill.type = Image.Type.Sliced;
            secsPill.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 24f);
            UiKit.Place(secsPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(ButtonSize * 0.84f, 48f));
            s.SecsPill = secsPill.rectTransform;
            s.SecsPillImage = secsPill;
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
            return s;
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

        /// <summary>Updates the buttons from the simulation state and the hero cooldowns. Call once per frame.</summary>
        public void Refresh(in SimState st, SimConfig cfg)
        {
            HeroPowerCooldown hero = session != null ? session.Hero : null;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot s = slots[i];
                if (s == null) continue;
                int units = s.Bought && session != null ? session.PowerStock(s.Type) : 0;
                int ticks = PowerUps.ActiveTicks(in st, s.Type);
                int total = PowerUps.TotalTicks(cfg, s.Type);
                int free = FreeNone, cool = 0;
                if (s.HeroIdx >= 0 && hero != null)
                {
                    if (hero.IsReady(s.HeroIdx)) free = FreeReady;
                    else { free = FreeCool; cool = hero.RemainingSeconds(s.HeroIdx); }
                }
                int state = ticks > 0 ? Active : (units > 0 || free == FreeReady ? Ready : Spent);
                int secs = ticks > 0 ? (int)Math.Min(((long)ticks + SimConfig.TicksPerSecond - 1) / SimConfig.TicksPerSecond, 999999L) : 0;
                if (ticks > s.PeakTicks) s.PeakTicks = Math.Max(ticks, total);   // a stacking tap raises the ring's full mark
                if (ticks <= 0) s.PeakTicks = 0;
                // a bought unit works any time (an active button stays tappable while units are left: a tap stacks more time); the free hero power only when it is ready and not already running
                int inter = (units > 0 || (free == FreeReady && PowerUps.CanActivateHero(in st, s.Type))) ? 1 : 0;

                if (state != s.LastState || units != s.LastUnits || secs != s.LastSecs || free != s.LastFree || cool != s.LastCool)
                {
                    s.LastState = state;
                    s.LastUnits = units;
                    s.LastSecs = secs;
                    s.LastFree = free;
                    s.LastCool = cool;
                    if (s.Bought) UiKit.SetText(s.Count, units > 0 ? FormatCount(units) : "X");   // used up: a cross, not just a grey button
                    // top pill: running effect (gold seconds), else the free hero power (green FREE word, or slate cooldown seconds)
                    if (state == Active)
                    {
                        UiKit.SetText(s.Secs, secs + "s");
                        s.SecsPillImage.color = new Color(0.98f, 0.8f, 0.2f, 0.96f);
                    }
                    else if (free == FreeReady)
                    {
                        UiKit.SetText(s.Secs, "FREE");
                        s.SecsPillImage.color = new Color(0.55f, 0.92f, 0.6f, 0.98f);
                    }
                    else if (free == FreeCool)
                    {
                        UiKit.SetText(s.Secs, cool + "s");
                        s.SecsPillImage.color = new Color(0.72f, 0.77f, 0.92f, 0.96f);
                    }
                    s.SecsPill.gameObject.SetActive(state == Active || free != FreeNone);
                    s.Background.color = state == Spent ? new Color(0.75f, 0.77f, 0.84f, 0.7f) : (state == Active ? new Color(1f, 0.93f, 0.6f, 1f) : new Color(1f, 1f, 1f, 0.95f));
                    string spoken = Spoken(s.Type, s.Bought, units, free, cool);
                    if (state == Active) spoken += ", active, " + secs + " seconds left" + (units > 0 ? ", tap to add more time" : "");
                    else if (state == Spent && s.Bought && free == FreeNone) spoken += ", used up";
                    if (s.Label != null) s.Label.Text = spoken;
                }
                if (inter != s.LastInter) { s.LastInter = inter; s.Button.interactable = inter == 1; }

                // ring: drains with the remaining time of a running effect; refills while the only thing left is a cooling free hero power; otherwise full (ready) or empty (used up)
                int mode;
                float frac;
                if (state == Active) { mode = 0; frac = s.PeakTicks > 0 ? Mathf.Clamp01(ticks / (float)s.PeakTicks) : 1f; }
                else if (free == FreeCool && units <= 0) { mode = 1; frac = 1f - hero.Fraction(s.HeroIdx); }
                else if (state == Ready) { mode = 2; frac = 1f; }
                else { mode = 3; frac = 0f; }
                int fill64 = Mathf.RoundToInt(frac * 64f);
                int ringKey = mode * 100 + fill64;
                if (ringKey != s.LastRing)
                {
                    s.LastRing = ringKey;
                    s.Ring.fillAmount = fill64 / 64f;
                    s.Ring.color = mode == 0 ? new Color(1f, 0.84f, 0.2f, 1f) : (mode == 1 ? new Color(0.55f, 0.75f, 1f, 1f) : new Color(0.55f, 0.95f, 0.65f, 1f));
                }
            }
        }

        /// <summary>
        /// Spoken label. Bought part: "Magnet, 30 seconds, 2 left". Free hero part: ", free hero power, ready" or ", free hero power, cooling down, 22 seconds".
        /// A hero-only button says no count; a bought-only button says no hero part. <paramref name="free"/>: 0 none, 1 ready, 2 cooling down.
        /// </summary>
        public static string Spoken(PowerUp p, bool bought, int units, int free, int cool)
        {
            string s = bought ? PowerUpUi.Spoken(p, units) : PowerUpUi.Name(p);
            if (free == FreeReady) s += ", free hero power, ready";
            else if (free == FreeCool) s += ", free hero power, cooling down, " + cool + " seconds";
            return s;
        }

        /// <summary>Shows or hides all buttons (hidden during the level intro so they never sit over its text).</summary>
        public void SetVisible(bool visible)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i] != null) slots[i].Button.gameObject.SetActive(visible);
            Visible = visible;
        }

        public bool Visible { get; private set; }

        /// <summary>Test and audit access: the root rectangle of the built button (left group bottom up, then right group bottom up).</summary>
        public RectTransform ButtonRect(int index)
        {
            return index >= 0 && index < slots.Length && slots[index] != null ? (RectTransform)slots[index].Button.transform : null;
        }

        /// <summary>Test access: true when the built button sits in the left group (Flight, Rocket, Double Jump).</summary>
        public bool ButtonIsLeft(int index)
        {
            return index >= 0 && index < slots.Length && slots[index] != null && slots[index].LeftGroup;
        }

        /// <summary>Test access: true when the built button carries a free hero power.</summary>
        public bool ButtonHasFree(int index)
        {
            return index >= 0 && index < slots.Length && slots[index] != null && slots[index].HeroIdx >= 0;
        }

        /// <summary>Test access: true when the built button shows a bought count (the player owned the type when the run started).</summary>
        public bool ButtonHasBought(int index)
        {
            return index >= 0 && index < slots.Length && slots[index] != null && slots[index].Bought;
        }

        /// <summary>Test access: the text of the top pill (the FREE tag, the cooldown seconds or the running seconds); empty when the pill is hidden.</summary>
        public string ButtonTopText(int index)
        {
            return index >= 0 && index < slots.Length && slots[index] != null && slots[index].SecsPill.gameObject.activeSelf ? slots[index].Secs.text : "";
        }

        /// <summary>Test access: the power-up type of the built button.</summary>
        public PowerUp ButtonType(int index)
        {
            return index >= 0 && index < slots.Length && slots[index] != null ? slots[index].Type : PowerUp.None;
        }
    }
}
