using System.Collections.Generic;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    public enum ChestOpenResult
    {
        Ok,
        NotEnoughGems,
        Invalid
    }

    /// <summary>What one chest gave: coins, power-ups and exactly one hero card (null only when no hero exists in an open region at all).</summary>
    public sealed class ChestContents
    {
        /// <summary>The hero card of this chest (one more copy of this hero was added). A duplicate is just another copy, never a refund.</summary>
        public CharacterSpec Hero;
        /// <summary>Power-up ids, one entry per unit (1 in an Iron chest, 3 in a Gold one; the same type may repeat).</summary>
        public readonly List<string> PowerUps = new List<string>();
        /// <summary>All coins of this chest.</summary>
        public int Coins;
    }

    /// <summary>
    /// Treasure chests, opened with gems only (docs/22 point 3, reworked by Bug List 5 F3). Iron: 1 power-up, 800 to 1,200 coins and 1 hero card. Gold: 3 power-ups, 3,000 to 4,200 coins and 1 hero card.
    /// Each power-up is one of the implemented types with equal chance. Open 10 costs 10 times the single price (no discount). The pure rules (odds, pool, worth) are in ChestCards.cs.
    /// </summary>
    public static partial class ChestRolls
    {
        /// <summary>Seed of one chest opening: the kind, the running count of chests opened (stored in the save) and the per-account seed (0 = none, used by old tests).</summary>
        public static int SeedOf(ChestKind kind, int openIndex, int accountSeed = 0)
        {
            unchecked { return ((openIndex + 1) * (int)0x9E3779B1) ^ (((int)kind + 1) * 0x5bd1e995) ^ (accountSeed * 0x2545F491); }
        }

        /// <summary>Mean gem price of one random power-up of the implemented types (the number the worth uses). 60.71 with the built-in catalog.</summary>
        public static double AveragePowerUpGems(PowerUpCatalog powerUps)
        {
            double sum = 0.0;
            int n = 0;
            if (powerUps != null) foreach (PowerUpDef d in powerUps.Implemented) { sum += d.GemPrice; n++; }
            return n > 0 ? sum / n : 0.0;
        }

        static PowerUpCatalog builtIn;

        /// <summary>Worth multiplier of a chest with the given catalog (the built-in one when null).</summary>
        public static double Worth(ChestKind kind, PowerUpCatalog powerUps = null)
        {
            return WorthMultiplier(kind, AveragePowerUpGems(powerUps ?? (builtIn ?? (builtIn = PowerUpCatalog.BuiltIn()))));
        }

        /// <summary>"Worth x1.8" for the chest card and its info popup.</summary>
        public static string WorthLabel(ChestKind kind, PowerUpCatalog powerUps = null)
        {
            return WorthText(kind, AveragePowerUpGems(powerUps ?? (builtIn ?? (builtIn = PowerUpCatalog.BuiltIn()))));
        }

        /// <summary>Rolls one chest with every region open and the full character list (tests, previews).</summary>
        public static ChestContents Roll(ChestKind kind, int openIndex, IList<PowerUpDef> powerUpTypes)
        {
            return Roll(kind, openIndex, powerUpTypes, CharacterDefs.All, null, 0);
        }

        /// <summary>
        /// Rolls one chest, repeatable from (kind, openIndex, accountSeed): the power-up units, the coins, then the hero tier and the hero. The hero comes only from characters of regions
        /// <paramref name="regionOpen"/> says are open (null = all), with the tier fallback of <see cref="TryResolveTier"/>.
        /// </summary>
        public static ChestContents Roll(ChestKind kind, int openIndex, IList<PowerUpDef> powerUpTypes, IList<CharacterSpec> all, System.Func<int, bool> regionOpen, int accountSeed)
        {
            var rng = new Rng(SeedOf(kind, openIndex, accountSeed));
            var result = new ChestContents();
            int units = PowerUpCount(kind);
            for (int i = 0; i < units; i++)
                result.PowerUps.Add(powerUpTypes[rng.Next(powerUpTypes.Count)].Id);
            result.Coins = rng.Range(MinCoins(kind), MaxCoins(kind));
            ulong tierRoll = ((ulong)rng.NextUInt() << 32) | rng.NextUInt();
            ulong heroRoll = ((ulong)rng.NextUInt() << 32) | rng.NextUInt();
            result.Hero = DrawHero(kind, tierRoll, heroRoll, all, regionOpen);
            return result;
        }
    }

    /// <summary>Treasure chests and owned characters. Every public change saves once.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>Saves with a bad chest counter or character list get safe defaults; the current save version is stamped.</summary>
        void EnsureChestFields()
        {
            int loadedVersion = Data.Version;
            if (Data.Characters == null) Data.Characters = new List<string>();
            Data.Characters.RemoveAll(id => CharacterDefs.Get(id) == null);
            if (Data.HeroesRevealed == null) Data.HeroesRevealed = new List<string>();
            Data.HeroesRevealed.RemoveAll(id => CharacterDefs.Get(id) == null);
            if (Data.ChestOpens < 0) Data.ChestOpens = 0;
            EnsureCopyFields(loadedVersion);   // ProgressService.Merge.cs
            if (Data.Version < SaveData.CurrentVersion) Data.Version = SaveData.CurrentVersion;
        }

        /// <summary>True when the player has the character: a Common one is owned when its region is unlocked; any other came from a chest or the shop.</summary>
        public bool OwnsCharacter(string id)
        {
            CharacterSpec c = CharacterDefs.Get(id);
            if (c == null) return false;
            if (TestOwns(c)) return true;   // Character Test: every COMMON hero is owned; Uncommon and above only by merge, bought or received copies (docs/34 section 10)
            int counted;
            if (Data.Copies != null && Data.Copies.TryGetValue(id, out counted)) return counted > 0;   // docs/34: once a character is counted, its copies decide
            return OwnedWithoutCopyCount(c);
        }

        /// <summary>Character Test: the Common heroes of the regions count as owned (never the merge tiers: those stay locked until merged, bought or received).</summary>
        internal static bool TestOwns(CharacterSpec c) { return TestAllCharacters && c != null && c.Rarity == CharacterRarity.Common && c.Region > 0; }

        /// <summary>True when the hero's reveal is done (or it never needs one: Momo and every non-hero character).</summary>
        public bool IsHeroRevealed(string id)
        {
            CharacterSpec c = CharacterDefs.Get(id);
            return c != null && (!c.NeedsReveal || Data.HeroesRevealed.Contains(id));
        }

        /// <summary>True when the character may be shown and chosen: owned, and for a region hero also revealed. SYSTEM_TEST shows everything.</summary>
        public bool IsCharacterVisible(string id)
        {
            if (!OwnsCharacter(id)) return false;
            return TestAllCharacters || IsHeroRevealed(id);
        }

        /// <summary>Heroes whose region is unlocked but whose reveal pop-up was not confirmed yet, in list order (shown one after the other on the next home or map screen).</summary>
        public List<string> PendingHeroReveals()
        {
            var list = new List<string>(2);
            if (TestAllCharacters) return list;
            foreach (CharacterSpec c in CharacterDefs.All)
                if (c.NeedsReveal && !Data.HeroesRevealed.Contains(c.Id) && OwnsCharacter(c.Id)) list.Add(c.Id);
            return list;
        }

        /// <summary>Records a confirmed reveal and saves at once, so the pop-up shows once. False for an unknown, not needing or already revealed hero.</summary>
        public bool MarkHeroRevealed(string id)
        {
            CharacterSpec c = CharacterDefs.Get(id);
            if (c == null || !c.NeedsReveal || Data.HeroesRevealed.Contains(id) || !OwnsCharacter(id)) return false;
            Data.HeroesRevealed.Add(id);
            Commit();
            return true;
        }

        /// <summary>Gives a character (shop, tests). Returns false for an unknown or already owned one.</summary>
        public bool GrantCharacter(string id)
        {
            if (CharacterDefs.Get(id) == null || OwnsCharacter(id)) return false;
            Data.Characters.Add(id);
            Commit();
            return true;
        }

        /// <summary>
        /// Opens 1 or 10 chests for gems only. Debits the gems and credits the power-ups and coins, then adds exactly ONE copy of each chest's hero through <see cref="AddCopies"/>
        /// (which saves). A hero the player already has is simply another copy (a new card): nothing is refunded. Nothing changes unless the result is Ok.
        /// <paramref name="contents"/> has one entry per chest. The hero draw is repeatable from the per-account seed and the running chest counter, and only uses heroes of open regions.
        /// </summary>
        public ChestOpenResult TryOpenChests(ChestKind kind, int opens, out List<ChestContents> contents)
        {
            contents = null;
            int price = ChestRolls.Price(kind, opens);
            if (price <= 0) return ChestOpenResult.Invalid;
            if (Data.Gems < price) return ChestOpenResult.NotEnoughGems;
            var types = new List<PowerUpDef>(PowerUpDefs.Implemented);
            if (types.Count == 0) return ChestOpenResult.Invalid;
            BundleSaveData b = BundleState;
            EnsureBundleSeed(b);
            Data.Gems -= price;
            contents = new List<ChestContents>(opens);
            for (int i = 0; i < opens; i++)
            {
                ChestContents c = ChestRolls.Roll(kind, Data.ChestOpens++, types, CharacterDefs.All, IsBundleRegionOpen, b.Seed);
                foreach (string pid in c.PowerUps)
                {
                    int have = PowerUpCount(pid);
                    Data.Inventory.Powerups[pid] = SaturatingAdd(have, 1);   // no stock cap: a chest always gives its power-ups
                }
                Data.Coins = SaturatingAdd(Data.Coins, c.Coins);
                contents.Add(c);
            }
            bool saved = false;
            foreach (ChestContents c in contents)
                if (c.Hero != null && CharacterInventory.Exists(c.Hero.Id)) { AddCopies(c.Hero.Id, 1); saved = true; }   // the existing API; the first save also holds everything above
            if (!saved) Commit();
            return ChestOpenResult.Ok;
        }
    }
}
