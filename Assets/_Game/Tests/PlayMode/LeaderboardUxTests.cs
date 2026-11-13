using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Leaderboard;
using FluffyClimbers.Monetization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FluffyClimbers.Tests
{
    /// <summary>Scripted answers for the leaderboard screens (no network): the board, the own standing, the prizes and the claims.</summary>
    sealed class PlayLbTransport : ILeaderboardTransport
    {
        public Func<string, string, string, HttpReply> Handler;
        public int Calls;

        public void Send(string method, string url, string bearer, string jsonBody, Action<HttpReply> done)
        {
            Calls++;
            done(Handler != null ? Handler(method, url, jsonBody ?? "") : new HttpReply { NetworkError = true });
        }

        public void GetTexture(string url, Action<Texture2D> done) { done(null); }
    }

    /// <summary>
    /// CriticalUX audits of the weekly leaderboard (CUX-3 / CUX-4 / CUX-5 / CUX-6): the screen (three boards, scrolled, prizes popup, sign-in, consent, empty, failed, coming soon), the consent popup,
    /// the Weekly Prize cards of the Rewards screen and the bottom bar, on every aspect, with a notch, at 130 percent text and left-handed. Part of the same test class as the other layout audits so it
    /// shares their overlap, safe-area, touch-target and text-size checks.
    /// </summary>
    public partial class CriticalUxLayoutTests
    {
        const string LbWeek = "2026-11-02";

        static string LbBoardJson(int n)
        {
            var sb = new StringBuilder("{\"fields\":{\"final\":{\"booleanValue\":false},\"entries\":{\"arrayValue\":{\"values\":[");
            for (int i = 1; i <= n; i++)
            {
                string name = i == 7 ? "Extraordinarily Long Player Name" : i == 12 ? "Wide W W W W W W W W" : "Climber " + i;
                if (i > 1) sb.Append(',');
                sb.Append("{\"mapValue\":{\"fields\":{\"rank\":{\"integerValue\":\"").Append(i).Append("\"},\"pid\":{\"stringValue\":\"p").Append(i)
                  .Append("\"},\"name\":{\"stringValue\":\"").Append(name).Append("\"},\"avatar\":{\"stringValue\":\"\"},\"crowns\":{\"integerValue\":\"")
                  .Append(1234567L - i * 9999L).Append("\"}}}}");
            }
            return sb.Append("]}}}}").ToString();
        }

        static string LbMyWeekJson(bool prizes, string pid = "p37", int overallRank = 37)
        {
            string p = prizes
                ? ",\"pendingPrizes\":[{\"weekKey\":\"" + LbWeek + "\",\"board\":\"overall\",\"rank\":2,\"crowns\":400000,\"prize\":{\"bundles\":1,\"ironChests\":1},\"state\":\"open\"},"
                  + "{\"weekKey\":\"" + LbWeek + "\",\"board\":\"levels\",\"rank\":1,\"crowns\":500000,\"prize\":{\"goldChests\":1,\"powerUps\":3},\"state\":\"open\"},"
                  + "{\"weekKey\":\"" + LbWeek + "\",\"board\":\"endless\",\"rank\":5,\"crowns\":90000,\"prize\":{\"powerUps\":1,\"coins\":3000},\"state\":\"open\"}]"
                : ",\"pendingPrizes\":[]";
            return "{\"weekKey\":\"" + LeaderboardTime.WeekKey(DateTime.UtcNow) + "\",\"pid\":\"" + pid + "\",\"weekEndsAt\":\"" + LeaderboardTime.WeekEndUtc(DateTime.UtcNow).ToString("o") + "\","
                 + "\"totals\":{\"overall\":1234567,\"levels\":1000000,\"endless\":234567},\"ranks\":["
                 + "{\"board\":\"overall\",\"crowns\":1234567,\"rank\":" + overallRank + ",\"entryLimit\":200000,\"reachedLimit\":true},"
                 + "{\"board\":\"levels\",\"crowns\":80000,\"rank\":412,\"entryLimit\":150000,\"reachedLimit\":false},"
                 + "{\"board\":\"endless\",\"crowns\":0,\"rank\":0,\"entryLimit\":50000,\"reachedLimit\":false}]" + p + "}";
        }

        static string LbBetween(string s, string a, string b)
        {
            int i = s.IndexOf(a, StringComparison.Ordinal);
            if (i < 0) return "";
            i += a.Length;
            int j = s.IndexOf(b, i, StringComparison.Ordinal);
            return j < 0 ? "" : s.Substring(i, j - i);
        }

        /// <summary>A server that answers the board, my-week and claims. <paramref name="boardMode"/>: "rows", "empty", "offline".</summary>
        static PlayLbTransport LbServer(string boardMode, bool prizes, string pid = "p37", int overallRank = 37)
        {
            var net = new PlayLbTransport();
            net.Handler = (m, u, b) =>
            {
                if (u.Contains("/boards/"))
                {
                    if (boardMode == "offline") return new HttpReply { NetworkError = true };
                    if (boardMode == "empty") return new HttpReply { Status = 404, Body = "" };
                    return new HttpReply { Status = 200, Body = LbBoardJson(100) };
                }
                if (u.Contains("/my-week")) return new HttpReply { Status = 200, Body = LbMyWeekJson(prizes, pid, overallRank) };
                if (u.EndsWith("/claim-prize"))
                {
                    if (b.Contains("\"ack\":true")) return new HttpReply { Status = 200, Body = "{\"state\":\"granted\",\"grant\":false}" };
                    string week = LbBetween(b, "\"weekKey\":\"", "\""), board = LbBetween(b, "\"board\":\"", "\"");
                    string items = board == "overall" ? "\"bundles\":1,\"ironChests\":1" : board == "levels" ? "\"goldChests\":1,\"powerUps\":3" : "\"powerUps\":1,\"coins\":3000";
                    return new HttpReply { Status = 200, Body = "{\"claimId\":\"" + week + "_" + board + "_stub-uid\",\"state\":\"issued\",\"grant\":true,\"prize\":{" + items + "}}" };
                }
                return new HttpReply { Status = 200, Body = "{}" };
            };
            return net;
        }

        /// <summary>Puts the hub in a state: live or off, Google signed in or a guest, consent or not.</summary>
        static StubLeaderboardAuth LbState(PlayLbTransport net, bool live, bool signedIn, bool consent)
        {
            LeaderboardHub.ResetForTests();
            LeaderboardHub.AccountModeIsGoogle = null;
            Services.Account = new StubAccountService { UseMemoryOnly = true };
            var auth = new StubLeaderboardAuth();
            ProgressService p = GameSession.Progress;
            LeaderboardHub.Setup(p, live ? new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" } : LeaderboardConfig.Off, auth, net);
            if (signedIn) { Services.Account.SignIn(null); auth.SignIn(null); }
            if (consent) p.GrantLeaderboardConsent(1);
            else { p.RevokeLeaderboardConsent(); p.DeclineLeaderboardConsent(LeaderboardTime.DayKey(DateTime.UtcNow)); }   // declined today: the popup does not cover the card
            return auth;
        }

        static void LbCleanUp()
        {
            LeaderboardHub.ResetForTests();
            LeaderboardHub.AccountModeIsGoogle = null;
            AvatarCache.Loader = null;
            AvatarCache.Clear();
        }

        [UnityTest]
        public IEnumerator LeaderboardScreenHasNoOverlapAndFitsEveryAspect() { return LeaderboardAudit(100, false); }

        /// <summary>CUX-5 and the left-handed option: the leaderboard survives the 130 percent text size and the mirrored layout.</summary>
        [UnityTest]
        public IEnumerator LeaderboardScreenSurvives130PercentTextAndLeftHandedLayout() { return LeaderboardAudit(130, true); }

        IEnumerator LeaderboardAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            try
            {
                LbState(LbServer("rows", false), true, true, true);
                yield return CheckScreen("leaderboard-overall", () => meta.ShowLeaderboard(0), problems);
                yield return CheckScreen("leaderboard-levels", () => meta.ShowLeaderboard(1), problems);
                yield return CheckScreen("leaderboard-endless", () => meta.ShowLeaderboard(2), problems);
                yield return CheckScreen("leaderboard-scrolled", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.ScrollTo(0.5f); }, problems);
                yield return CheckScreen("leaderboard-bottom", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.ScrollTo(0f); }, problems);
                yield return CheckScreen("leaderboard-prizes-overall", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.OpenPrizes(); }, problems);
                yield return CheckScreen("leaderboard-prizes-endless", () => { meta.ShowLeaderboard(2); meta.CurrentLeaderboard.OpenPrizes(); }, problems);

                LbState(LbServer("rows", false, "p5", 5), true, true, true);
                yield return CheckScreen("leaderboard-own-rank-5", () => meta.ShowLeaderboard(0), problems);
                LbState(LbServer("rows", false, "p37", 37), true, true, true);
                yield return CheckScreen("leaderboard-own-rank-37", () => meta.ShowLeaderboard(0), problems);
                LbState(LbServer("rows", false, "pOutside", 348), true, true, true);
                yield return CheckScreen("leaderboard-pinned-outside", () => meta.ShowLeaderboard(0), problems);
                yield return CheckScreen("leaderboard-pinned-bottom", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.ScrollTo(0f); }, problems);

                LbState(LbServer("empty", false), true, true, true);
                yield return CheckScreen("leaderboard-empty", () => meta.ShowLeaderboard(0), problems);
                LbState(LbServer("offline", false), true, true, true);
                yield return CheckScreen("leaderboard-offline", () => meta.ShowLeaderboard(0), problems);

                LbState(LbServer("rows", false), true, false, false);   // a guest
                yield return CheckScreen("leaderboard-sign-in-card", () => meta.ShowLeaderboard(0), problems);
                LbState(LbServer("rows", false), true, true, false);    // signed in, no consent yet (declined today)
                yield return CheckScreen("leaderboard-consent-card", () => meta.ShowLeaderboard(0), problems);
                yield return CheckScreen("leaderboard-consent-popup", () => { meta.ShowLeaderboard(0); meta.ShowLeaderboardConsent(); }, problems);
                LbState(LbServer("rows", false), false, false, false);  // switched off
                yield return CheckScreen("leaderboard-coming-soon", () => meta.ShowLeaderboard(0), problems);
            }
            finally { LbCleanUp(); GameSettings.LeftHanded = false; }
            Report(problems);
        }

        [UnityTest]
        public IEnumerator WeeklyPrizeCardsFitTheRewardsScreenOnEveryAspect() { return PrizeCardAudit(100); }

        [UnityTest]
        public IEnumerator WeeklyPrizeCardsSurvive130PercentText() { return PrizeCardAudit(130); }

        IEnumerator PrizeCardAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            try
            {
                LbState(LbServer("rows", true), true, true, true);
                LeaderboardHub.RefreshMyWeek(true, null, true);   // the prizes are known when the screen opens
                yield return CheckScreen("rewards-weekly-prize-cards", () => meta.ShowRewards(RewardTab.Weekly), problems);
                meta.ShowRewards(RewardTab.Weekly);
                Assert.AreEqual(3, meta.CurrentRewards.PrizeCardCount, "one Weekly Prize card per board with a claimable prize");
                LbState(LbServer("rows", false), true, true, true);
                LeaderboardHub.RefreshMyWeek(true, null, true);
                meta.ShowRewards(RewardTab.Weekly);
                Assert.AreEqual(0, meta.CurrentRewards.PrizeCardCount, "no card without a prize");
                LbState(LbServer("rows", true), false, false, false);   // feature off: never a card
                meta.ShowRewards(RewardTab.Weekly);
                Assert.AreEqual(0, meta.CurrentRewards.PrizeCardCount);
            }
            finally { LbCleanUp(); }
            Report(problems);
        }

        [UnityTest]
        public IEnumerator TappingAWeeklyPrizeCardClaimsItOnceAndRemovesTheCard()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", true), true, true, true);
                LeaderboardHub.RefreshMyWeek(true, null, true);
                meta.ShowRewards(RewardTab.Weekly);
                yield return null;
                Assert.AreEqual(3, meta.CurrentRewards.PrizeCardCount);
                int coins = GameSession.Progress.Coins;
                Button claim = null;
                foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                    if (b.name == "ClaimPrize" && b.transform.parent.name == RewardsScreen.PrizeCardName + "_endless") claim = b;
                Assert.IsNotNull(claim, "the endless board card has its Claim button");
                claim.onClick.Invoke();
                yield return null;
                Assert.AreEqual(coins + 3000, GameSession.Progress.Coins, "the prize is in the wallet at once");
                Assert.AreEqual(2, meta.CurrentRewards.PrizeCardCount, "the claimed card is gone");
                Assert.IsTrue(GameSession.Progress.LeaderboardPrizeGranted(LbWeek + "_endless_stub-uid"));
                // a second claim of the same prize (a stale card, a second tap) grants nothing
                var stale = new PendingPrize { WeekKey = LbWeek, Board = "endless", Rank = 5, Prize = new PrizeItemsDto { PowerUps = 1, Coins = 3000 } };
                ClaimOutcome again = null;
                LeaderboardHub.ClaimPrize(stale, o => again = o);
                Assert.AreEqual(ClaimStatus.AlreadyClaimed, again.Status);
                Assert.AreEqual(coins + 3000, GameSession.Progress.Coins);
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator BottomBarOfMapAndLevelsHasShopLeaderboardHeroesRewardsInThatOrder()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            try
            {
                LbState(LbServer("rows", false), true, false, false);   // a guest taps the crown
                foreach (string where in new[] { "map", "levels" })
                {
                    if (where == "map") meta.ShowMap(); else meta.ShowLevels(r1);
                    yield return null;
                    string before = meta.CurrentScreen;
                    string[] names = { "TabShop", "TabLeaderboard", "TabCharacters", "TabDailyrewards" };
                    var buttons = new Button[names.Length];
                    foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                        for (int i = 0; i < names.Length; i++) if (b.name == names[i] && b.gameObject.activeInHierarchy) buttons[i] = b;
                    for (int i = 0; i < names.Length; i++) Assert.IsNotNull(buttons[i], where + ": the bar has " + names[i]);
                    for (int i = 1; i < names.Length; i++)
                        Assert.Greater(buttons[i].transform.position.x, buttons[i - 1].transform.position.x, where + ": " + names[i] + " sits right of " + names[i - 1]);
                    Assert.IsNotNull(buttons[1].transform.Find("Icon"), "the Leaderboard button carries the crown icon");
                    buttons[1].onClick.Invoke();
                    yield return null;
                    Assert.AreEqual(LeaderboardScreen.ScreenName, meta.CurrentScreen);
                    Assert.AreEqual(LeaderboardScreen.State.SignIn, meta.CurrentLeaderboard.Current, "a guest sees the sign-in prompt");
                    Button back = null;
                    foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) if (b.name == "Back") back = b;
                    Assert.IsNotNull(back);
                    back.onClick.Invoke();
                    yield return null;
                    Assert.AreEqual(before, meta.CurrentScreen, where + ": Back returns to the screen that opened the leaderboard");
                }
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator ConsentPopupAnswersStoreOrDeclineAndNothingIsSentBeforeYes()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                PlayLbTransport net = LbServer("rows", false);
                LbState(net, true, true, false);
                meta.ShowLeaderboard(0);
                yield return null;
                Assert.IsFalse(GameSession.Progress.HasLeaderboardConsent);
                meta.ShowLeaderboardConsent();
                yield return null;
                Assert.IsNotNull(FindRt("JoinLeaderboards"), "the popup offers Join leaderboards");
                Assert.IsNotNull(FindRt("PrivacyPolicy"), "and the privacy policy");
                Button later = FindRt("NotNow").GetComponent<Button>();
                int callsBefore = net.Calls;
                later.onClick.Invoke();
                yield return null;
                Assert.IsFalse(GameSession.Progress.HasLeaderboardConsent, "Not now stores nothing");
                Assert.IsFalse(GameSession.Progress.ShouldAskLeaderboardConsent(LeaderboardTime.DayKey(DateTime.UtcNow)), "and is not asked again today");
                Assert.AreEqual(callsBefore, net.Calls, "no call to the service before the player said yes");
                meta.ShowLeaderboardConsent();
                yield return null;
                FindRt("JoinLeaderboards").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.IsTrue(GameSession.Progress.HasLeaderboardConsent, "Join stores leaderboardConsent");
                Assert.AreEqual(LeaderboardScreen.State.Ready, meta.CurrentLeaderboard.Current, "the screen shows the boards after the consent");
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator LeaderboardRowsArePooledAndScrollingDoesNotGrowThem()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", false), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null;
                LeaderboardScreen s = meta.CurrentLeaderboard;
                Assert.AreEqual(100, s.Data.Entries.Count);
                int pooled = s.PooledRows;
                Assert.Less(pooled, 30, "about a screenful of row objects, not 100");
                int objects = s.Root.GetComponentsInChildren<Transform>(true).Length;
                for (float v = 1f; v >= 0f; v -= 0.1f) s.ScrollTo(v);
                Assert.AreEqual(pooled, s.PooledRows);
                Assert.AreEqual(objects, s.Root.GetComponentsInChildren<Transform>(true).Length, "scrolling re-binds the pooled rows and creates nothing");
            }
            finally { LbCleanUp(); }
        }

        // ---------------- the redone screen (Bug List 1, 2026-11-13) ----------------

        static Transform LbFind(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(false)) if (t.name == name) return t;
            return null;
        }

        static Rect LbWorld(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
        }

        /// <summary>The visible row objects of the list.</summary>
        static List<Transform> LbRows(LeaderboardScreen s)
        {
            var rows = new List<Transform>();
            Transform content = LbFind(s.Root, "Content");
            if (content == null) return rows;
            for (int i = 0; i < content.childCount; i++) if (content.GetChild(i).gameObject.activeSelf && content.GetChild(i).name.StartsWith("Row")) rows.Add(content.GetChild(i));
            return rows;
        }

        static Transform LbRowOfRank(LeaderboardScreen s, int rank)
        {
            foreach (Transform r in LbRows(s))
            {
                Transform rt = r.Find("RankText");
                if (rt != null && rt.GetComponent<Text>().text == rank.ToString()) return r;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator LeaderboardTabsReadOverallRegionsEndlessAndAreBigEnough()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", false), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                string[] names = { "TabOverall", "TabRegions", "TabEndless" }, labels = { "Overall", "Regions", "Endless" };
                for (int i = 0; i < 3; i++)
                {
                    Transform t = LbFind(s.Root, names[i]);
                    Assert.IsNotNull(t, "the tab " + names[i]);
                    Assert.AreEqual(labels[i], t.Find("Label").GetComponent<Text>().text);
                    var rt = (RectTransform)t;
                    Assert.GreaterOrEqual(rt.rect.height, UiKit.MinTouchRef - 1f, names[i] + " height");
                    Assert.GreaterOrEqual(rt.rect.width, UiKit.MinTouchRef - 1f, names[i] + " width");
                }
                Assert.IsNull(LbFind(s.Root, "TabLevels"), "no tab is called Levels any more");
                var prizes = (RectTransform)LbFind(s.Root, "PrizesButton");
                Assert.IsNotNull(prizes);
                Assert.GreaterOrEqual(prizes.rect.height, UiKit.MinTouchRef - 1f);
                Assert.GreaterOrEqual(prizes.rect.width, UiKit.MinTouchRef - 1f);
                Assert.IsNull(prizes.GetComponentInChildren<Text>(), "the Prizes button has no text");
                Assert.AreEqual("lb_icon_gift", LbFind(prizes, "Gift").GetComponent<Image>().sprite.name);
                Assert.AreEqual("lb_plate_round", prizes.GetComponent<Image>().sprite.name);
                Assert.AreEqual("Prizes", prizes.GetComponent<A11yLabel>().Text);
                var back = (RectTransform)LbFind(meta.transform.root, "Back");
                Assert.IsNotNull(back);
                Assert.GreaterOrEqual(back.rect.height, UiKit.MinTouchRef - 1f);
                Assert.IsNotNull(LbFind(s.Root, "Clock"), "the week timer has its clock");
                Assert.AreEqual("lb_pill_timer", LbFind(s.Root, "TimerPill").Find("Plate").GetComponent<Image>().sprite.name);
                // no black backing behind the title, timer or Prizes: no dark flat image (the painted navy timer pill is a sprite of the kit)
                foreach (Image img in s.Root.GetComponentsInChildren<Image>(false))
                {
                    if (img.sprite != null && img.sprite.name.StartsWith("lb_")) continue;
                    Assert.IsFalse(img.color.a > 0.4f && img.color.r < 0.25f && img.color.g < 0.25f && img.color.b < 0.35f, "no black backing: " + img.name);
                    Assert.AreNotEqual("Backing", img.name);
                    Assert.AreNotEqual("InfoPanel", img.name);
                }
                s.SelectBoard(1);
                yield return null;
                Assert.AreEqual("Regions", LbFind(s.Root, "TabRegions").Find("Label").GetComponent<Text>().text);
                Assert.AreEqual("lb_tab_on", LbFind(s.Root, "TabRegions").GetComponent<Image>().sprite.name, "the selected tab is the golden pill");
                Assert.AreEqual("lb_tab_off", LbFind(s.Root, "TabOverall").GetComponent<Image>().sprite.name);
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator LeaderboardRowsCarryMedalsBadgesAndRankColours()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var holder = new RenderTexture[1];
            try
            {
                yield return ApplyAspect(Aspects[2], holder);   // a tall phone: 12 rows fit
                LbState(LbServer("rows", false, "pOutside", 348), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                s.ScrollTo(1f);
                Rect view = LbWorld((RectTransform)LbFind(s.Root, "Viewport"));
                for (int rank = 1; rank <= 12; rank++)
                {
                    Transform row = LbRowOfRank(s, rank);
                    Assert.IsNotNull(row, "row " + rank + " is bound");
                    string plate = rank == 1 ? "lb_row_gold" : rank == 2 ? "lb_row_silver" : rank == 3 ? "lb_row_bronze" : rank <= 10 ? "lb_row_green" : "lb_row_grey";
                    string badge = rank == 1 ? "lb_medal_gold" : rank == 2 ? "lb_medal_silver" : rank == 3 ? "lb_medal_bronze" : rank <= 10 ? "lb_badge_green" : "lb_badge_grey";
                    Assert.AreEqual(plate, row.Find("Plate").GetComponent<Image>().sprite.name, "plate of rank " + rank);
                    Assert.AreEqual(badge, row.Find("Medal").GetComponent<Image>().sprite.name, "medal or badge of rank " + rank);
                    Text num = row.Find("RankText").GetComponent<Text>();
                    Assert.IsTrue(num.gameObject.activeInHierarchy && num.text == rank.ToString(), "the rank number is always drawn on the medal or badge");
                    Assert.AreEqual("lb_icon_crown", row.Find("CrownIcon").GetComponent<Image>().sprite.name);
                    Assert.IsFalse(row.Find("Backdrop").gameObject.activeSelf, "no glow on another player's row");
                    Rect rw = LbWorld((RectTransform)row);
                    if (rank <= 10) Assert.IsTrue(rw.xMin >= view.xMin - 0.01f && rw.xMax <= view.xMax + 0.01f && rw.yMin >= view.yMin - 0.01f && rw.yMax <= view.yMax + 0.01f, "row " + rank + " sits inside the list");
                }
                // crown and number are balanced: the crown icon is about 1.4 cap heights of the number
                Transform r1 = LbRowOfRank(s, 1);
                Text crowns = r1.Find("CrownsText").GetComponent<Text>();
                var crownRt = (RectTransform)r1.Find("CrownIcon");
                float cap = crowns.fontSize * 0.72f;
                Assert.That(crownRt.rect.height, Is.InRange(cap * 1.0f, cap * 1.9f), "crown icon height vs number cap height");
                Assert.AreEqual(100f, ((RectTransform)r1).rect.height, 0.5f);
            }
            finally { LbCleanUp(); Restore(holder); }
        }

        [UnityTest]
        public IEnumerator LeaderboardOpensAroundOwnRowOrPinsOwnCardOutsideTheTop100()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                // rank 5: opens at the top, the own row is highlighted in place
                LbState(LbServer("rows", false, "p5", 5), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                Assert.AreEqual(4, s.OwnIndex);
                Assert.IsFalse(s.PinnedShown, "inside the top 100 there is no pinned card");
                Assert.AreEqual(0f, s.ScrollOffset, 0.5f, "ranks 1 to 10 open at the top");
                Transform own = LbRowOfRank(s, 5);
                Assert.IsTrue(own.Find("Backdrop").gameObject.activeSelf && own.Find("YouTag").gameObject.activeSelf, "glow outline and YOU tag on the own row");
                Assert.AreEqual("lb_row_green", own.Find("Plate").GetComponent<Image>().sprite.name, "the own row keeps its rank colour");
                Assert.AreEqual("YOU", own.Find("YouTag").Find("TagText").GetComponent<Text>().text);

                // rank 37: the list opens scrolled so the own row is visible near the middle
                LbState(LbServer("rows", false, "p37", 37), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                s = meta.CurrentLeaderboard;
                Assert.AreEqual(36, s.OwnIndex);
                Assert.Greater(s.ScrollOffset, 100f, "scrolled down to the own row");
                own = LbRowOfRank(s, 37);
                Assert.IsNotNull(own, "the own row is one of the bound rows");
                Assert.IsTrue(own.Find("Backdrop").gameObject.activeSelf);
                Rect view = LbWorld((RectTransform)LbFind(s.Root, "Viewport")), rw = LbWorld((RectTransform)own);
                Assert.IsTrue(rw.yMin >= view.yMin - 0.01f && rw.yMax <= view.yMax + 0.01f, "the own row is fully visible");
                Assert.AreEqual(view.center.y, rw.center.y, view.height * 0.12f, "and about centred");

                // outside the top 100: the list opens at the top, a pinned card shows the real rank inside the safe area
                LbState(LbServer("rows", false, "pOutside", 348), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                s = meta.CurrentLeaderboard;
                Assert.AreEqual(-1, s.OwnIndex);
                Assert.IsTrue(s.PinnedShown);
                Assert.AreEqual("#348", s.PinnedRankText);
                Assert.AreEqual(0f, s.ScrollOffset, 0.5f);
                Transform card = LbFind(s.Root, "OwnCard");
                Assert.IsNotNull(card);
                Assert.AreEqual("#348", card.Find("RankText").GetComponent<Text>().text);
                Assert.AreEqual("lb_row_you", card.Find("Plate").GetComponent<Image>().sprite.name);
                Rect cw = LbWorld((RectTransform)card), root = LbWorld(s.Root), vw = LbWorld((RectTransform)LbFind(s.Root, "Viewport"));
                Assert.IsTrue(cw.xMin >= root.xMin && cw.xMax <= root.xMax && cw.yMin >= root.yMin && cw.yMax <= root.yMax, "the pinned card is inside the screen");
                Assert.LessOrEqual(cw.yMax, vw.yMin + 0.01f, "the card sits under the scroll area, not over it");
                foreach (Transform r in LbRows(s)) Assert.IsFalse(r.Find("Backdrop").gameObject.activeSelf, "no row is highlighted");

                // no crowns and no rank yet: no card
                LbState(LbServer("rows", false, "pNew", 0), true, true, true);
                meta.ShowLeaderboard(2);   // the endless board of the stub has rank 0 and 0 crowns
                yield return null; yield return null;
                Assert.IsFalse(meta.CurrentLeaderboard.PinnedShown, "a player without crowns gets no card");
            }
            finally { LbCleanUp(); }
        }
    }
}
