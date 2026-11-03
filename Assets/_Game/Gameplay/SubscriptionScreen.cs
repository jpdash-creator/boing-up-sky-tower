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
    /// The VIP page (docs/26 section 2; the owner renamed the Subscription screen to VIP, issue list 30 item 4). A full page in the shop look
    /// (ShopChrome: Momo scene, wood background, yellow italic ribbon "VIP") whose body is a LIST OF SECTIONS, each a header plus content (add a section by
    /// calling BuildSection in BuildSections). The first section is "Subscription": ONE compact card per plan (WEEKLY blue, MONTHLY gold) built like the
    /// Iron and Gold chest cards: ticket icon on the left, plan name, two or three colour-coded benefit lines, and on the right the price button (an active
    /// plan shows the days left and a Claim button, or the countdown to local midnight, instead). The page never scrolls: the cards shrink between a minimum
    /// and a preferred height, and the text inside best-fits down to the 16 sp floor. Purchases are TEST purchases in development builds
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

        /// <summary>One plan: a card like the chest cards. Ticket left, name and benefit lines in the middle, price (or days left + Claim) right.</summary>
        void BuildCard(RectTransform section, SubscriptionDef def)
        {
            bool weekly = def.Id == SubscriptionCatalog.Weekly;
            bool gold = !weekly;
            DateTime now = progress.SubscriptionClock();
            bool active = progress.IsActive(def.Id, now);
            string word = weekly ? "Weekly" : "Monthly";

            RectTransform card = ShopChrome.Card(section, "Card_" + def.Id, gold ? ShopChrome.FrameColor.Gold : ShopChrome.FrameColor.Blue);
            bool power5 = def.PowerUpEveryClaims > 0;
            int lineCount = power5 ? 3 : 2;
            float cardH = Mathf.Max(CardMinH, CardPad * 2f + NameRowH + lineCount * (LineH + LineGap));
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.minHeight = cardH;
            le.preferredHeight = cardH;

            Color nameCol = gold ? new Color(0.33f, 0.14f, 0.02f) : new Color(0.82f, 0.93f, 1f);
            Color baseCol = gold ? new Color(0.20f, 0.08f, 0.02f) : Color.white;
            Color gems = gold ? new Color(0.05f, 0.25f, 0.75f) : new Color(0.62f, 0.88f, 1f);
            Color coins = gold ? new Color(0.45f, 0.24f, 0.0f) : new Color(1f, 0.86f, 0.30f);
            Color power = gold ? new Color(0.02f, 0.36f, 0.12f) : new Color(0.45f, 0.98f, 0.50f);

            // ticket icon (left)
            Sprite ticket = ToyKit.Get(weekly ? "icon_ticket_weekly" : "icon_ticket_monthly");
            bool fallback = ticket == null;
            if (fallback) ticket = ToyKit.Get("icon_crown");
            Image ti = UiKit.AddImage(card, "Ticket", ticket, fallback && weekly ? new Color(0.78f, 0.9f, 1f, 1f) : Color.white);
            ti.preserveAspect = true;
            ti.raycastTarget = false;
            ti.rectTransform.anchorMin = new Vector2(0.015f, 0.12f);
            ti.rectTransform.anchorMax = new Vector2(0.185f, 0.88f);
            ti.rectTransform.offsetMin = ti.rectTransform.offsetMax = Vector2.zero;

            // middle: ONE vertical stack (name + value row, then the benefit lines), single-line texts at the 16 sp floor: no overlap by construction
            const float cx0 = 0.19f, cx1 = 0.735f;
            RectTransform stack = UiKit.Rect(card, "Lines");
            stack.anchorMin = new Vector2(cx0, 0f);
            stack.anchorMax = new Vector2(cx1, 1f);
            stack.offsetMin = new Vector2(0f, CardPad);
            stack.offsetMax = new Vector2(0f, -CardPad);
            var vg = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            vg.spacing = LineGap;
            vg.childAlignment = TextAnchor.MiddleLeft;
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
            Text name = Plain(UiKit.AddText(head, "Name", word.ToUpperInvariant(), 48, nameCol, TextAnchor.MiddleLeft, true), TextAnchor.MiddleLeft, NameFs);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Plain(UiKit.AddText(head, "Worth", SubscriptionCatalog.WorthText(def).Replace("about ", ""), 42, gold ? new Color(0.02f, 0.36f, 0.19f) : power, TextAnchor.MiddleRight, true), TextAnchor.MiddleRight, Fs);

            string l1 = "+" + Colored(Num(def.InstantGems) + " gems", gems) + " now";
            string l2 = "Daily " + Colored(Num(def.DailyGems) + " gems", gems) + " + " + Colored(Num(def.DailyCoins) + " coins", coins);
            string l3 = power5 ? Colored("Power-up", power) + " every " + def.PowerUpEveryClaims + "th claim" : null;
            for (int r = 0; r < lineCount; r++)
            {
                string txt = r == 0 ? l1 : r == 1 ? l2 : l3;
                Text line = Plain(UiKit.AddText(stack, "Benefit" + (r + 1), txt, 42, baseCol, TextAnchor.MiddleLeft), TextAnchor.MiddleLeft, Fs);
                line.supportRichText = true;
                Row(line.gameObject, LineH);
            }

            string benefits = Num(def.InstantGems) + " gems now. Daily " + Num(def.DailyGems) + " gems and " + Num(def.DailyCoins) + " coins." + (power5 ? " A free power-up every " + def.PowerUpEveryClaims + "th claim." : "");
            card.gameObject.AddComponent<A11yLabel>().Text = def.Name + ", " + def.Days + " days. " + benefits + " " + SubscriptionCatalog.WorthText(def) + ".";

            // right: price button, or days left + Claim
            const float bx0 = 0.755f, bx1 = 0.985f;
            if (active)
            {
                int days = progress.DaysLeft(now);
                Text left = Plain(UiKit.AddText(card, "DaysLeft", days + (days == 1 ? " day left" : " days left"), 44, baseCol, TextAnchor.MiddleCenter, true), TextAnchor.MiddleCenter, Fs);
                Rows(left, bx0 - 0.03f, 1f, 0.62f, 0.97f);
                BuildClaim(card, bx0, bx1);
                return;
            }
            string planId = def.Id;
            bool otherActive = progress.IsActive(now);
            string a11y = "Buy " + def.Name + " for " + SubscriptionCatalog.AmountText(def) + ", " + def.Days + " days" + (ShopScreen.TestPurchasesEnabled ? ", test purchase" : ", not available yet");
            string price = SubscriptionCatalog.AmountText(def) + "\n/ " + def.Days + " Days";
            Button buy = ShopChrome.PriceButton(card, "Buy", 1, null, price, a11y, () => TapBuy(planId), 40);
            FixLabel(buy);
            if (otherActive) buy.GetComponent<Image>().color = DisabledTint;
            Anchor(buy, bx0, bx1);
        }

        /// <summary>The button column: vertically centred, as tall as the shared price buttons (at least the 48 dp target).</summary>
        static void Anchor(Button b, float x0, float x1)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(x0, 0.5f);
            rt.anchorMax = new Vector2(x1, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(0f, -ShopChrome.PriceButtonH * 0.5f);
            rt.offsetMax = new Vector2(0f, ShopChrome.PriceButtonH * 0.5f);
        }

        /// <summary>Today's reward: a green Claim button, or a disabled button showing the countdown to local midnight.</summary>
        void BuildClaim(RectTransform card, float x0, float x1)
        {
            Button b;
            if (progress.CanClaimToday())
            {
                b = ShopChrome.PriceButton(card, "Claim", 1, null, "Claim", "Claim today's VIP subscription reward", TapClaim, 44);
            }
            else
            {
                string time = Countdown(progress.TimeToNextClaim(progress.SubscriptionClock()));
                b = ShopChrome.PriceButton(card, "ComeBack", 0, null, time, "Today's reward is claimed. Come back tomorrow.", null, 40);
                b.interactable = false;
                b.GetComponent<Image>().color = DisabledTint;
                countdowns.Add(b.transform.Find("Label").GetComponent<Text>());
            }
            FixLabel(b);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(x0, 0.05f);
            rt.anchorMax = new Vector2(x1, 0.05f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, ShopChrome.PriceButtonH);
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
