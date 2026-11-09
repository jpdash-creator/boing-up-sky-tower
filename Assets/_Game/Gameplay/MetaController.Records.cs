using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>Timed gameplay entries of the Meta scene (doc 46): the Time Attack start on the endless popup.</summary>
    public sealed partial class MetaController
    {
        /// <summary>Name of the Time Attack button on the endless popup (tests look it up).</summary>
        public const string TimeAttackButtonName = "TimeAttack";

        /// <summary>Name of the caption text under the Time Attack button and under START (tests look them up).</summary>
        public const string TimeAttackCaptionName = "TimeAttackCaption", EndlessCaptionName = "EndlessCaption";

        const float ActionRowBottom = 24f, ActionCaptionGap = 8f, ActionRowTop = 16f, ActionSlotX = 200f;

        static float ActionCaptionH() { return Mathf.Ceil(UiKit.MinBodyTextPx * UiKit.TextScale * 1.3f) + 4f; }

        /// <summary>Size (square) of the two green action buttons on the endless popup: at least 48 dp on the real screen.</summary>
        public static float EndlessActionSize() { return Mathf.Max(150f, UiKit.MinTouchRef); }

        /// <summary>Height the endless popup reserves at the bottom for the action row (buttons, captions under them, margins).</summary>
        public static float EndlessActionRowHeight() { return ActionRowTop + EndlessActionSize() + ActionCaptionGap + ActionCaptionH() + ActionRowBottom; }

        /// <summary>
        /// The "3:00, best N floors" record line, placed <paramref name="lineTop"/> below the top of the panel. The two play buttons stand on one row below it
        /// (<see cref="AddEndlessActionRow"/>).
        /// </summary>
        void AddTimeAttackEntry(RectTransform panel, RegionDef region, int mode, float lineTop)
        {
            TimeAttackRecord rec = progress.TimeAttackBest(region.Id, mode);
            TimedUi.Line(panel, "TimeAttackBest", TimeAttackLineText(rec), lineTop, 780f, TimeAttackLineLines(rec), false, Theme.Ink);
        }

        /// <summary>
        /// ONE row at the bottom of the endless popup (Bug List 8 item 2b): the green START play button (Endless) on the left and the green Time Attack button (stopwatch
        /// icon) on the right, each with a caption under it so the two are told apart by icon AND word. Both are at least 48 dp, centred on the panel as a pair.
        /// </summary>
        void AddEndlessActionRow(RectTransform panel, RegionDef region)
        {
            float size = EndlessActionSize(), capH = ActionCaptionH();
            float btnY = ActionRowBottom + capH + ActionCaptionGap;
            Button start = ToyKit.PopupAction(panel, "START", Btn(1), Color.white, btnY, () => StartEndless(region), -ActionSlotX);
            start.GetComponent<A11yLabel>().Text = "Start Endless";
            Button ta = ToyKit.IconButton(panel, TimeAttackButtonName, "Time Attack, three minutes, floors reached is the score", 1, TimedIcons.Stopwatch, new Vector2(size, size), () => StartTimeAttack(region), 0.66f);
            UiKit.Place((RectTransform)ta.transform, new Vector2(0.5f, 0f), new Vector2(ActionSlotX, btnY), new Vector2(size, size));
            ActionCaption(panel, EndlessCaptionName, "Endless", -ActionSlotX, capH);
            ActionCaption(panel, TimeAttackCaptionName, "Time Attack", ActionSlotX, capH);
        }

        static void ActionCaption(RectTransform panel, string name, string text, float x, float h)
        {
            Text t = UiKit.AddText(panel, name, text, 42, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(t);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 0f), new Vector2(x, ActionRowBottom), new Vector2(2f * ActionSlotX - 20f, h));
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
