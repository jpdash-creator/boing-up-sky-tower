using System.Collections.Generic;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Weekly Prize cards of the weekly leaderboards (Planning/LEADERBOARD-DESIGN.md section 8): one card per board whose closed week paid the player (the service says so in GET /my-week?prizes=1).
    /// They sit at the top of the Weekly tab. Tapping Claim calls POST /claim-prize once, grants the items through ProgressService (saved at once, idempotent per claim id), tells the service it was saved
    /// and shows the usual small reward card. Nothing is shown for guests, without consent, offline or while the feature is off.
    /// </summary>
    public sealed partial class RewardsScreen
    {
        public const string PrizeCardName = "WeeklyPrizeCard";

        int shownPrizeCount = -1;
        string prizeFailKey = "", prizeFailText = "";

        /// <summary>Reads the claimable prizes in the background; the Weekly tab is redrawn once when they changed. Called once when the screen is built.</summary>
        void StartPrizeRefresh()
        {
            if (!LeaderboardHub.CanUpload) return;
            LeaderboardHub.RefreshMyWeek(true, d =>
            {
                if (root == null || d == null || Tab != RewardTab.Weekly) return;
                if (LeaderboardHub.ClaimablePrizes().Count != shownPrizeCount) Render(true);
            });
        }

        /// <summary>The claimable weekly prize cards (tests count them).</summary>
        public int PrizeCardCount { get { return shownPrizeCount < 0 ? 0 : shownPrizeCount; } }

        void BuildLeaderboardPrizeCards(RectTransform content)
        {
            shownPrizeCount = 0;
            if (!LeaderboardHub.CanUpload) return;
            List<PendingPrize> list = LeaderboardHub.ClaimablePrizes();
            for (int i = 0; i < list.Count; i++)
            {
                shownPrizeCount++;
                BuildPrizeCard(content, list[i]);
            }
        }

        void BuildPrizeCard(RectTransform content, PendingPrize p)
        {
            const float btnW = 260f;
            float line = LineH(BodyPx);
            float h = CardPadY * 2f + line * 2f + 6f + line * 3f + 12f;   // title, then up to three lines of prize text at 130 percent text
            RectTransform card = UiKit.Rect(content, PrizeCardName + "_" + p.Board);
            FixedHeight(card, h);
            Image plate = Plate(card, "Plate", "plate_cream", StateTint(CardState.Ready), CardCorner);
            UiKit.Stretch(plate.rectTransform);

            string head = "Weekly Prize: " + LbBoards.TitleOf(p.Board) + ", rank " + p.Rank;
            Text title = UiKit.AddText(card, "Title", head, BodyPx, UiKit.PanelInk, TextAnchor.UpperLeft, true);
            UiKit.OnPanel(title);
            BestFit(title, false);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(CardPadX, -(CardPadY + line * 2f)); trt.offsetMax = new Vector2(-(btnW + CardPadX * 2f), -CardPadY);

            bool failed = prizeFailKey == p.Key && prizeFailText.Length > 0;
            string items = failed ? prizeFailText : p.Prize.Describe();
            Text body = UiKit.AddText(card, "Prize", items, BodyPx, UiKit.PanelInk, TextAnchor.UpperLeft);
            UiKit.OnPanel(body);
            BestFit(body, true);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(CardPadX, -(CardPadY + line * 2f + 6f + line * 3f)); brt.offsetMax = new Vector2(-(btnW + CardPadX * 2f), -(CardPadY + line * 2f + 6f));

            PendingPrize prize = p;
            float btnH = Mathf.Max(ClaimButtonH, UiKit.MinTouchRef);
            Button claim = UiKit.AddButton(card, "ClaimPrize", failed ? "Try again" : "Claim", ToyKit.Button(1), Color.white, new Vector2(btnW, btnH), () => ClaimLeaderboardPrize(prize), 46);
            claim.GetComponent<A11yLabel>().Text = "Claim weekly prize, " + LbBoards.TitleOf(p.Board) + " rank " + p.Rank;
            var crt = (RectTransform)claim.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-CardPadX, 0f);
            crt.sizeDelta = new Vector2(btnW, btnH);
        }

        static void BestFit(Text t, bool wrap)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            t.resizeTextMaxSize = t.fontSize;
        }

        /// <summary>Claims one weekly prize (tap on a card; tests call it too): the service claim, the grant, the ack, then the small reward card.</summary>
        public void ClaimLeaderboardPrize(PendingPrize prize)
        {
            if (prize == null) return;
            LeaderboardHub.ClaimPrize(prize, o =>
            {
                if (root == null) return;
                if (o.Status == ClaimStatus.Granted && o.Reward != null) { prizeFailKey = ""; prizeFailText = ""; AfterClaim(o.Reward); return; }
                if (o.Status == ClaimStatus.AlreadyClaimed) { prizeFailKey = ""; Render(true); return; }
                if (o.Status == ClaimStatus.Busy) return;
                prizeFailKey = prize.Key;
                prizeFailText = o.Status == ClaimStatus.Offline ? "Can't reach the server. Try again." : o.Status == ClaimStatus.NotEligible ? "This prize is no longer available." : "Could not claim. Try again later.";
                Render(true);
            });
        }
    }
}
