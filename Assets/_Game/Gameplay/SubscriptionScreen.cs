using System;
using System.Collections.Generic;
using System.Globalization;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The VIP page (docs/26 section 2; the owner renamed the Subscription screen to VIP, issue list 30 item 4). A full page in the shop look
    /// (ShopChrome: Momo scene, wood background, yellow italic ribbon "VIP") whose body is a LIST OF SECTIONS, each a header plus content (add a section by
    /// calling BuildSection in BuildSections). The first section is "Subscription": ONE card per plan, four in all (owner 2026-11-13: Weekly Starter and Weekly Plus in blue,
    /// Monthly Standard and Monthly Premium in gold; Plus and Premium carry a BEST VALUE line): ticket icon top left, plan name and Worth badge, colour-coded benefit lines
    /// that say how many days each repeats, two totals lines and, as the last row, the PAY button with the price (an active plan shows the days left and a Claim button,
    /// or the countdown to local midnight, instead). Every text is one line at the 16 sp floor and the card height follows the line count, so the four cards are taller than
    /// a phone screen: the VIP page scrolls (ShopChrome.ScrollArea end padding keeps the last button clear of the bottom bar). Purchases are TEST purchases in development builds
    /// (ShopScreen.TestPurchasesEnabled), "Not available yet" in release builds. Menu screen only.
    /// Ticket art: Resources/Art/Ui/icon_ticket_weekly.png and icon_ticket_monthly.png; the crown icon until they exist.
    /// </summary>
    public sealed class SubscriptionScreen
    {
        /// <summary>The page title (ribbon, tab caption, accessibility labels).</summary>
        public const string PageTitle = "VIP";
        /// <summary>The first section header, under the title.</summary>
        public const string SubscriptionSectionTitle = "Subscription";

        const float CardMinH = 232f, CardPad = 12f, LineGap = 6f, SectionGap = 12f, CardGap = 10f;
        /// <summary>The 16 sp floor in pixels; every card text uses it (the card height is fixed from it, so 130% text cannot overlap).</summary>
        static readonly int Fs = Mathf.CeilToInt(UiKit.MinBodyTextPx);
        static readonly int NameFs = Fs + 6;
        static readonly float LineH = Mathf.Ceil(Fs * 1.3f), NameRowH = Mathf.Ceil((Fs + 6) * 1.3f);
        static readonly Color HeaderColor = new Color(1f, 0.86f, 0.30f, 1f);          // yellow on the wood: above 8:1
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

        static string Colored(string text, Color c) { return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>"; }

        // ---------------- build ----------------

        public void Build()
        {
            Text coin, gem, star;
            ShopChrome.BuildScene(screen, art, null, out coin, out gem, out star);
            CoinText = coin;
            GemText = gem;
            StarText = star;
            RebuildBody();
        }

        void RebuildBody()
        {
            if (body != null)
            {
                body.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(body.gameObject);
            }
            countdowns.Clear();
            RectTransform content = ShopChrome.BuildBody(screen, PageTitle, ShopChrome.RibbonColor.Yellow);
            body = (RectTransform)content.parent;
            shownActive = progress.IsActive();
            shownCanClaim = progress.CanClaimToday();
            BuildSections(content);
            var ticker = body.gameObject.AddComponent<Ticker>();
            ticker.Tick = OnSecond;
            if (CoinText != null) UiKit.SetText(CoinText, progress.Coins.ToString());
            if (GemText != null) UiKit.SetText(GemText, progress.Gems.ToString());
            if (StarText != null) UiKit.SetText(StarText, ShopChrome.TotalStars(progress).ToString());
        }

        /// <summary>Once per second (menu only): refreshes the countdown, and rebuilds the page when the state changed (midnight passed, the plan ended).</summary>
        void OnSecond()
        {
            if (progress.IsActive() != shownActive || progress.CanClaimToday() != shownCanClaim) { Refresh(); return; }
            if (countdowns.Count == 0) return;
            string text = Countdown(progress.TimeToNextClaim(progress.SubscriptionClock()));
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

        // ---------------- sections ----------------

        /// <summary>
        /// The page is a vertical list of sections under the ribbon. ADD A NEW VIP SECTION HERE: one more BuildSection(content, "Title", fill) call,
        /// where fill adds the section's rows to the rect it is given (rows are layout children: give them a LayoutElement min/preferred height).
        /// </summary>
        void BuildSections(RectTransform content)
        {
            var list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = SectionGap;
            list.padding = new RectOffset(0, 0, 4, 4);
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            list.childAlignment = TextAnchor.UpperCenter;
            BuildSection(content, SubscriptionSectionTitle, BuildPlanCards);
        }

        // ---------------- embedded in the shop's VIP tab (owner issue 35, item 2) ----------------

        Action embeddedRebuild;

        /// <summary>
        /// Builds the "Subscription" section (header + the plan cards) into <paramref name="sectionsParent"/>, a vertical layout of the shop's VIP page,
        /// instead of a page of its own. <paramref name="rebuild"/> is called when the content has to be rebuilt (a purchase, a claim, midnight passed): the
        /// shop then rebuilds the whole VIP page. Returns the section rect (the page scrolls to it).
        /// </summary>
        public RectTransform BuildEmbedded(RectTransform sectionsParent, Action rebuild)
        {
            embeddedRebuild = rebuild;
            countdowns.Clear();
            shownActive = progress.IsActive();
            shownCanClaim = progress.CanClaimToday();
            RectTransform sec = NewSection(sectionsParent, SubscriptionSectionTitle, false);   // the page's yellow "Subscription" banner is the title
            BuildPlanCards(sec);
            var ticker = sec.gameObject.AddComponent<Ticker>();
            ticker.Tick = OnSecond;
            return sec;
        }

        void Refresh()
        {
            if (embeddedRebuild != null) embeddedRebuild();
            else RebuildBody();
        }

        void BuildSection(RectTransform parent, string title, Action<RectTransform> fill)
        {
            fill(NewSection(parent, title));
        }

        /// <summary>A section of the VIP page: a vertical layout holding a yellow italic header (children after it are the rows). Shared with the Chests section.</summary>
        public static RectTransform NewSection(RectTransform parent, string title, bool header = true)
        {
            RectTransform sec = UiKit.Rect(parent, "Section_" + title);
            var v = sec.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = CardGap;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            if (!header) return sec;   // inside the shop the yellow page / section banner is the title
            Text head = UiKit.AddText(sec, "Header", title, 48, HeaderColor, TextAnchor.MiddleLeft, true);
            head.fontStyle = FontStyle.Italic;
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.gameObject.AddComponent<A11yLabel>().Text = title + " section";
            var hle = head.gameObject.AddComponent<LayoutElement>();
            hle.minHeight = 60f;
            hle.preferredHeight = 60f + Mathf.Max(0f, UiKit.TextScale - 1f) * 40f;
            return sec;
        }

        void BuildPlanCards(RectTransform section)
        {
            for (int i = 0; i < catalog.All.Count; i++) BuildCard(section, catalog.All[i]);
        }

        /// <summary>A single-line text at a fixed pixel size (no best fit, no wrap): its box is its line height.</summary>
        static Text Plain(Text t, TextAnchor align, int px)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = false;
            t.fontSize = px;
            return t;
        }

        /// <summary>Button labels use the same floor size, so a 130% text size cannot push them out of the button.</summary>
        static void FixLabel(Button b)
        {
            Text l = b.transform.Find("Label").GetComponent<Text>();
            l.resizeTextForBestFit = false;
            l.fontSize = Fs;
        }

        static void Row(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = h;
            le.preferredHeight = h;
        }

        static void Rows(Text t, float x0, float x1, float y0, float y1)
        {
            RectTransform r = t.rectTransform;
            r.anchorMin = new Vector2(x0, y0);
            r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// One plan: a tall card. Left: the ticket. Right, ONE vertical stack of single-line texts at the 16 sp floor (name and worth, an optional BEST VALUE line, the benefit lines
        /// with how many days each repeats, two totals lines) and, as its last row, the action row: the Pay button (or days left + Claim) at least as tall as UiKit.MinTouchRef.
        /// The card height is computed from the line count, so there is no overlap by construction; the VIP page scrolls (ShopChrome.ScrollArea end padding).
        /// </summary>
        void BuildCard(RectTransform section, SubscriptionDef def)
        {
            bool monthly = def.IsMonthly;
            bool gold = monthly;
            DateTime now = progress.SubscriptionClock();
            bool active = progress.IsActive(def.Id, now);

            string puLine = SubscriptionCatalog.PowerUpLine(def);
            string totals2 = SubscriptionCatalog.TotalsLine2(def);
            int lineCount = 3 + (puLine != null ? 1 : 0) + def.Chests.Count + 1 + (totals2 != null ? 1 : 0) + (def.Featured ? 1 : 0);   // now, gems, coins, [power-up], [chests], total 1, [total 2], [badge]
            float buttonH = Mathf.Max(ShopChrome.PriceButtonH, UiKit.MinTouchRef);
            float cardH = Mathf.Max(CardMinH, CardPad * 2f + NameRowH + lineCount * (LineH + LineGap) + LineGap + buttonH);

            RectTransform card = ShopChrome.Card(section, "Card_" + def.Id, gold ? ShopChrome.FrameColor.Gold : ShopChrome.FrameColor.Blue);
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.minHeight = cardH;
            le.preferredHeight = cardH;

            Color nameCol = gold ? new Color(0.33f, 0.14f, 0.02f) : new Color(0.82f, 0.93f, 1f);
            Color baseCol = gold ? new Color(0.20f, 0.08f, 0.02f) : Color.white;
            Color gems = gold ? new Color(0.05f, 0.25f, 0.75f) : new Color(0.62f, 0.88f, 1f);
            Color coins = gold ? new Color(0.45f, 0.24f, 0.0f) : new Color(1f, 0.86f, 0.30f);
            Color power = gold ? new Color(0.02f, 0.36f, 0.12f) : new Color(0.45f, 0.98f, 0.50f);
            Color chestCol = gold ? new Color(0.50f, 0.10f, 0.05f) : new Color(1f, 0.74f, 0.45f);

            // ticket icon (top left): weekly plans use the weekly ticket, monthly plans the monthly one
            Sprite ticket = ToyKit.Get(monthly ? "icon_ticket_monthly" : "icon_ticket_weekly");
            bool fallback = ticket == null;
            if (fallback) ticket = ToyKit.Get("icon_crown");
            Image ti = UiKit.AddImage(card, "Ticket", ticket, fallback && !monthly ? new Color(0.78f, 0.9f, 1f, 1f) : Color.white);
            ti.preserveAspect = true;
            ti.raycastTarget = false;
            ti.rectTransform.anchorMin = new Vector2(0.015f, 0.55f);
            ti.rectTransform.anchorMax = new Vector2(0.15f, 0.95f);
            ti.rectTransform.offsetMin = ti.rectTransform.offsetMax = Vector2.zero;

            RectTransform stack = UiKit.Rect(card, "Lines");
            stack.anchorMin = new Vector2(0.16f, 0f);
            stack.anchorMax = new Vector2(0.985f, 1f);
            stack.offsetMin = new Vector2(0f, CardPad);
            stack.offsetMax = new Vector2(0f, -CardPad);
            var vg = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            vg.spacing = LineGap;
            vg.childAlignment = TextAnchor.UpperLeft;
            vg.childControlWidth = vg.childControlHeight = true;
            vg.childForceExpandWidth = true;
            vg.childForceExpandHeight = false;

            RectTransform head = UiKit.Rect(stack, "NameRow");
            var hg = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            hg.spacing = 8f;
            hg.childAlignment = TextAnchor.MiddleLeft;
            hg.childControlWidth = hg.childControlHeight = true;
            hg.childForceExpandWidth = hg.childForceExpandHeight = false;
            Row(head.gameObject, NameRowH);
            Text name = Plain(UiKit.AddText(head, "Name", def.Name.ToUpperInvariant(), 48, nameCol, TextAnchor.MiddleLeft, true), TextAnchor.MiddleLeft, NameFs);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Plain(UiKit.AddText(head, "Worth", SubscriptionCatalog.WorthText(def).Replace("about ", ""), 42, gold ? new Color(0.02f, 0.36f, 0.19f) : power, TextAnchor.MiddleRight, true), TextAnchor.MiddleRight, Fs);

            if (def.Featured) AddLine(stack, "Badge", Colored("BEST VALUE", gold ? new Color(0.60f, 0.10f, 0.05f) : new Color(1f, 0.86f, 0.30f)), baseCol, true);
            AddLine(stack, "Now", Colored(SubscriptionCatalog.NowLine(def), gems), baseCol, false);
            AddLine(stack, "DailyGems", Colored(SubscriptionCatalog.DailyGemsLine(def), gems), baseCol, false);
            AddLine(stack, "DailyCoins", Colored(SubscriptionCatalog.DailyCoinsLine(def), coins), baseCol, false);
            if (puLine != null) AddLine(stack, "PowerUps", Colored(puLine, power), baseCol, false);
            for (int c = 0; c < def.Chests.Count; c++) AddLine(stack, "Chest" + (c + 1), Colored(SubscriptionCatalog.ChestLine(def.Chests[c]), chestCol), baseCol, false);
            AddLine(stack, "Total1", SubscriptionCatalog.TotalsLine1(def), baseCol, true);
            if (totals2 != null) AddLine(stack, "Total2", totals2, baseCol, true);

            card.gameObject.AddComponent<A11yLabel>().Text = SubscriptionCatalog.Describe(def);

            // action row (last row of the stack): left days left, right the Pay or Claim button
            RectTransform act = UiKit.Rect(stack, "ActionRow");
            var ag = act.gameObject.AddComponent<HorizontalLayoutGroup>();
            ag.spacing = 8f;
            ag.childAlignment = TextAnchor.MiddleRight;
            ag.childControlWidth = ag.childControlHeight = true;
            ag.childForceExpandWidth = ag.childForceExpandHeight = false;
            Row(act.gameObject, buttonH);
            Button b;
            if (active)
            {
                int days = progress.DaysLeft(now);
                Text left = Plain(UiKit.AddText(act, "DaysLeft", days + (days == 1 ? " day left" : " days left"), 44, baseCol, TextAnchor.MiddleLeft, true), TextAnchor.MiddleLeft, Fs);
                left.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                b = BuildClaim(act);
            }
            else
            {
                Text pad = Plain(UiKit.AddText(act, "Spacer", " ", 42, baseCol, TextAnchor.MiddleLeft), TextAnchor.MiddleLeft, Fs);
                pad.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                string planId = def.Id;
                bool otherActive = progress.IsActive(now);
                string amount = StoreLink.StorePrice(ProductCatalog.ForSubscription(def.Id)) ?? SubscriptionCatalog.AmountText(def);   // the store localised price once loaded
                string a11y = "Pay " + amount + " for " + def.Name + ", " + def.Days + " days" + (ShopScreen.TestPurchasesEnabled ? ", test purchase" : "");
                b = ShopChrome.PriceButton(act, "Buy", 1, null, PayLabel(amount), a11y, () => TapBuy(planId), 40);
                FixLabel(b);
                if (otherActive) b.GetComponent<Image>().color = DisabledTint;
            }
            var ble = b.gameObject.AddComponent<LayoutElement>();
            ble.minWidth = ble.preferredWidth = ButtonW;
            ble.minHeight = ble.preferredHeight = buttonH;
        }

        const float ButtonW = 360f;

        /// <summary>The owner wording: the button says Pay and the price ("PAY $4.99"); the days are on the card text.</summary>
        public static string PayLabel(string amount) { return "PAY " + amount; }

        /// <summary>One single-line card text (rich text for the colour runs).</summary>
        static void AddLine(RectTransform stack, string name, string txt, Color baseCol, bool bold)
        {
            Text line = Plain(UiKit.AddText(stack, name, txt, 42, baseCol, TextAnchor.MiddleLeft), TextAnchor.MiddleLeft, Fs);
            line.supportRichText = true;
            if (bold) line.fontStyle = FontStyle.Bold;
            Row(line.gameObject, LineH);
        }

        /// <summary>Today's reward: a green Claim button, or a disabled button showing the countdown to local midnight.</summary>
        Button BuildClaim(RectTransform parent)
        {
            Button b;
            if (progress.CanClaimToday())
            {
                b = ShopChrome.PriceButton(parent, "Claim", 1, null, "Claim", "Claim today's VIP subscription reward", TapClaim, 44);
            }
            else
            {
                string time = Countdown(progress.TimeToNextClaim(progress.SubscriptionClock()));
                b = ShopChrome.PriceButton(parent, "ComeBack", 0, null, time, "Today's reward is claimed. Come back tomorrow.", null, 40);
                b.interactable = false;
                b.GetComponent<Image>().color = DisabledTint;
                countdowns.Add(b.transform.Find("Label").GetComponent<Text>());
            }
            FixLabel(b);
            return b;
        }

        // ---------------- actions ----------------

        void Info(string title, string text)
        {
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            host.OpenPopup(title, text, Mathf.Max(900f, 520f + 62f * lines));   // a claim that opened chests lists their contents: the popup grows with the lines
            host.PopupButton("OK", false, 40f, host.ClosePopup);
        }

        /// <summary>Tap on a plan's price button. Release builds: "Not available yet". Development builds: confirm, then a TEST purchase.</summary>
        public void TapBuy(string planId)
        {
            SubscriptionDef def = catalog.Get(planId);
            if (def == null) return;
            string running = progress.ActiveSubscription();
            if (running != null && running != planId)
            {
                SubscriptionDef other = catalog.Get(running);
                Info("Another plan is running", "Your " + (other != null ? other.Name : "subscription") + " is still active. You can buy this plan after it ends.");
                return;
            }
            if (!ShopScreen.TestPurchasesEnabled) { BuyWithStore(def); return; }   // the real store (Services.Payments); the Google sheet is the confirmation
            string extend = running != null ? "\nThis adds " + def.Days + " days to your current time." : "";
            host.OpenPopup("Confirm test buy", "TEST PURCHASE\n" + def.Name + " for " + SubscriptionCatalog.AmountText(def) + ".\nYou get " + Num(def.InstantGems) + " gems now and a daily reward for " + def.Days + " days." + (def.Chests.Count > 0 ? "\nIncludes chests on set days." : "") + extend + "\nNo money is charged.", 1180f);
            host.PopupButton("Confirm", true, 230f, () => CompleteBuy(def));
            host.PopupButton("Cancel", false, 60f, host.ClosePopup);
        }

        bool storeBusy;

        /// <summary>A real subscription purchase (docs/33): grant and save inside the store callback, once per transaction; a plan the game cannot start is rejected (not confirmed, so refunded).</summary>
        void BuyWithStore(SubscriptionDef def)
        {
            string productId = ProductCatalog.ForSubscription(def.Id);
            if (productId == null) { Info("Not available", def.Name + " cannot be bought right now."); return; }
            if (storeBusy) return;
            storeBusy = true;
            IPaymentService pay = Services.Payments;
            pay.Initialize();
            pay.Purchase(productId, r =>
            {
                storeBusy = false;
                string title, text;
                if (r.Status == PurchaseStatus.Success)
                {
                    StorePurchaseOutcome o = progress.ApplyStorePurchase(r.ProductId, r.TransactionId, DateTime.UtcNow);
                    if (o == StorePurchaseOutcome.Granted) { title = "Subscription active"; text = "+" + Num(def.InstantGems) + " gems added.\n" + def.Name + ": " + progress.DaysLeft() + " days left.\nClaim your reward every day."; }
                    else if (o == StorePurchaseOutcome.Duplicate) { title = "Already added"; text = "This purchase was already added."; }
                    else { r.Reject = true; title = "Could not start the plan"; text = "Your purchase could not be added. Google Play refunds it if this keeps happening."; }
                }
                else if (r.Status == PurchaseStatus.Cancelled) return;
                else if (r.Status == PurchaseStatus.Pending) { title = "Purchase pending"; text = "Your payment is waiting for approval."; }
                else { title = "Purchase failed"; text = r.Status == PurchaseStatus.Unavailable ? "The store is not available right now. Check your connection and try again." : "The purchase did not go through. You were not charged."; }
                if (host == null) return;
                if (r.Success && !r.Reject) AudioManager.Play(Sfx.PowerUp);
                Refresh();
                Info(title, text);
            });
        }

        void CompleteBuy(SubscriptionDef def)
        {
            host.ClosePopup();
            switch (progress.GrantTestSubscriptionPurchase(def.Id, ShopScreen.TestPurchasesEnabled))
            {
                case SubscriptionBuyResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    Refresh();   // days left, Claim button and the wallet follow
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

        /// <summary>What a chest of the claim gave: "Gold chest opened: 3,512 coins, 3 power-ups (...), hero card Name".</summary>
        string ChestText(SubscriptionChestResult r)
        {
            ChestContents c = r.Contents;
            string text = (r.Kind == ChestKind.Golden ? "Gold" : "Iron") + " chest opened:\n+" + Num(c.Coins) + " coins";
            for (int i = 0; i < c.PowerUps.Count; i++)
            {
                PowerUpDef pw = progress.PowerUpDefs.Get(c.PowerUps[i]);
                text += "\n+1 " + (pw != null ? pw.Name : "power-up");
            }
            if (c.Hero != null) text += "\n+1 hero card: " + c.Hero.Name;
            return text;
        }

        /// <summary>Tap on Claim: pays today's reward (once per local day) and tells what was added.</summary>
        public void TapClaim()
        {
            SubscriptionClaim claim = progress.ClaimDaily();
            switch (claim.Result)
            {
                case SubscriptionClaimResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    Refresh();
                    string text = "+" + Num(claim.Gems) + " gems\n+" + Num(claim.Coins) + " coins";
                    if (claim.PowerUpId != null)
                    {
                        PowerUpDef pw = progress.PowerUpDefs.Get(claim.PowerUpId);
                        text += "\n+" + claim.PowerUpCount + " " + (pw != null ? pw.Name : "power-up") + " (free power-up)";
                    }
                    for (int i = 0; i < claim.Chests.Count; i++) text += "\n" + ChestText(claim.Chests[i]);
                    Info("Reward claimed", text + "\nCome back tomorrow for the next one.");
                    break;
                case SubscriptionClaimResult.AlreadyClaimedToday:
                    Refresh();
                    Info("Already claimed", "Today's reward is already yours. Come back tomorrow.");
                    break;
                default:
                    Refresh();
                    Info("Subscription ended", "Your subscription is not active. Buy it again to get daily rewards.");
                    break;
            }
        }
    }
}
