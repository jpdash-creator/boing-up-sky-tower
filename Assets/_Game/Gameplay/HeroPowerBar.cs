using System;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The HERO power buttons in a run (owner 2026-11-02, docs/34 "Hero powers in a run"): one ROUND button for every power of the chosen hero (0 for Common, 1 to 4 from
    /// Uncommon to Legendary). They float over the field in a SECOND column next to the bought power-up buttons (<see cref="PowerUpBar"/>), at the same height above the pad
    /// (<see cref="PowerUpBar.BottomOffset"/>), growing upward. Which side: see <see cref="OnJumpSide"/>. A button is 150 px (about 57 dp) with 28 px (about 10 dp) gaps (CUX-4), drawn at
    /// <see cref="ControlPad.ButtonAlpha"/> like every run button, mirrored by the left-handed option through the HUD placement.
    /// Free and READY at the start of the run; a tap uses it (same effect as the bought power-up of that type for 60 percent of the time) and starts its own cooldown.
    /// States never rely on colour alone: ready = icon plus a "FREE" pill and a full ring, cooling down = a dimmed icon, the seconds left in the pill and a ring that refills
    /// clockwise, effect running = the remaining seconds in a gold pill at the top. Refresh allocates only when a displayed value changes (CUX-1).
    /// </summary>
    public sealed class HeroPowerBar
    {
        public const float ButtonSize = PowerUpBar.ButtonSize;
        public const float Gap = PowerUpBar.Gap;
        public const int MaxButtons = HeroPowerCooldown.MaxPowers;

        /// <summary>
        /// False (default): the column sits on the MOVE-pad side, above the left/right pad. True: it sits on the jump side just inside the bought block.
        /// Why the default is false: bought block (2 columns, 352 px) plus a hero column reaches 530 px from the edge, which is more than half of the narrowest canvas (about 942 px at 9:21)
        /// and collides with the centred combo and speed-up banners (BannerW 380, owner 2026-11-02) at every aspect; on the move side nothing overlaps (CUX-3).
        /// </summary>
        public const bool OnJumpSide = false;

        sealed class Slot
        {
            public PowerUp Type;
            public Button Button;
            public Image Background;
            public Text Code, Info, Secs;
            public RectTransform SecsPill;
            public Image Ring;
            public A11yLabel Label;
            public int LastState = -1, LastCool = -1, LastSecs = -1, LastFill = -1;
        }

        const int Ready = 0, Cooling = 1, Running = 2;

        readonly Slot[] slots = new Slot[MaxButtons];
        public int ButtonCount { get; private set; }
        public bool Visible { get; private set; }

        /// <summary>Called with the PowerUp type id when a hero button is pressed (the run controller latches it into the next tick's input).</summary>
        public Action<int> OnPress;

        /// <summary>Columns taken by the bought power-up block for a number of owned types (0 when none are owned).</summary>
        public static int BoughtColumns(int boughtButtons)
        {
            return boughtButtons <= 0 ? 0 : PowerUpBar.ColumnsFor(boughtButtons);
        }

        /// <summary>Position of hero button k (0 = bottom) as an offset from the bottom corner of the jump side, before the left-handed mirror.</summary>
        public static Vector2 PositionFor(int index, int boughtButtons)
        {
            float x = OnJumpSide ? -(PowerUpBar.SideOffset + BoughtColumns(boughtButtons) * (ButtonSize + Gap)) : PowerUpBar.SideOffset;
            return new Vector2(x, PowerUpBar.BottomOffset + index * (ButtonSize + Gap));
        }

        /// <summary>Horizontal anchor of the buttons: 1 = right edge (jump side), 0 = left edge (move side), before the left-handed mirror.</summary>
        public static float AnchorX { get { return OnJumpSide ? 1f : 0f; } }

        /// <summary>Distance of the hero column's inner edge from the side edge of the safe area it sits on (reference px), for the layout tests.</summary>
        public static float ReachFromEdge(int boughtButtons)
        {
            return OnJumpSide ? PowerUpBar.SideOffset + (BoughtColumns(boughtButtons) + 1) * ButtonSize + BoughtColumns(boughtButtons) * Gap : PowerUpBar.SideOffset + ButtonSize;
        }

        /// <summary>Builds one button for every power of the hero of this run. <paramref name="boughtButtons"/> is the number of bought power-up buttons the HUD shows.</summary>
        public void Build(RectTransform root, RunSession run, int boughtButtons, Action<RectTransform, Vector2, Vector2, Vector2> place)
        {
            HeroPowerCooldown hero = run.Hero;
            if (hero == null) return;
            for (int k = 0; k < hero.Count && k < MaxButtons; k++)
            {
                PowerUp t = hero.Type(k);
                PowerUp type = t;
                Slot s = new Slot { Type = t };
                s.Button = UiKit.AddButton(root, "Hero" + t, "", Theme.RoundSprite, Color.white, new Vector2(ButtonSize, ButtonSize), () => { if (OnPress != null) OnPress((int)type); }, 38, false);
                s.Button.gameObject.AddComponent<CanvasGroup>().alpha = ControlPad.ButtonAlpha;
                s.Background = s.Button.GetComponent<Image>();
                s.Background.type = Image.Type.Sliced;
                s.Background.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / (ButtonSize * 0.5f));
                s.Label = s.Button.GetComponent<A11yLabel>();
                RectTransform rt = (RectTransform)s.Button.transform;
                place(rt, new Vector2(AnchorX, 0f), PositionFor(k, boughtButtons), new Vector2(ButtonSize, ButtonSize));

                s.Code = UiKit.AddText(rt, "Code", PowerUpUi.Code(t), 38, Theme.Ink, TextAnchor.MiddleCenter, true);
                s.Code.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline o1 = s.Code.GetComponent<Outline>(); if (o1 != null) o1.enabled = false;
                UiKit.Place((RectTransform)s.Code.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(ButtonSize - 12f, 50f));

                // bottom pill: "FREE" while ready, the seconds left while cooling down (readable on any art, CUX-5)
                Image infoPill = UiKit.AddImage(rt, "InfoBacking", Theme.RoundSprite, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.88f));
                infoPill.type = Image.Type.Sliced;
                infoPill.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 24f);
                UiKit.Place(infoPill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(ButtonSize * 0.84f, 48f));
                s.Info = UiKit.AddText(infoPill.transform, "Info", "", 36, Color.white, TextAnchor.MiddleCenter, true);
                s.Info.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline o2 = s.Info.GetComponent<Outline>(); if (o2 != null) o2.enabled = false;
                UiKit.Stretch((RectTransform)s.Info.transform);

                // top pill: remaining seconds of the running effect, only while it runs
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

                Sprite iconArt = PowerUpUi.IconSprite(t);   // the same icon as the bought power-up of the type
                if (iconArt != null)
                {
                    s.Background.type = Image.Type.Simple;
                    s.Background.sprite = iconArt;
                    s.Background.preserveAspect = true;
                    s.Code.gameObject.SetActive(false);
                }

                // The cooldown is a ring AROUND the button: a dim full ring behind and a radial fill that refills clockwise while cooling down (full = ready).
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

                slots[k] = s;
                ButtonCount = k + 1;
            }
        }

        static void RingStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-6f, -6f);
            rt.offsetMax = new Vector2(6f, 6f);
        }

        /// <summary>Updates the buttons from the cooldowns and the simulation state. Call once per frame.</summary>
        public void Refresh(in SimState st, RunSession run)
        {
            HeroPowerCooldown hero = run.Hero;
            if (hero == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot s = slots[i];
                if (s == null) continue;
                bool ready = hero.IsReady(i);
                int running = PowerUps.ActiveTicks(in st, s.Type);
                int state = !ready ? Cooling : (running > 0 ? Running : Ready);
                int cool = hero.RemainingSeconds(i);
                int secs = running > 0 ? (int)Math.Min(((long)running + SimConfig.TicksPerSecond - 1) / SimConfig.TicksPerSecond, 999999L) : 0;

                if (state != s.LastState || cool != s.LastCool || secs != s.LastSecs)
                {
                    bool stateChanged = state != s.LastState;
                    bool runChanged = (secs > 0) != (s.LastSecs > 0);
                    s.LastState = state;
                    s.LastCool = cool;
                    s.LastSecs = secs;
                    UiKit.SetText(s.Info, ready ? "FREE" : cool + "s");
                    if (secs > 0) UiKit.SetText(s.Secs, secs + "s");
                    if (runChanged || stateChanged) s.SecsPill.gameObject.SetActive(secs > 0);
                    s.Background.color = ready ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.62f, 0.64f, 0.74f, 0.8f);
                    s.Button.interactable = ready && PowerUps.CanActivateHero(in st, s.Type);
                    if (stateChanged && s.Label != null) s.Label.Text = Spoken(s.Type, ready);
                }
                // the ring refills while cooling down (full = ready)
                float frac = ready ? 1f : 1f - hero.Fraction(i);
                int fill64 = Mathf.RoundToInt(frac * 64f);
                if (fill64 != s.LastFill) { s.LastFill = fill64; s.Ring.fillAmount = fill64 / 64f; s.Ring.color = ready ? new Color(1f, 0.84f, 0.2f, 1f) : new Color(0.55f, 0.75f, 1f, 1f); }
            }
        }

        /// <summary>Spoken label: "Magnet, hero power, ready" or "Magnet, hero power, cooling down".</summary>
        public static string Spoken(PowerUp p, bool ready)
        {
            return PowerUpUi.Name(p) + ", hero power, " + (ready ? "ready, free to use" : "cooling down");
        }

        /// <summary>Shows or hides all buttons (hidden during the level intro so they never sit over its text).</summary>
        public void SetVisible(bool visible)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i] != null) slots[i].Button.gameObject.SetActive(visible);
            Visible = visible;
        }

        /// <summary>Test and audit access: the root rectangle of the built button (0 = bottom).</summary>
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
