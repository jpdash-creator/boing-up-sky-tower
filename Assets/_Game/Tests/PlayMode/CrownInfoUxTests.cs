using System.Collections;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Crown line and crown calculation pop-ups (owner 2026-11-13, Main Bug List 2 item 4): the level and Endless start pop-ups show ONE compact crown line without a dark backing; the crown part is a
    /// tap target of at least 48 dp that opens a small pop-up with the formula, the best run, the setting bonus, the crown and (levels) the medal paces. Every number equals the stored crown;
    /// everything fits, never overlaps and keeps 16 sp text on every aspect at 100 and 130 percent text. Same class as the other layout audits.
    /// </summary>
    public partial class CriticalUxLayoutTests
    {
        static string CrownTextIn(RectTransform under, string name)
        {
            RectTransform rt = FindRt(name, under);
            Text t = rt != null ? rt.GetComponent<Text>() : null;
            return t != null ? t.text : null;
        }

        static bool CrownOverlap(Rect a, Rect b)
        {
            return Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 1f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 1f;
        }

        static RectTransform CrownUp(RectTransform rt, string name)
        {
            for (Transform t = rt; t != null; t = t.parent) if (t.name == name) return (RectTransform)t;
            return null;
        }

        /// <summary>The compact crown line of a start pop-up: inside the panel, one line, a 48 dp crown button, no dark pill, clear of everything around it.</summary>
        static void CrownCheckRow(string tag, RectTransform panel, string crownText, string secondText, List<string> problems)
        {
            RectTransform row = FindRt(CrownInfoUi.RowName, panel);
            if (row == null) { problems.Add(tag + "crown row missing"); return; }
            Rect pb = CanvasBox(panel), rb = CanvasBox(row);
            if (rb.xMin < pb.xMin - 1f || rb.xMax > pb.xMax + 1f || rb.yMin < pb.yMin - 1f || rb.yMax > pb.yMax + 1f) problems.Add(tag + "crown row leaves the panel");
            RectTransform btn = FindRt(CrownInfoUi.CrownButtonName, row), sec = FindRt(CrownInfoUi.SecondName, row);
            if (btn == null || sec == null) { problems.Add(tag + "crown button or second group missing"); return; }
            Rect bb = CanvasBox(btn), sb = CanvasBox(sec);
            if (bb.width < UiKit.MinTouchPx - 1f || bb.height < UiKit.MinTouchPx - 1f) problems.Add(tag + "crown button below 48 dp");
            if (Mathf.Abs(bb.center.y - sb.center.y) > 2f) problems.Add(tag + "crown and second value are not on one line");
            if (CrownOverlap(bb, sb)) problems.Add(tag + "crown button and second value overlap");
            if (bb.xMin < rb.xMin - 1f || sb.xMax > rb.xMax + 1f) problems.Add(tag + "the line is wider than its row");
            if (btn.GetComponent<A11yLabel>() == null || string.IsNullOrEmpty(btn.GetComponent<A11yLabel>().Text) || !btn.GetComponent<A11yLabel>().Text.Contains("calculated")) problems.Add(tag + "crown button needs an accessibility label");
            foreach (Image im in row.GetComponentsInChildren<Image>(true)) if (im.name == "Pill") problems.Add(tag + "the crown line must not sit on a dark pill");
            if (CrownTextIn(row, CrownInfoUi.CrownValueName) != crownText) problems.Add(tag + "crown shows '" + CrownTextIn(row, CrownInfoUi.CrownValueName) + "', expected '" + crownText + "'");
            if (CrownTextIn(row, CrownInfoUi.SecondValueName) != secondText) problems.Add(tag + "second value shows '" + CrownTextIn(row, CrownInfoUi.SecondValueName) + "', expected '" + secondText + "'");
            foreach (Text t in row.GetComponentsInChildren<Text>(true))
            {
                if (string.IsNullOrEmpty(t.text)) continue;
                if (!IsCentred(t.alignment)) problems.Add(tag + t.name + " is not centre aligned");
                if (t.fontSize < Mathf.RoundToInt(UiKit.MinBodyTextPx) - 1) problems.Add(tag + t.name + " is below 16 sp");
            }
            foreach (string other in new[] { "Star0", "Star1", "Star2", MedalStrip.RowName, "PaysLine", "RecordNone", "RecordLabel", "Record", "TimeAttackBest", "PLAY", "START", MetaController.TimeAttackButtonName, "Region", "EndlessIcon" })
            {
                RectTransform o = FindRt(other, panel);
                if (o != null && CrownOverlap(rb, CanvasBox(o))) problems.Add(tag + "crown row overlaps " + other);
            }
        }

        /// <summary>Taps the crown, checks the calculation pop-up against <paramref name="expect"/> and closes it (the X) back to the start pop-up.</summary>
        static IEnumerator CrownTapAndCheck(string tag, List<string> problems, CrownBreakdown expect, bool endless, string startName)
        {
            RectTransform startPopup = FindRt("Popup");
            RectTransform startPanel = startPopup != null ? startPopup.Find("Panel") as RectTransform : null;
            RectTransform btn = startPanel != null ? FindRt(CrownInfoUi.CrownButtonName, startPanel) : null;
            if (btn == null) { problems.Add(tag + "crown button missing"); yield break; }
            btn.GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform calc = FindRt(CrownInfoUi.CalcName);
            RectTransform panel = calc != null ? CrownUp(calc, "Panel") : null;
            RectTransform popup = panel != null ? CrownUp((RectTransform)panel.parent, "Popup") : null;
            if (calc == null || panel == null || popup == null) { problems.Add(tag + "calculation pop-up missing"); yield break; }

            Rect pb = CanvasBox(panel), ob = CanvasBox(popup);
            bool scrolls = panel.GetComponentInChildren<ScrollRect>(true) != null;
            if (pb.xMin < ob.xMin - 1f || pb.xMax > ob.xMax + 1f || pb.yMin < ob.yMin - 1f || pb.yMax > ob.yMax + 1f) problems.Add(tag + "calculation pop-up does not fit the screen");
            if (scrolls && GameSettings.TextPercent <= 100) problems.Add(tag + "the calculation needs a scroll view at normal text size");
            int closes = 0;
            foreach (Button b in panel.GetComponentsInChildren<Button>(false))
            {
                if (b.name == "Close" || b.name == "CloseX") closes++;
                Rect bb = CanvasBox((RectTransform)b.transform);
                if (bb.width < UiKit.MinTouchPx - 1f || bb.height < UiKit.MinTouchPx - 1f) problems.Add(tag + "button " + b.name + " below 48 dp");
            }
            if (closes != 1) problems.Add(tag + "calculation pop-up has " + closes + " close buttons, expected 1");

            var boxes = new List<KeyValuePair<string, Rect>>();
            foreach (Text t in panel.GetComponentsInChildren<Text>(false))
            {
                if (string.IsNullOrEmpty(t.text) || !t.gameObject.activeInHierarchy) continue;
                if (t.fontSize < Mathf.RoundToInt(UiKit.MinBodyTextPx) - 1) problems.Add(tag + t.name + " is below 16 sp");
                Rect tb = CanvasBox(t.rectTransform);
                if (!scrolls && (tb.xMin < pb.xMin - 1f || tb.xMax > pb.xMax + 1f || tb.yMin < pb.yMin - 1f || tb.yMax > pb.yMax + 1f)) problems.Add(tag + t.name + " leaves the panel");
                if (t.preferredHeight > tb.height + 2f && t.verticalOverflow == VerticalWrapMode.Truncate) problems.Add(tag + t.name + " is clipped");
                boxes.Add(new KeyValuePair<string, Rect>(t.name, tb));
            }
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    if (CrownOverlap(boxes[i].Value, boxes[j].Value)) problems.Add(tag + boxes[i].Key + " overlaps " + boxes[j].Key);

            if (CrownTextIn(calc, "Formula") == null || !CrownTextIn(calc, "Formula").Contains("floors x " + CrownScore.FloorPoints)) problems.Add(tag + "the formula is missing");
            if (CrownTextIn(calc, "Tip") == null) problems.Add(tag + "the tip line is missing");
            string bonus = "+" + SimConfig.DifficultyStepPercent[expect.Mode] + "%";
            if (CrownTextIn(calc, "RowBonusValue") != bonus) problems.Add(tag + "bonus shows '" + CrownTextIn(calc, "RowBonusValue") + "', expected " + bonus);
            if (CrownTextIn(calc, "RowCrownValue") != expect.Crown.ToString()) problems.Add(tag + "crown shows '" + CrownTextIn(calc, "RowCrownValue") + "', expected " + expect.Crown);
            if (expect.PartsKnown)
            {
                if (CrownTextIn(calc, "RowFloorsValue") != expect.FloorPoints.ToString()) problems.Add(tag + "floors points show '" + CrownTextIn(calc, "RowFloorsValue") + "', expected " + expect.FloorPoints);
                if (CrownTextIn(calc, "RowComboValue") != expect.ComboPoints.ToString()) problems.Add(tag + "combo points show '" + CrownTextIn(calc, "RowComboValue") + "', expected " + expect.ComboPoints);
                if (expect.FloorPoints + expect.ComboPoints != expect.BaseScore) problems.Add(tag + "the parts do not add up");
                string fl = CrownTextIn(calc, "RowFloorsLabel");
                if (fl == null || !fl.StartsWith(expect.Floors + " floors")) problems.Add(tag + "floors label shows '" + fl + "'");
            }
            if (endless)
            {
                if (FindRt("NoMedals", calc) == null) problems.Add(tag + "Endless must say it has no medals");
                if (FindRt("BronzeRow" + "Label", calc) != null) problems.Add(tag + "Endless shows no medal lines");
            }

            panel.Find("CloseX").GetComponent<Button>().onClick.Invoke();   // the one close returns to the start pop-up
            yield return null;
            yield return null;
            if (FindRt(CrownInfoUi.CalcName) != null) problems.Add(tag + "the calculation pop-up did not close");
            if (FindRt(startName) == null || FindRt(CrownInfoUi.RowName) == null) problems.Add(tag + "closing the calculation must return to the start pop-up");
        }

        [UnityTest]
        public IEnumerator CrownLineAndCalculationPopupsFitAndAddUp()
        {
            IMedalTargetSource savedTargets = MedalTargets.Source;
            var targets = new DictionaryMedalTargets();
            targets.Set("lvl_001_01", 3, 900);
            MedalTargets.Source = targets;
            var problems = new List<string>();
            try
            {
                foreach (int percent in new[] { 100, 130 })
                {
                    GameSettings.TextPercent = percent;
                    GameSettings.Difficulty = 3;   // Hard: +40 percent
                    yield return Load("Meta");
                    var meta = Object.FindFirstObjectByType<MetaController>();
                    RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
                    var levels = GameSession.Repo.LevelsOf(r1.Id);
                    ProgressService progress = GameSession.Progress;
                    // 37 floors and a chain of 5: 370 + 5 x (1 + 4 + 9 + 16 + 25) = 645, crown 645 x 1.40 = 903
                    progress.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 1, Score = 645, Floors = 37, BestCombo = 5, Mode = 3 });
                    progress.FinishEndlessRun(new RunRecord { Kind = RunKind.Endless, RegionId = r1.Id, Floors = 90, Ticks = 5400, Mode = 3, Score = 1050, BestCombo = 4 }, "20261113", out RecordResult rr);
                    CrownBreakdown level = CrownBreakdown.From(progress.BestCrownRun("lvl_001_01", 3), 3, progress.BestCrown("lvl_001_01", 3));
                    CrownBreakdown endless = CrownBreakdown.From(progress.EndlessCrownRun(r1.Id, 3), 3);
                    if (level.Crown != 903 || level.FloorPoints != 370 || level.ComboPoints != 275) problems.Add(percent + ": the stored level run is wrong (" + level.Crown + ")");
                    if (endless.Crown != 1470) problems.Add(percent + ": the stored endless run is wrong (" + endless.Crown + ")");

                    var holder = new RenderTexture[1];
                    foreach (Aspect a in Aspects)
                    {
                        UiKit.SafeAreaNormalized = () => FullSafe;

                        // level pop-up
                        meta.ShowLevels(r1);
                        meta.OnLevelTapped(levels[0]);
                        yield return ApplyAspect(a, holder);
                        string tag = a.Name + "@" + percent + " level: ";
                        RectTransform popup = FindRt("Popup");
                        RectTransform panel = popup != null ? popup.Find("Panel") as RectTransform : null;
                        if (panel == null) { problems.Add(tag + "popup missing"); continue; }
                        CrownCheckRow(tag, panel, level.Crown.ToString(), "100%", problems);
                        Shot("crown-level-popup", a, holder[0]);
                        yield return CrownTapAndCheck(tag, problems, level, false, "PLAY");
                        meta.ShowLevelCrownInfo(levels[0]);   // the calculation again, for the medal lines (the tap above opened and closed it)
                        yield return null;
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                        Shot("crown-calc-level", a, holder[0]);
                        RectTransform calc = FindRt(CrownInfoUi.CalcName);
                        if (calc == null) { problems.Add(tag + "calculation pop-up missing (medals)"); continue; }
                        for (int tier = 1; tier <= 3; tier++)
                        {
                            string name = MedalIcons.Name((MedalLevel)tier);
                            string label = CrownTextIn(calc, name + "RowLabel"), value = CrownTextIn(calc, name + "RowValue");
                            string wantValue = MedalStrip.PaceLineText(TimeRecords.MedalLinePaceMilli((MedalLevel)tier, 900));
                            if (label == null || !label.StartsWith(name)) problems.Add(tag + name + " medal line missing");
                            if (value != wantValue) problems.Add(tag + name + " pace shows '" + value + "', expected " + wantValue);
                            if (label != null && label.Contains("earned") != (progress.Medal("lvl_001_01", 3) >= (MedalLevel)tier)) problems.Add(tag + name + " earned mark is wrong");
                        }

                        // Endless pop-up
                        meta.ShowLevels(r1);
                        meta.OnEndlessTapped(r1);
                        yield return ApplyAspect(a, holder);
                        tag = a.Name + "@" + percent + " endless: ";
                        popup = FindRt("Popup");
                        panel = popup != null ? popup.Find("Panel") as RectTransform : null;
                        if (panel == null) { problems.Add(tag + "popup missing"); continue; }
                        CrownCheckRow(tag, panel, endless.Crown.ToString(), progress.EndlessBestFloor(r1.Id).ToString(), problems);
                        Shot("crown-endless-popup", a, holder[0]);
                        if (FindRt("START", panel) == null || FindRt(MetaController.TimeAttackButtonName, panel) == null) problems.Add(tag + "START or Time Attack missing");
                        yield return CrownTapAndCheck(tag, problems, endless, true, "START");
                    }
                    Restore(holder);
                }
            }
            finally { MedalTargets.Source = savedTargets; GameSettings.TextPercent = 100; GameSettings.Difficulty = 0; }
            Report(problems);
        }

        [UnityTest]
        public IEnumerator CrownLineWithoutARunShowsPlaceholdersAndStillOpens()
        {
            GameSettings.Difficulty = 2;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            var levels = GameSession.Repo.LevelsOf(r1.Id);
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowLevels(r1);
                meta.OnLevelTapped(levels[0]);
                yield return ApplyAspect(a, holder);
                string tag = a.Name + " no run: ";
                RectTransform popup = FindRt("Popup");
                RectTransform panel = popup != null ? popup.Find("Panel") as RectTransform : null;
                if (panel == null) { problems.Add(tag + "popup missing"); continue; }
                CrownCheckRow(tag, panel, "-", "100%", problems);
                meta.ShowLevelCrownInfo(levels[0]);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                RectTransform calc = FindRt(CrownInfoUi.CalcName);
                if (calc == null) { problems.Add(tag + "calculation pop-up missing"); continue; }
                if (CrownTextIn(calc, "RowCrownValue") != "?" || CrownTextIn(calc, "RowFloorsValue") != "?" || CrownTextIn(calc, "RowComboValue") != "?") problems.Add(tag + "placeholders missing");
                if (CrownTextIn(calc, "RowBonusValue") != "+" + SimConfig.DifficultyStepPercent[2] + "%") problems.Add(tag + "the setting bonus must show without a run");
                string hint = CrownTextIn(calc, "Hint");
                if (hint == null || !hint.Contains("Play this level")) problems.Add(tag + "the 'Play this level' hint is missing");
            }
            Restore(holder);
            GameSettings.Difficulty = 0;
            Report(problems);
        }
    }
}
