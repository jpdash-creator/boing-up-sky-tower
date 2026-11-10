using System.Collections.Generic;
using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The "Completed (N)" section at the bottom of the Main, Daily and Weekly lists (owner Bug List 10). Finished cards (claimed goals and weekly tasks, finished daily actions)
    /// leave the main list and live under a header row that is collapsed by default: a tap expands small dimmed cards, a second tap collapses again.
    /// Main and Weekly cards move when the list is rebuilt after the claim (after the jingle and the reward card were started); Daily actions have no claim of their own
    /// (their chests are on the track), so a finished action moves at once, with the next rebuild.
    /// </summary>
    public sealed partial class RewardsScreen
    {
        bool completedOpen;

        /// <summary>True while the Completed section of the current list is expanded (tests read it).</summary>
        public bool CompletedOpen { get { return completedOpen; } }

        /// <summary>True when the card of the entry is finished: a claimed goal / weekly task, or a daily action that reached its target.</summary>
        bool EntryDone(RewardEntry e, bool claimable)
        {
            RegionGoal goal = e as RegionGoal;
            if (goal != null) return progress.IsRegionGoalClaimed(goal);
            if (claimable) return progress.IsRewardClaimed(Tab, e);
            return progress.RewardProgress(Tab, e) >= progress.RewardTarget(Tab, e);
        }

        /// <summary>Open cards first (in table order), then the Completed section (header row, and the small cards when it is expanded).</summary>
        void AddCardsWithCompleted(RectTransform content, IReadOnlyList<RewardEntry> entries, bool claimable)
        {
            List<RewardEntry> done = null;
            for (int i = 0; i < entries.Count; i++)
            {
                RewardEntry e = entries[i];
                if (EntryDone(e, claimable)) { if (done == null) done = new List<RewardEntry>(8); done.Add(e); }
                else AddCard(content, e, claimable);
            }
            AddCompletedSection(content, done, claimable);
        }

        /// <summary>The header row "Completed (N)" and, when expanded, the small cards. Nothing is added when nothing is completed.</summary>
        void AddCompletedSection(RectTransform content, List<RewardEntry> done, bool claimable)
        {
            if (done == null || done.Count == 0) return;
            AddCompletedHeader(content, done.Count);
            if (!completedOpen) return;
            for (int i = 0; i < done.Count; i++) AddCompletedCard(content, done[i]);
        }

        /// <summary>Height of the header row: the 48 dp touch minimum, or the text with padding when that is more (it grows with the text size).</summary>
        public static float CompletedHeaderH { get { return Mathf.Max(Mathf.Ceil(UiKit.MinTouchPx), LineH(BodyPx) + 20f); } }

        void AddCompletedHeader(RectTransform content, int count)
        {
            RectTransform row = UiKit.Rect(content, "CompletedHeader");
            float h = CompletedHeaderH;
            FixedHeight(row, h);
            Sprite sp = ToyKit.Get("plate_chip") ?? ToyKit.Button(2);
            Button b = UiKit.AddButton(row, "Toggle", "", sp, Color.white, new Vector2(400f, h), ToggleCompleted, BodyPx);
            b.GetComponent<Image>().raycastTarget = true;
            b.GetComponent<A11yLabel>().Text = "Completed, " + count + (count == 1 ? " reward, " : " rewards, ") + (completedOpen ? "expanded, tap to collapse" : "collapsed, tap to expand");
            RectTransform brt = (RectTransform)b.transform;
            UiKit.Stretch(brt);
            Text t = UiKit.AddText(brt, "Label", "Completed (" + count + ")", BodyPx, Color.white, TextAnchor.MiddleLeft, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(CardPadX + 8f, 0f);
            trt.offsetMax = new Vector2(-(CardPadX + 8f + 56f), 0f);
            // Root cause of the stray down arrow: UiKit.Place pivots the rect on its right edge, and a 180 degree turn about that pivot swung the arrow out of the header.
            // So the arrow slot is a fixed, unrotated rect (same place in both states) and only its centred child image is turned.
            float a = Mathf.Min(48f, h - 12f);
            RectTransform slot = UiKit.Rect(brt, "ArrowSlot");
            slot.anchorMin = slot.anchorMax = new Vector2(1f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.sizeDelta = new Vector2(a, a);
            slot.anchoredPosition = new Vector2(-(CardPadX + 8f + a * 0.5f), 0f);
            Image arrow = UiKit.AddImage(slot, "Arrow", Glyphs.Up, Color.white);
            arrow.raycastTarget = false;
            UiKit.Stretch(arrow.rectTransform);
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, completedOpen ? 0f : 180f);   // points up when open, down when it can be opened
        }

        void ToggleCompleted()
        {
            completedOpen = !completedOpen;
            RefreshList();
        }

        /// <summary>Rebuilds only what the list needs after the Completed section changed; the scroll position is kept.</summary>
        void RefreshList()
        {
            float y = ListContent != null ? ListContent.anchoredPosition.y : 0f;
            if (Tab == RewardTab.Main) RebuildGoals();
            else Render(true);
            if (Tab == RewardTab.Main && ListContent != null) ListContent.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>A small dimmed card of a finished entry: pale green plate, check badge and the title on one line (no button, nothing to claim).</summary>
        void AddCompletedCard(RectTransform list, RewardEntry e)
        {
            RectTransform row = UiKit.Rect(list, "Row_" + e.Id);
            RectTransform card = UiKit.Rect(row, "Small");   // not "Card": it has no bar, so the full-card layout checks skip it
            UiKit.Stretch(card);
            Image bg = Plate(card, "Bg", "plate_cream", new Color(0.78f, 0.90f, 0.72f, 1f), CardCorner * 0.6f);   // paler than the claimed card: dimmed, but the ink text keeps its contrast
            UiKit.Stretch(bg.rectTransform);

            float listW = screen != null && screen.rect.width > 200f ? screen.rect.width : 1080f;
            float cardW = listW - 2f * Side;
            const float badge = 40f;
            float left = CardPadX + badge + 12f, right = CardPadX;
            Text title = UiKit.AddText(card, "Title", TitleOf(e), BodyPx, Theme.Ink, TextAnchor.MiddleLeft, true);
            UiKit.OnPanel(title);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            float textW = Mathf.Max(1f, cardW - left - right);
            float titleH = Mathf.Max(LineH(BodyPx), MeasureTitle(title, textW));
            int minFs = Mathf.CeilToInt(UiKit.MinBodyTextPx);
            while (titleH > Mathf.Ceil(title.fontSize * 1.3f) && title.fontSize - 2 >= minFs)
            {
                title.fontSize -= 2;
                titleH = Mathf.Max(Mathf.Ceil(title.fontSize * 1.22f), MeasureTitle(title, textW));
            }
            float h = Mathf.Max(CompletedHeaderH, titleH + 24f);
            FixedHeight(row, h);
            RectTransform trt = title.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(left, 6f);
            trt.offsetMax = new Vector2(-right, -6f);
            Image tick = CheckBadge(card, "Check", badge);
            UiKit.Place(tick.rectTransform, new Vector2(0f, 0.5f), new Vector2(CardPadX, 0f), new Vector2(badge, badge));
            card.gameObject.AddComponent<A11yLabel>().Text = TitleOf(e) + ", completed";
        }
    }
}
