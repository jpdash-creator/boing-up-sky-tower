using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    public sealed partial class RunHud
    {
        /// <summary>The record report of the run that just ended (what RunController stored in GameSession.LastResult), or null when there is none for this kind of run.</summary>
        static RecordResult LastRecord(bool endlessRun)
        {
            RunResult r = GameSession.LastResult;
            if (r == null || r.Record == null || r.Endless != endlessRun) return null;
            return r.Record;
        }

        /// <summary>
        /// The timed rows of a result screen (doc 46 section 5): the run Time with the difference to the previous best, the Pace with its difference, and under them either the NEW BEST
        /// badge or, for a revived run, a line saying why no record was set. A tick or a cross in front of each difference says better or worse (never colour alone).
        /// Heights are known before the popup is opened (<see cref="TailHeight"/>), so the popup is sized to its content.
        /// </summary>
        struct ResultTiming
        {
            public int Ticks, Floors;
            public RecordResult Rec;
            public bool Endless, NewBest, Revived;

            const float BadgeH = 110f;

            /// <summary>Names of the badges (tests look them up). The generic one only shows for a record that is neither a Time nor a Pace (endless floors or a split).</summary>
            public const string BadgeTimeName = "NewBestTimeBadge", BadgePaceName = "NewBestPaceBadge", BadgeGenericName = "NewBestBadge";

            /// <summary>The run set a new best Time (a level: shorter; endless and Time Attack: longer survived).</summary>
            public bool BestTime { get { return Rec != null && (Rec.NewBestTime || Rec.NewBestSurvived); } }
            /// <summary>The run set a new best Pace.</summary>
            public bool BestPace { get { return Rec != null && Rec.NewBestPace; } }
            /// <summary>How many NEW BEST ribbons are shown: one each for Time and Pace (both when both), else one generic ribbon, else none.</summary>
            public int BadgeCount { get { return (BestTime ? 1 : 0) + (BestPace ? 1 : 0) + ((BestTime || BestPace) ? 0 : (NewBest ? 1 : 0)); } }

            public static ResultTiming For(RunSession s, RecordResult rec, bool endless, bool extraNewBest)
            {
                return new ResultTiming
                {
                    Rec = rec,
                    Endless = endless,
                    Ticks = rec != null ? rec.Ticks : s.RunTicks,
                    Floors = rec != null ? rec.Floors : s.RunFloors,
                    Revived = rec != null ? rec.Revived : s.RunRevived,
                    NewBest = extraNewBest || (rec != null && rec.AnyNewBest)
                };
            }

            /// <summary>Height under the pace row: the badge, or the revive note, or nothing.</summary>
            public float TailHeight
            {
                get { return BadgeCount > 0 ? BadgeCount * BadgeH : (Revived ? 2f * TimedUi.LineHeight() + 10f : 0f); }
            }

            /// <summary>One gold ribbon with its label; the text shrinks to fit (never clipped, never wrapped) at any text scale.</summary>
            static void Badge(Transform panel, string name, string label, float y)
            {
                Text ribbon = ToyKit.Ribbon(panel, label, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(520f, BadgeH - 10f));
                ribbon.transform.parent.name = name;
                var trt = (RectTransform)ribbon.transform;
                trt.anchorMin = new Vector2(0.14f, 0.5f);
                trt.anchorMax = new Vector2(0.86f, 0.9f);
                ribbon.horizontalOverflow = HorizontalWrapMode.Wrap;
                ribbon.verticalOverflow = VerticalWrapMode.Truncate;
                ribbon.resizeTextForBestFit = true;
                ribbon.resizeTextMinSize = 20;
                ribbon.resizeTextMaxSize = 52;
                ribbon.transform.parent.gameObject.AddComponent<A11yLabel>().Text = label;
            }

            public void Build(Transform panel, float yTime, float yPace, float yTail)
            {
                bool counted = Rec != null && Rec.Counted;
                RectTransform rowT = ToyKit.PillRow(panel, "ResultTime", yTime, 96f);
                TimedUi.StatPill(rowT, ToyKit.Get("icon_dial"), TimeRecords.FormatTime(Ticks), Color.white, 380f, 96f);
                if (counted && Rec.HasPrevTime)
                {
                    bool good = Endless ? Rec.DeltaTicks >= 0 : Rec.DeltaTicks <= 0;   // a level: a shorter Time is better; endless: living longer is better
                    TimedUi.StatPill(rowT, good ? Glyphs.Check : Glyphs.Cross, TimeRecords.FormatDeltaTime(Rec.DeltaTicks), good ? TimedUi.Good : TimedUi.Bad, 396f, 96f).transform.parent.name = "TimeDelta";
                }
                RectTransform rowP = ToyKit.PillRow(panel, "ResultPace", yPace, 96f);
                TimedUi.StatPill(rowP, ToyKit.Get("icon_speed"), TimeRecords.FormatPace(Floors, Ticks), TimedUi.Gold, 496f, 96f);
                if (counted && Rec.HasPrevPace)
                {
                    bool good = Rec.DeltaPaceHundredths >= 0;
                    TimedUi.StatPill(rowP, good ? Glyphs.Check : Glyphs.Cross, TimedText.PaceShort(Rec.DeltaPaceHundredths), good ? TimedUi.Good : TimedUi.Bad, 290f, 96f).transform.parent.name = "PaceDelta";
                }
                if (BadgeCount > 0)
                {
                    float y = yTail;
                    if (BestTime) { Badge(panel, BadgeTimeName, "NEW BEST TIME", y); y += BadgeH; }
                    if (BestPace) { Badge(panel, BadgePaceName, "NEW BEST PACE", y); y += BadgeH; }
                    if (!BestTime && !BestPace) Badge(panel, BadgeGenericName, "NEW BEST", y);
                }
                else if (Revived)
                {
                    TimedUi.Line(panel, "ReviveNote", "Revive used: no record or medal", yTail, 780f, 2, true, Theme.InkSoft);
                }
            }
        }
    }
}
