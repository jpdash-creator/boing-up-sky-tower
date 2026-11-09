using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Timed gameplay entries of the Meta scene (doc 46): the Time Attack start on the endless popup.</summary>
    public sealed partial class MetaController
    {
        /// <summary>Name of the Time Attack button on the endless popup (tests look it up).</summary>
        public const string TimeAttackButtonName = "TimeAttack";

        /// <summary>
        /// A line "3:00, best N floors" with the TIME ATTACK button under it, placed <paramref name="lineTop"/> below the top of the panel and the button
        /// <paramref name="buttonBottom"/> above its bottom. Open from the start; no revive; the score is the floors in 3:00.
        /// </summary>
        void AddTimeAttackEntry(RectTransform panel, RegionDef region, int mode, float lineTop, float buttonH, float buttonBottom)
        {
            TimeAttackRecord rec = progress.TimeAttackBest(region.Id, mode);
            TimedUi.Line(panel, "TimeAttackBest", TimeAttackLineText(rec), lineTop, 780f, TimeAttackLineLines(rec), false, Theme.Ink);
            Button b = UiKit.AddButton(panel, TimeAttackButtonName, "TIME ATTACK", Btn(0), Color.white, new Vector2(560f, buttonH), () => StartTimeAttack(region), 46);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0f, buttonBottom), new Vector2(560f, buttonH));
            b.GetComponent<A11yLabel>().Text = "Time Attack, three minutes, floors reached is the score";
        }

        /// <summary>Text lines of the Time Attack record on the endless popup: 1 without a record, 2 with one (best floors, then best pace and the time of that run).</summary>
        public static int TimeAttackLineLines(TimeAttackRecord rec) { return rec != null && rec.BestFloors > 0 ? 2 : 1; }

        /// <summary>
        /// "3:00, best N floors" and, on a second line, the best pace (floors per second of that run, "0.83 plat/s") plus "fell at m:ss" when the best run ended before the full 3:00.
        /// Built only when the popup opens (never per frame).
        /// </summary>
        public static string TimeAttackLineText(TimeAttackRecord rec)
        {
            if (rec == null || rec.BestFloors <= 0) return "3:00, floors in three minutes";
            string head = "3:00, best " + rec.BestFloors + " floors";
            int ticks = rec.Ticks > 0 ? rec.Ticks : TimeRecords.TimeAttackTicks;   // saves from before the time was stored count as a full run
            string tail = TimedUi.PaceText(new PaceValue(rec.BestFloors, ticks));
            if (ticks < TimeRecords.TimeAttackTicks) tail += ", fell at " + TimeRecords.FormatTime(ticks);
            return head + "\n" + tail;
        }

        /// <summary>Height of the Time Attack record text on the endless popup at the current text size.</summary>
        public static float TimeAttackLineHeight(TimeAttackRecord rec) { return TimeAttackLineLines(rec) * TimedUi.LineHeight(); }

        /// <summary>Opens the 3:00 Time Attack tower of a region in the Run scene.</summary>
        public void StartTimeAttack(RegionDef region)
        {
            GameSession.SelectTimeAttack(region.Id);
            SceneManager.LoadScene("Run");
        }
    }
}
