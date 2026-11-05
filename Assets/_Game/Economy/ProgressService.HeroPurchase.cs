using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    /// <summary>Buying one hero card for gems in the Shop (Bug List 4 E2; rules in <see cref="HeroPurchase"/>).</summary>
    public sealed partial class ProgressService
    {
        /// <summary>The heroes of exactly this rarity that a buy can give right now (open regions only). Count 0 means "No heroes yet".</summary>
        public List<CharacterSpec> HeroBuyPool(CharacterRarity rarity)
        {
            return BundleCards.Pool(rarity, CharacterDefs.All, IsBundleRegionOpen);
        }

        /// <summary>
        /// Buys one random hero of <paramref name="rarity"/> from the open regions. Order of checks, none of which touches the save: rarity sold, a hero exists, enough gems.
        /// Then the gems are paid and exactly one copy is added (AddCopies); that one save holds both. <paramref name="hero"/> is the hero received (null unless Ok).
        /// </summary>
        public HeroBuyResult TryBuyHero(CharacterRarity rarity, out CharacterSpec hero)
        {
            hero = null;
            int price = HeroPurchase.GemPrice(rarity);
            if (price <= 0) return HeroBuyResult.Invalid;
            if (HeroBuyPool(rarity).Count == 0) return HeroBuyResult.NoHeroes;   // nothing charged, nothing counted
            if (Data.Gems < price) return HeroBuyResult.NotEnoughGems;
            BundleSaveData b = BundleState;
            EnsureBundleSeed(b);
            CharacterSpec pick = HeroPurchase.Pick(rarity, CharacterDefs.All, IsBundleRegionOpen, HeroPurchase.Roll(b.Seed, b.HeroBuys, rarity));
            if (pick == null || !CharacterInventory.Exists(pick.Id)) return HeroBuyResult.Invalid;
            Data.Gems -= price;
            b.HeroBuys++;
            AddCopies(pick.Id, 1);   // saves once: the gems, the counter and the copy
            hero = pick;
            return HeroBuyResult.Ok;
        }
    }
}
