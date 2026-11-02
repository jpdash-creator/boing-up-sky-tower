using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    public enum CharacterRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        /// <summary>Shown to players as "Elite" (<see cref="CharacterDefs.RarityNames"/>); the enum name is kept so saves and code stay unchanged.</summary>
        Epic = 3,
        Legendary = 4
    }

    /// <summary>
    /// One character (docs/21). Common characters belong to a region and are owned when it unlocks, and have no power. Uncommon and above come only from chests and the
    /// shop (gems) and may carry powers: free use ready at the start of a run, then a cooldown that gets shorter with rarity.
    /// </summary>
    public sealed class CharacterSpec
    {
        public string Id;
        public string Name;
        public CharacterRarity Rarity;
        /// <summary>
        /// RegionIndex: the region whose unlock gives this Common hero (every region has one designated Common hero); 0 for the others.
        /// To add a hero (R3 beach, R4 candy, R5 monk ...): one CharacterSpec row in <see cref="CharacterDefs.All"/> with this index plus the art in Resources/Characters/id.
        /// </summary>
        public int Region;
        /// <summary>True for the Common hero of a region.</summary>
        public bool IsRegionHero { get { return Rarity == CharacterRarity.Common && Region > 0; } }
        /// <summary>
        /// True when the hero stays hidden (Heroes screen, selection, lists) until its region is unlocked AND the one-time "New hero joins you!" reveal was confirmed.
        /// Region 1 (Momo) is never hidden.
        /// </summary>
        public bool NeedsReveal { get { return IsRegionHero && Region > 1; } }
        /// <summary>Price in gems in the shop (0 while unpriced). A duplicate from a chest refunds 25 percent of it as gems.</summary>
        public int GemPrice;
        public PowerUp[] Powers = new PowerUp[0];
        /// <summary>The six stored stats (docs/29). The default is Momo's baseline, so every current hero plays alike.</summary>
        public CharacterStats Stats = CharacterStats.Default;
        /// <summary>
        /// Merge tiers only: the stats as the owner's rule gives them (Common value plus the full percent boost, NOT limited). The hero sheet and the merge result show these.
        /// <see cref="Stats"/> holds the effective copy that the simulation uses, with the four physics stats capped at the documented safe maximums. Null = same as Stats.
        /// </summary>
        public CharacterStats DisplayStats;
        public CharacterStats SheetStats { get { return DisplayStats ?? Stats; } }
        /// <summary>Merge line (docs/34): every tier of one hero shares it. Null = the character's own id (the Common hero of the line).</summary>
        public string Line;
        public string LineId { get { return Line ?? Id; } }
        /// <summary>Art slot of a merge tier: Resources/Characters/{ArtId}/. Null = Id.</summary>
        public string ArtId;
        /// <summary>Until the tier's own art folder exists, this character's art is drawn instead (tinted placeholder). Null for characters with their own art.</summary>
        public string FallbackArtId;
        /// <summary>True for tiers made by merging 3 copies (never in a chest pool, never tied to a region).</summary>
        public bool IsMergeTier;
    }

    public static class CharacterDefs
    {
        public static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Elite", "Legendary" };
        /// <summary>Cooldown in seconds of every power of a character, by rarity (Common has no power).</summary>
        public static readonly int[] CooldownSeconds = { 0, 30, 22, 15, 10 };
        /// <summary>How many powers a character of this rarity has.</summary>
        public static readonly int[] PowerCount = { 0, 1, 2, 3, 4 };

        public static readonly CharacterSpec Momo = new CharacterSpec { Id = "momo", Name = "Momo", Rarity = CharacterRarity.Common, Region = 1, Stats = new CharacterStats { RunBonusCoins = 80, RunBonusGems = 2 } };

        // region heroes (T66, owner-approved paintings): R2 forest girl Capi, R3 beach sea otter Otto, R4 candy girl Bonbon, R5 Shaolin monk Bao. (The old Sporeling was deleted 2026-11-01; Capi replaced it. Old saves that name "sporeling" are cleaned in ProgressService.EnsureChestFields and fall back to Momo.)
        public static readonly CharacterSpec ForestGirl = new CharacterSpec { Id = "forestgirl", Name = "Capi", Rarity = CharacterRarity.Common, Region = 2, Stats = new CharacterStats { JumpLaunch = 19, MaxSpeed = 10, Control = 29, WallBoostHundredths = 79, RunBonusCoins = 90, RunBonusGems = 1 } };
        public static readonly CharacterSpec BeachOtter = new CharacterSpec { Id = "beachotter", Name = "Otto", Rarity = CharacterRarity.Common, Region = 3, Stats = new CharacterStats { JumpLaunch = 18, MaxSpeed = 8, Control = 32, WallBoostHundredths = 72, RunBonusCoins = 100, RunBonusGems = 3 } };
        public static readonly CharacterSpec CandyGirl = new CharacterSpec { Id = "candygirl", Name = "Bonbon", Rarity = CharacterRarity.Common, Region = 4, Stats = new CharacterStats { JumpLaunch = 21, MaxSpeed = 10, Control = 31, WallBoostHundredths = 84, RunBonusCoins = 70, RunBonusGems = 4 } };
        public static readonly CharacterSpec Monk = new CharacterSpec { Id = "monk", Name = "Bao", Rarity = CharacterRarity.Common, Region = 5, Stats = new CharacterStats { JumpLaunch = 20, MaxSpeed = 10, Control = 31, WallBoostHundredths = 80, RunBonusCoins = 60, RunBonusGems = 5 } };

        /// <summary>Every character that exists (has art). More are added here as they are made.</summary>
        public static readonly CharacterSpec[] All = Concat(new[] { Momo, ForestGirl, BeachOtter, CandyGirl, Monk }, MergeTierDefs.Create(new[] { Momo, ForestGirl, BeachOtter, CandyGirl, Monk }));

        static CharacterSpec[] Concat(CharacterSpec[] a, CharacterSpec[] b)
        {
            var all = new CharacterSpec[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }

        /// <summary>
        /// The stats of the chosen character, read when a level result is saved (flat coin and gem finds). Set once at start-up by the Gameplay layer
        /// (CharacterCatalog); until then, and in tests, it is Momo's baseline.
        /// </summary>
        public static System.Func<CharacterStats> ActiveStats = () => CharacterStats.Default;

        public static CharacterSpec Get(string id)
        {
            if (id == null) return null;
            foreach (CharacterSpec c in All) if (c.Id == id) return c;
            return null;
        }

        public static List<CharacterSpec> OfRarity(CharacterRarity r)
        {
            var list = new List<CharacterSpec>(4);
            // region heroes come only with their region (never from a chest), so they are not in any draw pool
            foreach (CharacterSpec c in All) if (c.Rarity == r && !c.IsRegionHero && !c.IsMergeTier) list.Add(c);
            return list;
        }
    }
}
