using System;
using System.Collections.Generic;
using System.Globalization;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The Subscription page (docs/26 section 2): a full page in the shop look (Momo shopkeeper scene, brown background, ribbon title) with two
    /// big cards, WEEKLY (blue) and MONTHLY (gold/red). Each card shows the ticket icon, the purchase reward, the privileges, the daily rewards,
    /// the "worth about N x" text and the price button; an active plan also shows the days left and today's Claim button (or "Come back tomorrow"
    /// with a countdown to local midnight). Purchases are TEST purchases in development builds (ShopScreen.TestPurchasesEnabled), "Not available
    /// yet" in release builds. Menu screen only. The card content sits in layout groups, so a larger text size pushes the next row down instead of
    /// overlapping it (CUX-3, CUX-5).
    /// Ticket art: Resources/Art/Ui/icon_ticket_weekly.png and icon_ticket_monthly.png (new OpenArt art, arrives later); until then the crown icon.
    /// </summary>
    public sealed class SubscriptionScreen
    {
        const float HeaderH = 520f;                     // painted shop scene, same as the shop
        const float ListTop = HeaderH + 130f;           // below the ribbon title
        const float BottomMargin = ToyKit.TabBarReserve + 4f;
        const float ButtonH = 130f;

        static readonly Color WeeklyPlate = new Color(0.15f, 0.36f, 0.78f, 1f);       // white title on it: about 6:1
        static readonly Color MonthlyPlate = new Color(0.66f, 0.14f, 0.18f, 1f);      // white title on it: about 8:1
        static readonly Color WeeklyBody = new Color(0.84f, 0.92f, 1f, 1f);
        static readonly Color MonthlyBody = new Color(1f, 0.93f, 0.72f, 1f);
        static readonly Color WorthColor = new Color(0.02f, 0.36f, 0.19f, 1f);        // deep green on the pale card, above 6:1
        static readonly Color DisabledTint = new Color(0.66f, 0.66f, 0.70f, 1f);

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly SubscriptionCatalog catalog;
        readonly ArtCatalog art;
        readonly ShopScreen.Host host;
        RectTransform body;
        readonly List<Text> countdowns = new List<Text>();
        bool shownActive, shownCanClaim;

        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }
        public Text StarText { get; private set; }

        public SubscriptionScreen(RectTransform screen, ProgressService progress, SubscriptionCatalog catalog, ArtCatalog art, ShopScreen.Host host)
        {
            this.screen = screen;
            this.progress = progress;
            this.catalog = catalog ?? SubscriptionCatalog.BuiltIn();
            this.art = art;
            this.host = host;
        }

        static string Num(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        static void DropOutline(Text t)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }

        // ---------------- build ----------------

        public void Build()
        {
            BuildChrome();
            RebuildBody();
        }

        /// <summary>
        /// SWAP POINT for the shared shop chrome (ShopChrome by helper B1): the Momo scene across the top and the wallet pills over it.
        /// Built here with the same pieces as ShopScreen.BuildHeader until that file exists; replace the body of this method by a ShopChrome call.
        /// </summary>
        void BuildChrome()
        {
            RectTransform scene = UiKit.Rect(screen, "Scene");
            scene.anchorMin = new Vector2(0f, 1f);
            scene.anchorMax = new Vector2(1f, 1f);
            scene.pivot = new Vector2(0.5f, 1f);
            scene.anchoredPosition = Vector2.zero;
            scene.sizeDelta = new Vector2(0f, HeaderH);
            scene.gameObject.AddComponent<RectMask2D>();
            Sprite sc = ToyKit.Get("shop_scene");
            Image si = UiKit.AddImage(scene, "SceneArt", sc, sc != null ? Color.white : Theme.Sky);
            si.preserveAspect = false;
            UiKit.Stretch(si.rectTransform);
            if (sc != null)
            {
                var fit = si.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = sc.rect.width / sc.rect.height;
            }

            RectTransform row = UiKit.Rect(screen, "Wallet");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(190f, -118f);
            row.offsetMax = new Vector2(-16f, -18f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            CoinText = ToyKit.WalletPillFlex(row, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, 100f);
            GemText = ToyKit.WalletPillFlex(row, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), 100f);
            StarText = ToyKit.WalletPillFlex(row, "StarsPill", art != null ? art.StarFilled : null, new Color(1f, 0.92f, 0.5f), 100f);
        }

        int TotalStars()
        {
            int stars = 0;
            for (int i = 1; i <= GameSession.PlayableRegions; i++)
            {
                RegionDef r = GameSession.Repo != null ? GameSession.Repo.TryGetRegionByIndex(i) : null;
                if (r != null) stars += progress.StarsInRegion(r);
            }
            return stars;
        }

        void RebuildBody()
        {
            if (body != null)
            {
                body.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(body.gameObject);
            }
            countdowns.Clear();
            body = UiKit.Rect(screen, "SubscriptionBody");
            UiKit.Stretch(body);
            ToyKit.Ribbon(body, "Subscription", new Vector2(0.5f, 1f), new Vector2(0f, -(HeaderH - 70f)), new Vector2(700f, 206f));
            shownActive = progress.IsActive();
            shownCanClaim = progress.CanClaimToday();
            BuildCards();
            var ticker = body.gameObject.AddComponent<Ticker>();
            ticker.Tick = OnSecond;
            if (CoinText != null) UiKit.SetText(CoinText, progress.Coins.ToString());
            if (GemText != null) UiKit.SetText(GemText, progress.Gems.ToString());
            if (StarText != null) UiKit.SetText(StarText, TotalStars().ToString());
        }

        /// <summary>Once per second (menu only): refreshes the countdown, and rebuilds the page when the state changed (midnight passed, the plan ended).</summary>
        void OnSecond()
        {
            if (progress.IsActive() != shownActive || progress.CanClaimToday() != shownCanClaim) { RebuildBody(); return; }
            if (countdowns.Count == 0) return;
            string text = "Next reward in " + Countdown(progress.TimeToNextClaim(progress.SubscriptionClock()));
            for (int i = 0; i < countdowns.Count; i++) if (countdowns[i] != null) UiKit.SetText(countdowns[i], text);
        }

        sealed class Ticker : MonoBehaviour
        {
            public Action Tick;
            float next;

            void Update()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + 1f;
                if (Tick != null) Tick();
            }
        }

        /// <summary>"05:12:09" (hours:minutes:seconds; the span to the next local midnight is under a day).</summary>
        public static string Countdown(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            int h = (int)span.TotalHours;
            return h.ToString("00", CultureInfo.InvariantCulture) + ":" + span.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + span.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        // ---------------- cards ----------------

        void BuildCards()
        {
            RectTransform vp = UiKit.Rect(body, "SubViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, BottomMargin);
            vp.offsetMax = new Vector2(0f, -ListTop);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // gives the ScrollRect something to drag on between the cards
            RectTransform list = UiKit.Rect(vp, "SubList");
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(28, 28, 10, 40);
            v.spacing = 48f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = list.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = list;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;

            for (int i = 0; i < catalog.All.Count; i++) BuildCard(list, catalog.All[i]);
        }

        static void IgnoreLayout(RectTransform host)
        {
            for (int i = 0; i < host.childCount; i++) host.GetChild(i).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        static VerticalLayoutGroup Stack(GameObject go, int padX, int padTop, int padBottom, float spacing)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(padX, padX, padTop, padBottom);
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;
            return v;
        }

        static void Size(GameObject go, float minW, float minH, float prefW = -1f, float prefH = -1f)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minWidth = minW;
            le.minHeight = minH;
            if (prefW >= 0f) le.preferredWidth = prefW;
            if (prefH >= 0f) le.preferredHeight = prefH;
        }

        Text Line(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool display = false)
        {
            Text t = UiKit.AddText(parent, name, text, size, color, align, display);
            DropOutline(t);
            return t;
        }

        void BuildCard(RectTransform list, SubscriptionDef def)
        {
            bool weekly = def.Id == SubscriptionCatalog.Weekly;
            string word = weekly ? "WEEKLY" : "MONTHLY";
            DateTime now = progress.SubscriptionClock();
            bool active = progress.IsActive(def.Id, now);

            RectTransform card = UiKit.Rect(list, "Card_" + def.Id);
            Image surface = Theme.CardBacking(card);
            surface.color = weekly ? WeeklyBody : MonthlyBody;
            IgnoreLayout(card);   // the shadow and the surface are backdrops, not rows
            Stack(card.gameObject, 24, 24, 28, 16f);

            // Title plate: name on the left, ticket icon on the right.
            Image plate = UiKit.AddPanel(card, "Header", weekly ? WeeklyPlate : MonthlyPlate);
            Size(plate.gameObject, 0f, 220f);
            var ph = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
            ph.padding = new RectOffset(32, 16, 10, 10);
            ph.spacing = 12f;
            ph.childAlignment = TextAnchor.MiddleLeft;
            ph.childControlWidth = ph.childControlHeight = true;
            ph.childForceExpandWidth = false;
            ph.childForceExpandHeight = false;
            RectTransform titles = UiKit.Rect(plate.transform, "Titles");
            Stack(titles.gameObject, 0, 0, 0, 2f).childAlignment = TextAnchor.MiddleLeft;
            Size(titles.gameObject, 200f, 0f);
            titles.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Text big = UiKit.AddText(titles, "Name", word, 72, Color.white, TextAnchor.MiddleLeft, true);
            big.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text sub = UiKit.AddText(titles, "Kind", "Subscription", 42, Color.white, TextAnchor.MiddleLeft, true);
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            Sprite ticket = ToyKit.Get(weekly ? "icon_ticket_weekly" : "icon_ticket_monthly");
            bool fallback = ticket == null;
            if (fallback) ticket = ToyKit.Get("icon_crown");
            Image ti = UiKit.AddImage(plate.transform, "Ticket", ticket, fallback && weekly ? new Color(0.78f, 0.9f, 1f, 1f) : Color.white);
            ti.preserveAspect = true;
            Size(ti.gameObject, 190f, 190f, 190f, 190f);

            // Purchase reward.
            Line(card, "PurchaseLabel", "Purchase reward", 40, Theme.InkSoft);
            RectTransform buyRow = ChipRow(card, "PurchaseChips");
            Chip(buyRow, GemIcon, Num(def.InstantGems), "gems");

            // Privileges.
            Line(card, "PrivilegeLabel", "Privileges", 40, Theme.InkSoft);
            Text p1 = Line(card, "Privilege1", "Daily gems and coins every day", 42, Theme.Ink);
            p1.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (def.PowerUpEveryClaims > 0)
            {
                Text p2 = Line(card, "Privilege2", "Free power-up every " + def.PowerUpEveryClaims + " days", 42, Theme.Ink);
                p2.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            // Daily rewards.
            Line(card, "DailyLabel", "Daily Rewards", 40, Theme.InkSoft);
            RectTransform dailyRow = ChipRow(card, "DailyChips");
            Chip(dailyRow, GemIcon, Num(def.DailyGems), "gems");
            Chip(dailyRow, CoinIcon, Num(def.DailyCoins), "coins");
            if (def.PowerUpEveryClaims > 0) Chip(dailyRow, ToyKit.Get("icon_bolt"), "x" + def.PowerUpCount, "free power-up every " + def.PowerUpEveryClaims + " days");

            Text worth = Line(card, "Worth", SubscriptionCatalog.WorthText(def), 50, WorthColor, TextAnchor.MiddleCenter, true);
            worth.horizontalOverflow = HorizontalWrapMode.Wrap;

            if (active)
            {
                int days = progress.DaysLeft(now);
                Line(card, "DaysLeft", days + (days == 1 ? " day left" : " days left"), 44, Theme.Ink, TextAnchor.MiddleCenter, true);
                BuildClaim(card);
            }

            string price = SubscriptionCatalog.PriceText(def);
            string planName = def.Name;
            bool otherActive = progress.IsActive(now) && !active;
            string a11y = "Buy " + planName + " for " + SubscriptionCatalog.AmountText(def) + ", " + def.Days + " days" + (ShopScreen.TestPurchasesEnabled ? ", test purchase" : ", not available yet") + (active ? ", adds time" : "");
            string planId = def.Id;
            Button buy = UiKit.AddButton(card, "Buy", price, ToyKit.Button(1), otherActive ? DisabledTint : Color.white, new Vector2(400f, ButtonH), () => TapBuy(planId), 46);
            buy.GetComponent<A11yLabel>().Text = a11y;
            buy.transform.Find("Label").GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            Size(buy.gameObject, 0f, ButtonH, -1f, ButtonH);
            if (ShopScreen.TestPurchasesEnabled)
            {
                Text note = Line(card, "TestNote", "Test purchase: no money is charged.", 32, Theme.InkSoft, TextAnchor.MiddleCenter);
                note.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }

        /// <summary>Today's reward: a green Claim button, or the disabled "Come back tomorrow" with the countdown to local midnight.</summary>
        void BuildClaim(RectTransform card)
        {
            if (progress.CanClaimToday())
            {
                Button claim = UiKit.AddButton(card, "Claim", "Claim", ToyKit.Button(1), Color.white, new Vector2(400f, ButtonH), TapClaim, 48);
                claim.GetComponent<A11yLabel>().Text = "Claim today's subscription reward";
                Size(claim.gameObject, 0f, ButtonH, -1f, ButtonH);
                return;
            }
            Button wait = UiKit.AddButton(card, "ComeBack", "Come back tomorrow", ToyKit.Button(0), DisabledTint, new Vector2(400f, ButtonH), null, 44, false);
            wait.interactable = false;
            wait.GetComponent<A11yLabel>().Text = "Today's reward is claimed. Come back tomorrow.";
            wait.transform.Find("Label").GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            Size(wait.gameObject, 0f, ButtonH, -1f, ButtonH);
            Text count = Line(card, "Countdown", "Next reward in " + Countdown(progress.TimeToNextClaim(progress.SubscriptionClock())), 38, Theme.InkSoft, TextAnchor.MiddleCenter);
            countdowns.Add(count);
        }

        Sprite GemIcon { get { return art != null && art.GemIcon != null ? art.GemIcon : ToyKit.Get("icon_gem"); } }
        Sprite CoinIcon { get { return art != null && art.CoinIcon != null ? art.CoinIcon : ToyKit.Get("icon_coin"); } }

        static RectTransform ChipRow(Transform parent, string name)
        {
            RectTransform row = UiKit.Rect(parent, name);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return row;
        }

        /// <summary>A framed square with the item icon above its count (as the reward chips of the bundle cards).</summary>
        void Chip(RectTransform row, Sprite icon, string count, string what)
        {
            Image frame = UiKit.AddPanel(row, "Chip", new Color(1f, 1f, 1f, 0.9f));
            Stack(frame.gameObject, 14, 12, 10, 4f);
            Size(frame.gameObject, 170f, 0f);
            Image ic = UiKit.AddImage(frame.transform, "Icon", icon, Color.white);
            ic.preserveAspect = true;
            Size(ic.gameObject, 0f, 92f, -1f, 92f);
            Text t = UiKit.AddText(frame.transform, "Count", count, 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(t);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            frame.gameObject.AddComponent<A11yLabel>().Text = count + " " + what;
        }

        // ---------------- actions ----------------

        void Info(string title, string text)
        {
            host.OpenPopup(title, text, 900f);
            host.PopupButton("OK", false, 40f, host.ClosePopup);
        }

        /// <summary>Tap on a plan's price button. Release builds: "Not available yet". Development builds: confirm, then a TEST purchase.</summary>
        public void TapBuy(string planId)
        {
            SubscriptionDef def = catalog.Get(planId);
            if (def == null) return;
            if (!ShopScreen.TestPurchasesEnabled)
            {
                Info("Not available yet", "Buying a subscription with real money is not open yet. It arrives with the store release.");
                return;
            }
            string running = progress.ActiveSubscription();
            if (running != null && running != planId)
            {
                SubscriptionDef other = catalog.Get(running);
                Info("Another plan is running", "Your " + (other != null ? other.Name : "subscription") + " is still active. You can buy this plan after it ends.");
                return;
            }
            string extend = running != null ? "\nThis adds " + def.Days + " days to your current time." : "";
            host.OpenPopup("Confirm test buy", "TEST PURCHASE\n" + def.Name + " for " + SubscriptionCatalog.AmountText(def) + ".\nYou get " + Num(def.InstantGems) + " gems now and a daily reward for " + def.Days + " days." + extend + "\nNo money is charged.", 1180f);
            host.PopupButton("Confirm", true, 230f, () => CompleteBuy(def));
            host.PopupButton("Cancel", false, 60f, host.ClosePopup);
        }

        void CompleteBuy(SubscriptionDef def)
        {
            host.ClosePopup();
            switch (progress.GrantTestSubscriptionPurchase(def.Id, ShopScreen.TestPurchasesEnabled))
            {
                case SubscriptionBuyResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    RebuildBody();   // days left, Claim button and the wallet follow
                    Info("Subscription active", "+" + Num(def.InstantGems) + " gems added.\n" + def.Name + ": " + progress.DaysLeft() + " days left.\nClaim your reward every day.\nThis was a test purchase.");
                    break;
                case SubscriptionBuyResult.NotAvailableYet:
                    Info("Not available yet", "Buying a subscription with real money is not open yet. It arrives with the store release.");
                    break;
                case SubscriptionBuyResult.OtherPlanActive:
                    Info("Another plan is running", "Your current subscription is still active. You can buy this plan after it ends.");
                    break;
                default:
                    Info("Not available", def.Name + " cannot be bought right now.");
                    break;
            }
        }

        /// <summary>Tap on Claim: pays today's reward (once per local day) and tells what was added.</summary>
        public void TapClaim()
        {
            SubscriptionClaim claim = progress.ClaimDaily();
            switch (claim.Result)
            {
                case SubscriptionClaimResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    RebuildBody();
                    string text = "+" + Num(claim.Gems) + " gems\n+" + Num(claim.Coins) + " coins";
                    if (claim.PowerUpId != null)
                    {
                        PowerUpDef pw = progress.PowerUpDefs.Get(claim.PowerUpId);
                        text += "\n+" + claim.PowerUpCount + " " + (pw != null ? pw.Name : "power-up") + " (free power-up)";
                    }
                    Info("Reward claimed", text + "\nCome back tomorrow for the next one.");
                    break;
                case SubscriptionClaimResult.AlreadyClaimedToday:
                    RebuildBody();
                    Info("Already claimed", "Today's reward is already yours. Come back tomorrow.");
                    break;
                default:
                    RebuildBody();
                    Info("Subscription ended", "Your subscription is not active. Buy it again to get daily rewards.");
                    break;
            }
        }
    }
}
