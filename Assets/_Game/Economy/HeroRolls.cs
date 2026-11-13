using System;
using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Per-player hero stats (owner decision 2026-11-12, docs/44 section 11). Every tier hero (Uncommon, Rare, Elite, Legendary of EVERY active line, Momo's four upper tiers included) has a
    /// POOL of physics variants. Pool entry 0 is the hero's row in <see cref="HeroTable"/>; entries 1.. are the generated <see cref="HeroVariants"/>. Every entry passed the same measured
    /// Forgiveness Index gate (Tests/Simulation/HeroVariantGeneratorTests.cs, checked again by HeroVariantTests), so any entry is a legal hero of its rarity: two players holding the same hero see
    /// slightly different numbers, never a stronger or weaker hero. The Common hero of a line (and Momo Common, the 19.0 reference) is NOT rolled: it has one fixed row.
    /// The variant is dealt ONCE, when the hero is first obtained, by <see cref="Pick"/>: a pure integer function of (account seed, hero id, roll counter), so a server recomputes it from the
    /// per-account seed. Pure C#, no engine types, no clock, no floats: the .NET test project and a future server compile this file as it is.
    /// </summary>
    public static class HeroRolls
    {
        static readonly Dictionary<string, HeroRow[]> pools = BuildPools();
        static readonly Dictionary<string, Built> built = new Dictionary<string, Built>();

        sealed class Built { public CharacterStats Stats; public ShownStats Shown; }

        static Dictionary<string, HeroRow[]> BuildPools()
        {
            var variants = new Dictionary<string, List<HeroRow>>();
            foreach (HeroRow r in HeroVariants.Rows)
            {
                List<HeroRow> l;
                if (!variants.TryGetValue(r.Id, out l)) { l = new List<HeroRow>(32); variants[r.Id] = l; }
                l.Add(r);
            }
            var d = new Dictionary<string, HeroRow[]>();
            foreach (HeroRow r in HeroTable.Rows)
            {
                if (r.Tier < 1 || !r.Active) continue;   // Common stays fixed; inactive lines never exist at runtime
                var list = new List<HeroRow>(1 + 32) { r };
                List<HeroRow> v;
                if (variants.TryGetValue(r.Id, out v)) list.AddRange(v);
                d[r.Id] = list.ToArray();
            }
            return d;
        }

        /// <summary>True for a hero whose stats are rolled per player (an active tier hero, Uncommon or above).</summary>
        public static bool IsRollable(string heroId) { return heroId != null && pools.ContainsKey(heroId); }

        /// <summary>How many variants the hero has (table row included): 0 for a hero that is not rolled.</summary>
        public static int PoolSize(string heroId)
        {
            HeroRow[] p;
            return heroId != null && pools.TryGetValue(heroId, out p) ? p.Length : 0;
        }

        /// <summary>The physics row of a variant (0 = the table row). An index outside the pool gives the table row; null for a hero that is not rolled.</summary>
        public static HeroRow Row(string heroId, int variant)
        {
            HeroRow[] p;
            if (heroId == null || !pools.TryGetValue(heroId, out p)) return null;
            return variant >= 0 && variant < p.Length ? p[variant] : p[0];
        }

        /// <summary>True when the index is a real entry of the hero's pool.</summary>
        public static bool IsValidVariant(string heroId, int variant) { return variant >= 0 && variant < PoolSize(heroId); }

        static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>
        /// THE roll: the variant index of a hero for an account. Pure and repeatable: variant = f(accountSeed, heroId, counter), splitmix64 over the seed, the FNV hash of the hero id and the counter.
        /// Uniform over the pool (0 .. <see cref="PoolSize"/> - 1). 0 for a hero that is not rolled.
        /// </summary>
        public static int Pick(int accountSeed, string heroId, int counter)
        {
            int size = PoolSize(heroId);
            if (size <= 1) return 0;
            unchecked
            {
                ulong x = Mix(((ulong)(uint)accountSeed << 32) ^ (ulong)(uint)HeroIndex.Hash(heroId) ^ 0x48524F4C4C53UL);
                x = Mix(x ^ ((ulong)(uint)counter * 0xD6E8FEB86659FD93UL));
                return (int)(x % (ulong)size);
            }
        }

        /// <summary>
        /// The effective stats of a hero for a variant: the hero's own spec stats (flat finds, run bonus) with the four physics numbers of the variant, limited by the safety cap
        /// (<see cref="MergeTierDefs.Cap"/>). Variant 0 and any hero that is not rolled return the spec's own stats object. Cached, so a call in a game loop allocates nothing after the first.
        /// </summary>
        public static CharacterStats StatsFor(CharacterSpec spec, int variant)
        {
            if (spec == null) return CharacterStats.Default;
            if (variant <= 0 || !IsValidVariant(spec.Id, variant)) return spec.Stats ?? CharacterStats.Default;
            return Resolve(spec, variant).Stats;
        }

        /// <summary>The numbers the hero sheet prints for the variant (same rules as <see cref="StatsFor"/>); the spec's own <c>Shown</c> for variant 0. May be null for a spec without it.</summary>
        public static ShownStats ShownFor(CharacterSpec spec, int variant)
        {
            if (spec == null) return null;
            if (variant <= 0 || !IsValidVariant(spec.Id, variant)) return spec.Shown;
            return Resolve(spec, variant).Shown;
        }

        static Built Resolve(CharacterSpec spec, int variant)
        {
            string key = spec.Id + "#" + variant;
            lock (built)
            {
                Built b;
                if (built.TryGetValue(key, out b)) return b;
                HeroRow r = Row(spec.Id, variant);
                CharacterStats own = spec.Stats ?? CharacterStats.Default;
                CharacterStats stats = MergeTierDefs.Cap(new CharacterStats
                {
                    JumpLaunchTenths = r.LaunchTenths,
                    MaxSpeed = r.Speed,
                    Control = r.Control,
                    WallBoostHundredths = r.Wall,
                    CoinFind = own.CoinFind,
                    GemFind = own.GemFind,
                    RunBonusCoins = own.RunBonusCoins,
                    RunBonusGems = own.RunBonusGems,
                });
                b = new Built { Stats = stats, Shown = ShownStats.From(stats, new SimConfig()) };
                built[key] = b;
                return b;
            }
        }
    }
}
