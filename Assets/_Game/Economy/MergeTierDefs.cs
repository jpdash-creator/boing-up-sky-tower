using System.Collections.Generic;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// One higher-tier character as plain data (docs/34): which hero line it belongs to, its own id, name and art, and how its stats differ from the Common hero of the line.
    /// Adding Rare, Elite or Legendary later is one more row here (and art in Resources/Characters/id); nothing else changes.
    /// </summary>
    public sealed class TierRow
    {
        public string LineId;
        public string Id;
        public string Name;
        public CharacterRarity Rarity;
        /// <summary>
        /// Art slot: the pictures live in Resources/Characters/{Id}/ (same part files and rig.json as the Common hero). Until that folder exists the game draws the
        /// line's Common hero instead, tinted, so the tier is visible as a placeholder. Dropping the real art into the folder swaps it with no code change.
        /// </summary>
        public string ArtId;
        /// <summary>Whole percent added to the Common hero's stored value, 0 = unchanged. Exactly <see cref="TierRule.BoostedStats"/> of these four are above 0 (<see cref="TierRules.Validate"/>).</summary>
        public int JumpPct, SpeedPct, ControlPct, WallPct;
        /// <summary>Added to the Common hero's own end-of-run bonus (docs/29): coins in multiples of 10, gems in ones, inside the tier rule's range.</summary>
        public int RunCoinsAdd, RunGemsAdd;
    }

    /// <summary>The tier characters and the code that turns a row into a <see cref="CharacterSpec"/>. Pure data plus a builder: it never reads CharacterDefs statics itself (the bases are passed in).</summary>
    public static class MergeTierDefs
    {
        /// <summary>
        /// The five Uncommon heroes (rule: <see cref="TierRules"/>). Drawn ONCE with <see cref="TierRules.Generate"/> (seed = <see cref="TierRules.BaseSeed"/> + line index 0..4) and
        /// written down as constants; nothing is random at run time. Each hero boosts exactly 2 of the 4 stats by 10 to 25 percent, adds 50 to 100 run coins (steps of 10) and 1 to 5 run gems
        /// on top of the Common hero's own bonus, and carries 1 region power (<see cref="TierRules.Pools"/>). To re-randomise a hero: Generate with a new seed and paste the new numbers here.
        /// </summary>
        public static readonly TierRow[] Rows =
        {
            new TierRow { LineId = "momo", Id = "momo_u", ArtId = "momo_u", Name = "Cloudguard Momo", Rarity = CharacterRarity.Uncommon, JumpPct = 17, WallPct = 22, RunCoinsAdd = 60, RunGemsAdd = 5 },
            new TierRow { LineId = "forestgirl", Id = "forestgirl_u", ArtId = "forestgirl_u", Name = "Thornveil Capi", Rarity = CharacterRarity.Uncommon, SpeedPct = 17, WallPct = 13, RunCoinsAdd = 80, RunGemsAdd = 4 },
            new TierRow { LineId = "beachotter", Id = "beachotter_u", ArtId = "beachotter_u", Name = "Tidecrest Otto", Rarity = CharacterRarity.Uncommon, JumpPct = 20, ControlPct = 13, RunCoinsAdd = 90, RunGemsAdd = 1 },
            new TierRow { LineId = "candygirl", Id = "candygirl_u", ArtId = "candygirl_u", Name = "Sugarplate Bonbon", Rarity = CharacterRarity.Uncommon, JumpPct = 11, WallPct = 15, RunCoinsAdd = 90, RunGemsAdd = 5 },
            new TierRow { LineId = "monk", Id = "monk_u", ArtId = "monk_u", Name = "Ironbamboo Bao", Rarity = CharacterRarity.Uncommon, JumpPct = 12, ControlPct = 24, RunCoinsAdd = 90, RunGemsAdd = 4 },
        };

        /// <summary>Builds the specs of all rows whose line has a Common hero among <paramref name="bases"/>.</summary>
        public static CharacterSpec[] Create(IList<CharacterSpec> bases)
        {
            var list = new List<CharacterSpec>(Rows.Length);
            foreach (TierRow row in Rows)
            {
                CharacterSpec b = null;
                foreach (CharacterSpec c in bases) if (c.Id == row.LineId) { b = c; break; }
                if (b == null) continue;
                CharacterStats display = Boost(b.Stats ?? CharacterStats.Default, row);
                list.Add(new CharacterSpec
                {
                    Id = row.Id,
                    Name = row.Name,
                    Rarity = row.Rarity,
                    Region = 0,
                    Line = row.LineId,
                    ArtId = row.ArtId,
                    FallbackArtId = b.FallbackArtId ?? b.Id,
                    IsMergeTier = true,
                    Powers = TierRules.PowersFor(row.LineId, row.Rarity),
                    DisplayStats = display,
                    Stats = Cap(display),
                });
            }
            return list.ToArray();
        }

        /// <summary>
        /// The stats as the rule gives them: the Common stats with the row's percent on the boosted fields (percent over the stored value, rounded to the nearest whole
        /// number) and the run bonus added on top of the Common hero's own. NOT limited: this is what the hero sheet shows. Deterministic. Use <see cref="Cap"/> for what the simulation plays.
        /// </summary>
        public static CharacterStats Boost(CharacterStats b, TierRow r)
        {
            return new CharacterStats
            {
                JumpLaunch = Pct(b.JumpLaunch, r.JumpPct),
                MaxSpeed = Pct(b.MaxSpeed, r.SpeedPct),
                Control = Pct(b.Control, r.ControlPct),
                WallBoostHundredths = Pct(b.WallBoostHundredths, r.WallPct),
                CoinFind = b.CoinFind,
                GemFind = b.GemFind,
                RunBonusCoins = b.RunBonusCoins + r.RunCoinsAdd,
                RunBonusGems = b.RunBonusGems + r.RunGemsAdd,
            };
        }

        /// <summary>
        /// The effective stats: the four physics stats clamped to the documented safe limits (docs/29 section 7) so every level stays solvable and not trivialised. The run bonus
        /// (meta only, never in the simulation) is kept in full. A boosted value above a limit is shown on the sheet but plays at the limit.
        /// </summary>
        public static CharacterStats Cap(CharacterStats d)
        {
            return new CharacterStats
            {
                JumpLaunch = Clamp(d.JumpLaunch, CharacterStats.JumpLaunchMin, CharacterStats.JumpLaunchMax),
                MaxSpeed = Clamp(d.MaxSpeed, CharacterStats.MaxSpeedMin, CharacterStats.MaxSpeedMax),
                Control = Clamp(d.Control, CharacterStats.ControlMin, CharacterStats.ControlMax),
                WallBoostHundredths = Clamp(d.WallBoostHundredths, CharacterStats.WallBoostMin, CharacterStats.WallBoostMax),
                CoinFind = Clamp(d.CoinFind, 0, CharacterStats.CoinFindMax),
                GemFind = Clamp(d.GemFind, 0, CharacterStats.GemFindMax),
                RunBonusCoins = d.RunBonusCoins,
                RunBonusGems = d.RunBonusGems,
            };
        }

        static int Pct(int value, int pct)
        {
            return (value * (100 + pct) + 50) / 100;
        }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
