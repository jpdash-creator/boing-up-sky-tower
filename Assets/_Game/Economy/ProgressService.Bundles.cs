using System;
using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// Gem bundles (docs/26 section 3): the saved set of 6 offers, buying, the paid refresh, and the timer offer (30 percent off for 5 minutes,
    /// about every 45 minutes of play, never twice in a row, never during a run). Every public change saves once. All draws are repeatable:
    /// they come from the stored seed and counters, never from UnityEngine.Random.
    /// </summary>
    public sealed partial class ProgressService
    {
        public const int BundleOfferCount = 6;
        public const int BundleRefreshCoins = 100;
        public const int BundleRefreshGems = 5;
        /// <summary>Gems cost above which the page asks the player to confirm (docs/26: confirm above 50 gems).</summary>
        public const int BundleConfirmAboveGems = 50;
        public const int BundleTimerPercentOff = 30;
        public const int BundleTimerWindowSec = 5 * 60;
        /// <summary>Average play time between timer offers; each gap varies by plus or minus 5 minutes.</summary>
        public const int BundleTimerIntervalSec = 45 * 60;
        const int BundleTimerJitterSec = 5 * 60;
        const double BundleSaveEverySec = 30.0;

        BundleCatalog bundleCatalog;
        Func<DateTime> bundleClock;
        double bundleUnsavedSec;

        static readonly Func<DateTime> SystemUtcNow = () => DateTime.UtcNow;

        /// <summary>The bundle table (Resources/Content/bundles.json). Tests and remote overrides may replace it.</summary>
        public BundleCatalog Bundles
        {
            get { return bundleCatalog ?? (bundleCatalog = BundleCatalog.LoadFromResources()); }
            set { bundleCatalog = value; }
        }

        /// <summary>Clock used when a call has no explicit time (TickPlayTime, the draw seed). UTC. Tests inject a fixed one.</summary>
        public Func<DateTime> BundleClock
        {
            get { return bundleClock ?? SystemUtcNow; }
            set { bundleClock = value; }
        }

        /// <summary>The saved bundle state, created or repaired on first use (old saves and null lists are fine).</summary>
        BundleSaveData BundleState
        {
            get
            {
                if (Data.Bundles == null) Data.Bundles = new BundleSaveData();
                BundleSaveData b = Data.Bundles;
                if (b.Offers == null) b.Offers = new List<string>();
                if (b.TimerOfferId == null) b.TimerOfferId = "";
                if (b.TimerLastId == null) b.TimerLastId = "";
                return b;
            }
        }

        // ---- repeatable randomness (splitmix64) ----

        static ulong BundleMix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        void EnsureBundleSeed(BundleSaveData b)
        {
            if (b.Seed != 0) return;
            int s = (int)(BundleMix((ulong)BundleClock().Ticks) & 0x7FFFFFFFUL);
            b.Seed = s == 0 ? 1 : s;
        }

        // ---- the 6 offers ----

        /// <summary>
        /// The 6 bundles of the Bundles page. The first call on a new account draws and saves them; afterwards they stay the same until
        /// <see cref="RefreshBundleOffers"/>. Fewer than 6 only when the catalog itself has fewer.
        /// </summary>
        public List<BundleDef> CurrentBundleOffers()
        {
            BundleSaveData b = BundleState;
            var result = new List<BundleDef>(BundleOfferCount);
            int target = Math.Min(BundleOfferCount, Bundles.Count);
            foreach (string id in b.Offers)
            {
                BundleDef d = Bundles.Get(id);
                if (d != null && !result.Contains(d)) result.Add(d);
            }
            if (result.Count != target || b.Offers.Count != result.Count)
            {
                EnsureBundleSeed(b);
                result = DrawBundleOffers(b, null);
                b.Offers.Clear();
                foreach (BundleDef d in result) b.Offers.Add(d.Id);
                store.Save(Data);   // quiet save: reading must not raise Changed
            }
            return result;
        }

        List<BundleDef> DrawBundleOffers(BundleSaveData b, List<string> avoid)
        {
            IReadOnlyList<BundleDef> all = Bundles.All;
            int n = all.Count;
            int want = Math.Min(BundleOfferCount, n);
            var result = new List<BundleDef>(want);
            int avoidCount = avoid != null ? avoid.Count : 0;
            bool canAvoid = avoid != null && n - avoidCount >= want;
            ulong state = BundleMix(((ulong)(uint)b.Seed << 32) ^ (ulong)(uint)b.Draws ^ 0xB0B0B0B0UL);
            for (int guard = 0; result.Count < want && guard < 20000; guard++)
            {
                state = BundleMix(state);
                BundleDef d = all[(int)(state % (ulong)n)];
                if (result.Contains(d)) continue;
                if (canAvoid && avoid.Contains(d.Id)) continue;
                result.Add(d);
            }
            for (int i = 0; result.Count < want && i < n; i++) if (!result.Contains(all[i])) result.Add(all[i]);   // cannot happen in practice; keeps the set complete
            b.Draws++;
            return result;
        }

        /// <summary>
        /// Draws 6 new distinct bundles (none of the current ones while the catalog is big enough) and saves them. Costs 100 coins or 5 gems;
        /// nothing changes when the wallet is short.
        /// </summary>
        public BundleResult RefreshBundleOffers(bool payWithCoins)
        {
            if (Bundles.Count == 0) return BundleResult.Invalid;
            BundleSaveData b = BundleState;
            if (payWithCoins ? Data.Coins < BundleRefreshCoins : Data.Gems < BundleRefreshGems)
                return payWithCoins ? BundleResult.NotEnoughCoins : BundleResult.NotEnoughGems;
            List<BundleDef> old = CurrentBundleOffers();   // also makes sure the first set exists
            var avoid = new List<string>(old.Count);
            foreach (BundleDef d in old) avoid.Add(d.Id);
            EnsureBundleSeed(b);
            List<BundleDef> fresh = DrawBundleOffers(b, avoid);
            if (payWithCoins) Data.Coins -= BundleRefreshCoins; else Data.Gems -= BundleRefreshGems;
            b.Offers.Clear();
            foreach (BundleDef d in fresh) b.Offers.Add(d.Id);
            Commit();
            return BundleResult.Ok;
        }

        // ---- buying ----

        /// <summary>Gem price of the bundle on the page.</summary>
        public int BundlePrice(BundleDef def)
        {
            return def != null ? def.PriceGems : 0;
        }

        /// <summary>Buys a bundle at its list price. Atomic: either the gems are paid and everything is granted, or nothing changes. One save.</summary>
        public BundleResult TryBuyBundle(BundleDef def)
        {
            CharacterSpec card;
            return BuyBundleCore(def, BundlePrice(def), false, out card);
        }

        /// <summary>Same, and tells which hidden hero card was drawn and added (1 copy through <c>AddCopies</c>). Null when the bundle has no card or no card hero exists.</summary>
        public BundleResult TryBuyBundle(BundleDef def, out CharacterSpec card)
        {
            return BuyBundleCore(def, BundlePrice(def), false, out card);
        }

        public BundleResult TryBuyBundle(string bundleId)
        {
            CharacterSpec card;
            BundleDef def = Bundles.Get(bundleId);
            return BuyBundleCore(def, def != null ? def.PriceGems : 0, false, out card);
        }

        /// <summary>
        /// Draws the hidden card of the next purchase: repeatable from the per-account seed and the draw counter (like every bundle draw), never from UnityEngine.Random.
        /// Only the counter moves. Tier odds and the empty-tier fallback live in <see cref="BundleCards"/>.
        /// </summary>
        CharacterSpec DrawBundleCard(BundleSaveData b)
        {
            EnsureBundleSeed(b);
            ulong state = BundleMix(((ulong)(uint)b.Seed << 32) ^ (ulong)(uint)b.CardDraws ^ 0xCA4D0CA4DUL);
            ulong tierRoll = BundleMix(state);
            ulong charRoll = BundleMix(tierRoll);
            b.CardDraws++;
            return BundleCards.Draw(tierRoll, charRoll, CharacterDefs.All);
        }

        BundleResult BuyBundleCore(BundleDef def, int price, bool clearTimerOffer, out CharacterSpec card)
        {
            card = null;
            if (def == null) return BundleResult.Invalid;
            if (price <= 0 || def.Coins < 0) return BundleResult.Invalid;
            foreach (BundlePowerUp p in def.PowerUps)
                if (p == null || p.Count <= 0 || PowerUpDefs.Get(p.Id) == null) return BundleResult.Invalid;   // checked first: nothing is touched on failure
            if (Data.Gems < price) return BundleResult.NotEnoughGems;

            Data.Gems -= price;   // bundles give no gems back
            Data.Coins = SaturatingAdd(Data.Coins, def.Coins);
            foreach (BundlePowerUp p in def.PowerUps)
            {
                int have = PowerUpCount(p.Id);
                Data.Inventory.Powerups[p.Id] = (int)Math.Min((long)have + p.Count, int.MaxValue);   // saturating: no stock cap
            }
            if (def.HiddenCard) card = DrawBundleCard(BundleState);
            if (clearTimerOffer) ClearTimerOffer(BundleState);
            if (card != null) AddCopies(card.Id, 1);   // the existing API; it saves, and that one save also holds everything above
            else Commit();
            return BundleResult.Ok;
        }

        // ---- timer offer ----

        /// <summary>Price of a bundle while it is the timer offer: 30 percent off, rounded up to whole gems.</summary>
        public int TimerOfferPrice(BundleDef def)
        {
            if (def == null) return 0;
            long p = ((long)def.PriceGems * (100 - BundleTimerPercentOff) + 99) / 100;
            return (int)Math.Max(1, p);
        }

        /// <summary>The running timer offer, or null when there is none or it ended (<paramref name="nowUtc"/> is the injectable clock).</summary>
        public BundleDef CurrentTimerOffer(DateTime nowUtc)
        {
            BundleSaveData b = BundleState;
            if (b.TimerOfferId.Length == 0 || nowUtc.Ticks >= b.TimerExpiryTicks) return null;
            return Bundles.Get(b.TimerOfferId);
        }

        /// <summary>Whole seconds left of the timer offer (rounded up), 0 when there is none.</summary>
        public int TimerOfferSecondsLeft(DateTime nowUtc)
        {
            BundleSaveData b = BundleState;
            if (b.TimerOfferId.Length == 0 || nowUtc.Ticks >= b.TimerExpiryTicks) return 0;
            long ticks = b.TimerExpiryTicks - nowUtc.Ticks;
            return (int)((ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
        }

        /// <summary>Buys the running timer offer at the discounted price. Atomic like <see cref="TryBuyBundle(BundleDef)"/>; the offer ends with the purchase.</summary>
        public BundleResult TryBuyTimerOffer(DateTime nowUtc)
        {
            CharacterSpec card;
            return TryBuyTimerOffer(nowUtc, out card);
        }

        /// <summary>Same, and tells which hidden hero card was drawn and added.</summary>
        public BundleResult TryBuyTimerOffer(DateTime nowUtc, out CharacterSpec card)
        {
            card = null;
            BundleDef def = CurrentTimerOffer(nowUtc);
            if (def == null) return BundleResult.Invalid;
            return BuyBundleCore(def, TimerOfferPrice(def), true, out card);
        }

        /// <summary>The player closed the timer offer (X). It ends; the next one comes after the normal gap and is a different bundle.</summary>
        public void DismissTimerOffer()
        {
            BundleSaveData b = BundleState;
            if (b.TimerOfferId.Length == 0) return;
            ClearTimerOffer(b);
            Commit();
        }

        static void ClearTimerOffer(BundleSaveData b)
        {
            b.TimerOfferId = "";
            b.TimerExpiryTicks = 0;
        }

        /// <summary>
        /// Counts play time toward the next timer offer. Call about once a second while the app is in the foreground; pass inRun = true while a run is
        /// active: the time still counts but an offer never starts during a run (it starts on the first call after it). Saves when an offer starts or
        /// ends and otherwise every 30 seconds of play, so a restart loses at most half a minute.
        /// </summary>
        public void TickPlayTime(double seconds, bool inRun = false)
        {
            if (!(seconds > 0.0)) return;
            if (seconds > 3600.0) seconds = 3600.0;
            BundleSaveData b = BundleState;
            DateTime now = BundleClock();
            bool changed = false;
            if (b.TimerOfferId.Length > 0 && now.Ticks >= b.TimerExpiryTicks) { ClearTimerOffer(b); changed = true; }
            if (!b.TimerStarted)
            {
                EnsureBundleSeed(b);
                b.TimerStarted = true;
                b.TimerNextSec = NextTimerGapSec(b);
                changed = true;
            }
            b.TimerNextSec -= seconds;
            if (b.TimerNextSec <= 0.0 && !inRun && b.TimerOfferId.Length == 0 && Bundles.Count > 0)
            {
                StartTimerOffer(b, now);
                changed = true;
            }
            if (changed)
            {
                bundleUnsavedSec = 0.0;
                Commit();
            }
            else if (!inRun)   // no file write during a run; the next menu tick saves
            {
                bundleUnsavedSec += seconds;
                if (bundleUnsavedSec >= BundleSaveEverySec)
                {
                    bundleUnsavedSec = 0.0;
                    store.Save(Data);
                }
            }
        }

        double NextTimerGapSec(BundleSaveData b)
        {
            ulong r = BundleMix(((ulong)(uint)b.Seed << 32) ^ (ulong)(uint)b.TimerOffers ^ 0x7131713UL);
            return BundleTimerIntervalSec - BundleTimerJitterSec + (double)(r % (2UL * BundleTimerJitterSec + 1UL));
        }

        void StartTimerOffer(BundleSaveData b, DateTime now)
        {
            IReadOnlyList<BundleDef> all = Bundles.All;
            int n = all.Count;
            ulong state = BundleMix(((ulong)(uint)b.Seed << 32) ^ (ulong)(uint)b.TimerOffers ^ 0x5EEDC0DEUL);
            BundleDef pick = null;
            for (int guard = 0; guard < 2000 && pick == null; guard++)
            {
                state = BundleMix(state);
                BundleDef d = all[(int)(state % (ulong)n)];
                if (n > 1 && d.Id == b.TimerLastId) continue;   // never twice in a row
                pick = d;
            }
            if (pick == null) pick = all[0];
            b.TimerOfferId = pick.Id;
            b.TimerLastId = pick.Id;
            b.TimerExpiryTicks = now.AddSeconds(BundleTimerWindowSec).Ticks;
            b.TimerOffers++;
            b.TimerNextSec = NextTimerGapSec(b);
        }
    }
}
