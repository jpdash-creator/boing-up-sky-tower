using System;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The crown calculation pop-ups (owner 2026-11-13): tapping the crown on the level or Endless start pop-up opens a small pop-up with the formula, the player's best run in the current
    /// Gameplay setting, the setting bonus and the resulting crown (levels: also the pace each medal needs). Its one close (the X in the frame) returns to the start pop-up.
    /// The layout lives in <see cref="CrownInfoUi"/>, the numbers in <see cref="CrownBreakdown"/>.
    /// </summary>
    public sealed partial class MetaController
    {
        /// <summary>Opens the crown calculation of a level for the current Gameplay setting.</summary>
        public void ShowLevelCrownInfo(LevelDef level)
        {
            int mode = GameSettings.Difficulty;
            CrownBreakdown b = CrownBreakdown.From(progress.BestCrownRun(level.Id, mode), mode, progress.BestCrown(level.Id, mode));
            OpenCrownCalc("Level " + level.Index + " crown", b, false, LevelMedals(level.Id, mode), () => OnLevelTapped(level));
        }

        /// <summary>Opens the crown calculation of a region's Endless tower for the current Gameplay setting (Endless has no medals).</summary>
        public void ShowEndlessCrownInfo(RegionDef region)
        {
            int mode = GameSettings.Difficulty;
            CrownBreakdown b = CrownBreakdown.From(progress.EndlessCrownRun(region.Id, mode), mode);
            OpenCrownCalc("Endless crown", b, true, null, () => OnEndlessTapped(region));
        }

        /// <summary>The three medal requirements of a level and setting (null when the level has no medal targets) and whether the player holds each.</summary>
        CrownCalcMedal[] LevelMedals(string levelId, int mode)
        {
            int target = MedalTargets.Lookup(levelId, mode);
            if (target <= 0) return null;
            MedalLevel held = progress.Medal(levelId, mode);
            var list = new CrownCalcMedal[3];
            for (int i = 0; i < 3; i++)
            {
                var tier = (MedalLevel)(i + 1);
                list[i] = new CrownCalcMedal { Tier = tier, PaceMilli = TimeRecords.MedalLinePaceMilli(tier, target), Earned = held >= tier };
            }
            return list;
        }

        void OpenCrownCalc(string title, CrownBreakdown b, bool endless, CrownCalcMedal[] medals, Action back)
        {
            ClosePopup();
            popup = UiKit.Rect(canvas.transform, "Popup");
            UiKit.Stretch(popup);
            var dim = popup.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            RectTransform panel = UiKit.BuildPopupFrame(popup, title, art, new Vector2(860f, 1200f), back);
            CrownInfoUi.BuildCalc(panel, b, GameSettings.DifficultyNames[Mathf.Clamp(b.Mode, 0, GameSettings.DifficultyNames.Length - 1)], endless, medals);
        }
    }
}
