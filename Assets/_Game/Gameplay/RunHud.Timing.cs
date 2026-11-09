using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The timed part of the Run HUD (doc 46 section 5), a third strip row under the gauge: an always-visible timer (the clock of the run, or the 3:00 countdown of a Time Attack),
    /// the best pace of the record beside it (floors for a Time Attack) and a live delta against the best run: faster or slower, refreshed every 10 floors from the best run's
    /// floor-by-time curve and, in endless, at every 100 floors against the best split of that stage. The Pace Ghost (Settings, default OFF) is a marker for the floor your best run
    /// had reached at this second: a diamond on the tower gauge in a level, a ghost floor pill in the empty second row of endless. Everything sits inside the reserved top strip, so
    /// it can never cover the player, a platform or a hazard. Strings come from <see cref="TimedText"/> and change only when the shown tenth, floor bucket or ghost step changes:
    /// no managed allocation per frame (CUX-1).
    /// </summary>
    public sealed partial class RunHud
    {
        Text timerText, bestText, deltaText, ghostText;
        Image deltaIcon;
        RectTransform ghostMarker;
        Graphic[] bestGraphics, deltaGraphics;
        int[] bestCurve = new int[0];
        bool hasBest, timeAttack, ghostOn, deltaShown;
        string timedRegion;
        int timedMode, timedTarget;
        int lastTenths = -1, lastDeltaBucket, lastSplitStage, lastGhostKey = -1;

        static readonly Color DeltaGood = new Color(0.62f, 1f, 0.72f, 1f), DeltaBad = new Color(1f, 0.76f, 0.52f, 1f);

        /// <summary>Test hook: the three timed pills and the ghost items by name ("HudTimes", "TimerPill", "BestPill", "DeltaPill", "GhostMarker", "GhostPill").</summary>
        public Text TimerText { get { return timerText; } }
        public Text DeltaText { get { return deltaText; } }
        public bool GhostActive { get { return ghostOn; } }

        /// <summary>A glass pill with a small icon at the left and a right-aligned value; <paramref name="flex"/> is its share of a layout row (ignored when it is placed by hand).</summary>
        Text TimedPill(Transform parent, string name, Sprite icon, float flex, Color color, float h, out Image iconImage)
        {
            Image pill = Theme.GlassPanel(parent, name, h * 0.25f);
            var le = pill.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 0f;
            le.minWidth = 0f;
            le.flexibleWidth = flex;
            le.minHeight = h;
            Image ic = UiKit.AddImage(pill.transform, "Icon", icon, Color.white);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(mirror ? 1f : 0f, 0.5f);
            ic.rectTransform.pivot = new Vector2(mirror ? 1f : 0f, 0.5f);
            ic.rectTransform.anchoredPosition = new Vector2((mirror ? -1f : 1f) * h * 0.15f, 0f);
            ic.rectTransform.sizeDelta = new Vector2(h * 0.55f, h * 0.55f);
            iconImage = ic;
            Text t = UiKit.AddText(pill.transform, "Value", "", 46, color, mirror ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(mirror ? 16f : h * 0.8f, 0f);
            trt.offsetMax = new Vector2(mirror ? -h * 0.8f : -16f, 0f);
            return t;
        }

        static void SetGraphics(Graphic[] g, bool on)
        {
            if (g == null) return;
            for (int i = 0; i < g.Length; i++) if (g[i] != null) g[i].enabled = on;
        }

        void BuildTiming(RectTransform root, RunSession session, float row2Y)
        {
            TimedText.Warmup();   // the cached clock strings are made here, never while the run is going (CUX-1)
            timeAttack = session.IsTimeAttack;
            timedRegion = session.Region.Id;
            timedMode = session.Mode;
            timedTarget = session.IsEndless ? 0 : Mathf.Max(1, session.Spec.TargetRow());
            ProgressService pr = GameSession.Progress;
            string bestLabel = "";
            if (pr != null)
            {
                if (timeAttack)
                {
                    bestCurve = pr.TimeAttackBestCurve(timedRegion, timedMode);
                    int bf = pr.TimeAttackBestFloors(timedRegion, timedMode);
                    hasBest = bf > 0;
                    if (hasBest) bestLabel = TimedText.Int(bf);
                }
                else
                {
                    PaceValue bp = session.IsEndless ? pr.EndlessBestPace(timedRegion, timedMode) : pr.BestPace(session.Level.Id, timedMode);
                    bestCurve = session.IsEndless ? pr.EndlessBestCurve(timedRegion, timedMode) : pr.BestCurve(session.Level.Id, timedMode);
                    hasBest = bp.IsValid;
                    if (hasBest)
                    {
                        var sb = new System.Text.StringBuilder(8);
                        TimeRecords.AppendHundredths(sb, bp.Hundredths);
                        bestLabel = sb.ToString();
                    }
                }
            }
            if (bestCurve == null) bestCurve = new int[0];
            ghostOn = GameSettings.PaceGhost && hasBest && bestCurve.Length > 0;

            // Row 3: timer, best pace (best floors in a Time Attack), live delta. Equal-ish shares; every pill keeps its text inside at 130 percent.
            float top = 16f + RunLayout.RowH + 12f + RunLayout.Row2H + 12f;
            RectTransform row = UiKit.Rect(root, "HudTimes");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(18f, -(top + RunLayout.Row3H));
            row.offsetMax = new Vector2(-18f, -top);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            hl.reverseArrangement = mirror;
            float h = RunLayout.Row3H;
            Image ic;
            timerText = TimedPill(row, "TimerPill", ToyKit.Get("icon_dial"), 1.3f, Color.white, h, out ic);
            UiKit.SetText(timerText, TimedText.TimeOfTenths(timeAttack ? TimeRecords.TimeAttackTicks / 6 : 0));
            bestText = TimedPill(row, "BestPill", ToyKit.Get("icon_crown"), 0.9f, new Color(1f, 0.9f, 0.4f), h, out ic);
            UiKit.SetText(bestText, bestLabel);
            bestGraphics = bestText.transform.parent.GetComponentsInChildren<Graphic>(true);
            SetGraphics(bestGraphics, hasBest);
            deltaText = TimedPill(row, "DeltaPill", Glyphs.Check, 1.2f, DeltaGood, h, out deltaIcon);
            deltaGraphics = deltaText.transform.parent.GetComponentsInChildren<Graphic>(true);
            SetGraphics(deltaGraphics, false);   // shown at the first delta
            deltaText.transform.parent.gameObject.AddComponent<A11yLabel>().Text = "Time against your best run";
            timerText.transform.parent.gameObject.AddComponent<A11yLabel>().Text = timeAttack ? "Time left" : "Run time";
            bestText.transform.parent.gameObject.AddComponent<A11yLabel>().Text = timeAttack ? "Best floors in a Time Attack" : "Best pace in plat per second";

            if (!ghostOn) return;
            if (!session.IsEndless && gaugeTrack != null)
            {
                // a diamond on the tower gauge: where your best run would be now (a different shape from the round yellow marker, and translucent)
                Image g = UiKit.AddImage(gaugeTrack, "GhostMarker", Theme.RoundSprite, new Color(0.85f, 0.95f, 1f, 0.7f));
                g.type = Image.Type.Sliced;
                g.pixelsPerUnitMultiplier = 3f;
                g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                g.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                g.rectTransform.sizeDelta = new Vector2(34f, 34f);
                g.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                g.rectTransform.anchoredPosition = new Vector2(mirror ? 370f : 30f, 0f);
                ghostMarker = g.rectTransform;
            }
            else
            {
                // endless and Time Attack have no gauge: a small pill in the free second row shows the floor the best run had reached
                Image gi;
                ghostText = TimedPill(root, "GhostPill", Glyphs.Ring, 0f, new Color(0.85f, 0.95f, 1f), RunLayout.Row2H - 12f, out gi);
                P((RectTransform)ghostText.transform.parent, new Vector2(0f, 1f), new Vector2(18f, row2Y - 6f), new Vector2(300f, RunLayout.Row2H - 12f));
                ghostText.transform.parent.gameObject.AddComponent<A11yLabel>().Text = "Pace ghost: the floor your best run had reached";
                UiKit.SetText(ghostText, "0");
            }
        }

        void ShowDelta(int deltaTicks)
        {
            if (!deltaShown) { deltaShown = true; SetGraphics(deltaGraphics, true); }
            bool good = deltaTicks <= 0;
            UiKit.SetText(deltaText, TimedText.Delta(deltaTicks));
            deltaText.color = good ? DeltaGood : DeltaBad;
            Sprite want = good ? Glyphs.Check : Glyphs.Cross;   // a tick or a cross: never colour alone
            if (deltaIcon.sprite != want) deltaIcon.sprite = want;
            deltaIcon.color = good ? DeltaGood : DeltaBad;
        }

        /// <summary>Per frame: no work unless the shown tenth, the floor bucket or the ghost step changed.</summary>
        void RefreshTiming(RunSession s, int row)
        {
            int ticks = s.RunTicks;
            int shown = timeAttack ? s.TimeAttackTicksLeft : ticks;
            int tenths = TimeRecords.TimeTenths(shown);
            if (tenths != lastTenths) { lastTenths = tenths; UiKit.SetText(timerText, TimedText.TimeOfTenths(tenths)); }

            if (endless && !timeAttack && row >= TimeRecords.SplitFloors && row / TimeRecords.SplitFloors > lastSplitStage)
            {
                // every 100 floors in endless: the time of this stage against the best split of it
                lastSplitStage = row / TimeRecords.SplitFloors;
                lastDeltaBucket = row / TimeRecords.DeltaEveryFloors;
                int d;
                if (GameSession.Progress != null && GameSession.Progress.EndlessSplitDelta(timedRegion, timedMode, lastSplitStage, ticks, out d)) ShowDelta(d);
            }
            else if (row >= TimeRecords.DeltaEveryFloors && row / TimeRecords.DeltaEveryFloors > lastDeltaBucket)
            {
                lastDeltaBucket = row / TimeRecords.DeltaEveryFloors;
                int d;
                if (hasBest && TimeRecords.TryDeltaTicks(bestCurve, row, ticks, out d)) ShowDelta(d);
            }

            if (ghostOn)
            {
                int milli = TimeRecords.FloorsMilliAtTick(bestCurve, ticks);
                if (ghostMarker != null)
                {
                    int key = milli / 100;
                    if (key != lastGhostKey)
                    {
                        lastGhostKey = key;
                        float f = Mathf.Clamp01(milli / 1000f / timedTarget);
                        if (gaugeTrack != null && gaugeTrack.rect.width > 60f) gaugeW = gaugeTrack.rect.width;
                        ghostMarker.anchoredPosition = new Vector2(30f + (mirror ? 1f - f : f) * (gaugeW - 60f), 0f);
                    }
                }
                else if (ghostText != null)
                {
                    int fl = milli / 1000;
                    if (fl != lastGhostKey) { lastGhostKey = fl; UiKit.SetText(ghostText, TimedText.Int(fl)); }
                }
            }
        }
    }
}
