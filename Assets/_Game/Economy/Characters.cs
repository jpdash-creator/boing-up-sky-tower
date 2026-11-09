using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
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
        /// <summary>True for a Common hero of a region (every region has one starter hero and, from Region 1 on, more Common heroes that come as cards).</summary>
        public bool IsRegionHero { get { return Rarity == CharacterRarity.Common && Region > 0; } }
        /// <summary>
        /// True for the ONE Common hero a region hands over by itself when it unlocks (Momo, Capi, Otto, Bonbon, Bao). The other Common heroes of a region (Region 1: Bumble, Ribbi, Dandy, Pip)
        /// are never owned for free: they come as hero cards from region goals, weekly rewards, chests and bundles, like any other copy.
        /// </summary>
        public bool RegionStarter;
        /// <summary>
        /// True when the hero stays hidden (Heroes screen, selection, lists) until its region is unlocked AND the one-time "New hero joins you!" reveal was confirmed.
        /// Region 1 (Momo) is never hidden, and neither are the extra Common heroes (they appear as soon as a copy is owned).
        /// </summary>
        public bool NeedsReveal { get { return RegionStarter && Region > 1; } }
        /// <summary>Price in gems in the shop (0 while unpriced). A duplicate from a chest refunds 25 percent of it as gems.</summary>
        public int GemPrice;
        public PowerUp[] Powers = new PowerUp[0];
        /// <summary>The six stored stats (docs/44). The default is Momo's baseline, so every current hero plays alike.</summary>
        public CharacterStats Stats = CharacterStats.Default;
        /// <summary>
        /// Merge tiers only: the main stats as the player sees them (Jump height, Speed, Control, Wall Bounce, one decimal) after the tier chain, NOT limited. The hero sheet and
        /// the merge text show these. <see cref="Stats"/> holds the effective copy the simulation uses: derived from these and capped at the docs/44 section 4 limits. Null = a Common hero.
        /// </summary>
        public ShownStats Shown;
        /// <summary>The shown stats of any hero: the tier chain result, or the Common hero's own numbers.</summary>
        public ShownStats ShownValues() { return Shown ?? ShownStats.From(Stats ?? CharacterStats.Default, new SimConfig()); }
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

        /// <summary>
        /// The stats of a Common hero: the four physics numbers come from its row in <see cref="HeroTable"/> (docs/44; the table is the one place the numbers live), the flat finds and the run bonus are the hero's own.
        /// The default hero (Momo, 19.0 / 9 / 30 / 77) stays bit-identical to <see cref="CharacterStats.Default"/>.
        /// </summary>
        static CharacterStats FromTable(string id, int coinFind, int runBonusCoins, int runBonusGems)
        {
            HeroRow r = HeroTable.Find(id);
            return new CharacterStats
            {
                JumpLaunch = r.LaunchTenths / 10,
                JumpLaunchTenths = r.LaunchTenths,
                MaxSpeed = r.Speed,
                Control = r.Control,
                WallBoostHundredths = r.Wall,
                CoinFind = coinFind,
                RunBonusCoins = runBonusCoins,
                RunBonusGems = runBonusGems,
            };
        }

        public static readonly CharacterSpec Momo = new CharacterSpec { Id = "momo", Name = "Momo", Rarity = CharacterRarity.Common, Region = 1, RegionStarter = true, Stats = FromTable("momo", 0, 80, 2) };

        // Region 1 (Meadow Springs) extra Common heroes (F4.1, owner 2026-11-06). Each has ONE identity: Bumble the bee = speed (Flight power), Ribbi the frog = jump (Double Jump),
        // Dandy the dandelion puff = floaty air control (Slow Time; there is no gravity stat, so "slow fall" is the highest Control and the lowest Speed), Pip the ladybug = wall bounce (Shield).
        // All physics are generated by the Forgiveness Index rule engine (docs/44, HeroTable.cs). They belong to Region 1 but are NOT starters: they come as hero cards (Region 1 goals, weekly, chests, bundles).
        static readonly CharacterSpec Bumble = new CharacterSpec { Id = "bumble", Name = "Bumble", Rarity = CharacterRarity.Common, Region = 1, Stats = FromTable("bumble", 0, 70, 3) };
        static readonly CharacterSpec Ribbi = new CharacterSpec { Id = "ribbi", Name = "Ribbi", Rarity = CharacterRarity.Common, Region = 1, Stats = FromTable("ribbi", 0, 60, 2) };
        static readonly CharacterSpec Dandy = new CharacterSpec { Id = "dandy", Name = "Dandy", Rarity = CharacterRarity.Common, Region = 1, Stats = FromTable("dandy", 0, 100, 4) };
        static readonly CharacterSpec Pip = new CharacterSpec { Id = "pip", Name = "Pip", Rarity = CharacterRarity.Common, Region = 1, Stats = FromTable("pip", 0, 50, 1) };

        // Region 2 (Mushroom Grove) extra Common heroes (F4.2): Capi is the starter, these four come as hero cards. Nib the squirrel = coin finder (Magnet, CoinFind), Spike the hedgehog = Shield,
        // Moss the snail kid = Wall Stick (best wall bounce), Fenn the fox kit = extra run coins and gems.
        static readonly CharacterSpec Nib = new CharacterSpec { Id = "nib", Name = "Nib", Rarity = CharacterRarity.Common, Region = 2, Stats = FromTable("nib", 20, 100, 2) };
        static readonly CharacterSpec Spike = new CharacterSpec { Id = "spike", Name = "Spike", Rarity = CharacterRarity.Common, Region = 2, Stats = FromTable("spike", 0, 50, 3) };
        static readonly CharacterSpec Moss = new CharacterSpec { Id = "moss", Name = "Moss", Rarity = CharacterRarity.Common, Region = 2, Stats = FromTable("moss", 0, 90, 3) };
        static readonly CharacterSpec Fenn = new CharacterSpec { Id = "fenn", Name = "Fenn", Rarity = CharacterRarity.Common, Region = 2, Stats = FromTable("fenn", 0, 100, 5) };
        // Region 3 (Sunny Beach) extra Common heroes (F4.3): Otto is the starter, these four come as hero cards. Pinch the crab = Shield, Shelly the sea turtle = Slow Time (calm control), Bloop the pufferfish kid = Double Jump (jump),
        // Coco the monkey = Magnet and a coin find stat (no coins in his art).
        static readonly CharacterSpec Pinch = new CharacterSpec { Id = "pinch", Name = "Pinch", Rarity = CharacterRarity.Common, Region = 3, Stats = FromTable("pinch", 0, 60, 3) };
        static readonly CharacterSpec Shelly = new CharacterSpec { Id = "shelly", Name = "Shelly", Rarity = CharacterRarity.Common, Region = 3, Stats = FromTable("shelly", 0, 70, 2) };
        static readonly CharacterSpec Bloop = new CharacterSpec { Id = "bloop", Name = "Bloop", Rarity = CharacterRarity.Common, Region = 3, Stats = FromTable("bloop", 0, 90, 2) };
        static readonly CharacterSpec Coco = new CharacterSpec { Id = "coco", Name = "Coco", Rarity = CharacterRarity.Common, Region = 3, Stats = FromTable("coco", 30, 80, 1) };

        // Region 4 (Candy Canyon) extra Common heroes (F4.4): Bonbon is the starter, these four come as hero cards. Gummi the gummy bear = Shield (best wall bounce), Choco the chocolate bunny = Double Jump (jump),
        // Marsh the marshmallow kid = Slow Time (calm control), Cookie the cookie kid = Magnet and a coin find stat. Region 4 stats sit slightly above Region 3, inside the Momo +-10 percent band (docs/42).
        static readonly CharacterSpec Gummi = new CharacterSpec { Id = "gummi", Name = "Gummi", Rarity = CharacterRarity.Common, Region = 4, Stats = FromTable("gummi", 0, 80, 3) };
        static readonly CharacterSpec Choco = new CharacterSpec { Id = "choco", Name = "Choco", Rarity = CharacterRarity.Common, Region = 4, Stats = FromTable("choco", 0, 100, 1) };
        static readonly CharacterSpec Marsh = new CharacterSpec { Id = "marsh", Name = "Marsh", Rarity = CharacterRarity.Common, Region = 4, Stats = FromTable("marsh", 0, 90, 4) };
        static readonly CharacterSpec Cookie = new CharacterSpec { Id = "cookie", Name = "Cookie", Rarity = CharacterRarity.Common, Region = 4, Stats = FromTable("cookie", 35, 80, 4) };
        // Region 5 (Bamboo Temple) extra Common heroes (F4.5): Bao is the starter, these four come as hero cards. Sprout the bamboo sprout = Shield, Tora the tiger cub = Double Jump (jump),
        // Rusu the red panda = Magnet and a coin find stat, Ryu the wingless dragon pup = Rocket (speed). Region 5 stats sit above Region 4, inside the Momo +-10 percent band (docs/43).
        static readonly CharacterSpec Sprout = new CharacterSpec { Id = "sprout", Name = "Sprout", Rarity = CharacterRarity.Common, Region = 5, Stats = FromTable("sprout", 0, 70, 5) };
        static readonly CharacterSpec Tora = new CharacterSpec { Id = "tora", Name = "Tora", Rarity = CharacterRarity.Common, Region = 5, Stats = FromTable("tora", 0, 60, 4) };
        static readonly CharacterSpec Rusu = new CharacterSpec { Id = "rusu", Name = "Rusu", Rarity = CharacterRarity.Common, Region = 5, Stats = FromTable("rusu", 40, 80, 5) };
        static readonly CharacterSpec Ryu = new CharacterSpec { Id = "ryu", Name = "Ryu", Rarity = CharacterRarity.Common, Region = 5, Stats = FromTable("ryu", 0, 90, 5) };

        // region heroes (T66, owner-approved paintings): R2 forest girl Capi, R3 beach sea otter Otto, R4 candy girl Bonbon, R5 Shaolin monk Bao. (The old Sporeling was deleted 2026-11-01; Capi replaced it. Old saves that name "sporeling" are cleaned in ProgressService.EnsureChestFields and fall back to Momo.)
        public static readonly CharacterSpec ForestGirl = new CharacterSpec { Id = "forestgirl", Name = "Capi", Rarity = CharacterRarity.Common, Region = 2, RegionStarter = true, Stats = FromTable("forestgirl", 0, 90, 1) };
        public static readonly CharacterSpec BeachOtter = new CharacterSpec { Id = "beachotter", Name = "Otto", Rarity = CharacterRarity.Common, Region = 3, RegionStarter = true, Stats = FromTable("beachotter", 0, 100, 3) };
        public static readonly CharacterSpec CandyGirl = new CharacterSpec { Id = "candygirl", Name = "Bonbon", Rarity = CharacterRarity.Common, Region = 4, RegionStarter = true, Stats = FromTable("candygirl", 0, 70, 4) };
        public static readonly CharacterSpec Monk = new CharacterSpec { Id = "monk", Name = "Bao", Rarity = CharacterRarity.Common, Region = 5, RegionStarter = true, Stats = FromTable("monk", 0, 60, 5) };

        /// <summary>
        /// ALL 25 Common hero lines as DATA, in list order: Region 1 first (Momo, then Bumble, Ribbi, Dandy, Pip), then one hero per further region. Only the five lines of
        /// <see cref="HeroTable.ActiveLines"/> (Momo, Capi, Otto, Bonbon, Bao) are ACTIVE (owner 2026-11-09, Bug List 6): the other 20 lines are inactive ALWAYS, a permanent compile-time rule
        /// with no setting. Inactive heroes never exist at runtime: they are not in <see cref="All"/>, so no pool, shop, reward, save or screen ever sees them.
        /// </summary>
        static readonly CharacterSpec[] BasesAll = { Momo, Bumble, Ribbi, Dandy, Pip, ForestGirl, Nib, Spike, Moss, Fenn, BeachOtter, Pinch, Shelly, Bloop, Coco, CandyGirl, Gummi, Choco, Marsh, Cookie, Monk, Sprout, Tora, Rusu, Ryu };

        /// <summary>The ACTIVE Common heroes in list order: Momo, Capi, Otto, Bonbon, Bao.</summary>
        static readonly CharacterSpec[] Bases = ActiveOnly(BasesAll);

        /// <summary>DATA ONLY (tools and the bot proof tests): all 125 heroes (25 lines x 5 tiers), active and inactive. Runtime code must use <see cref="All"/>.</summary>
        public static readonly CharacterSpec[] AllIncludingInactive = Concat(BasesAll, MergeTierDefs.Create(BasesAll));

        /// <summary>Every character that exists at runtime: the 25 ACTIVE heroes (5 lines x 5 tiers). Everything is built from this list: hero lists, chest, bundle, shop and reward pools, region goals, All Heroes, merge lines, inventories and saves.</summary>
        public static readonly CharacterSpec[] All = ActiveOnly(AllIncludingInactive);

        /// <summary>True when the hero belongs to one of the five active lines (the permanent rule, <see cref="HeroTable.ActiveLines"/>).</summary>
        public static bool IsActive(CharacterSpec c) { return c != null && HeroTable.IsActiveLine(c.LineId); }

        /// <summary>True when the id names an ACTIVE hero (false for an inactive hero and for an unknown id).</summary>
        public static bool IsActiveId(string id) { return Get(id) != null; }

        static CharacterSpec[] ActiveOnly(CharacterSpec[] src)
        {
            var list = new List<CharacterSpec>(src.Length);
            foreach (CharacterSpec c in src) if (IsActive(c)) list.Add(c);
            return list.ToArray();
        }

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
            return null;   // an inactive hero id (or an unknown one) is not a character: callers fall back to Momo
        }

        /// <summary>Data accessor for tools and tests: the hero of this id among ALL 125 (active or not), or null.</summary>
        public static CharacterSpec GetIncludingInactive(string id)
        {
            if (id == null) return null;
            foreach (CharacterSpec c in AllIncludingInactive) if (c.Id == id) return c;
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
