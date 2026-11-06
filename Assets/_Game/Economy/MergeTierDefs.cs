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
        /// <summary>Whole percent added to the PREVIOUS tier's SHOWN stat (Common for Uncommon; hero-sheet numbers: Jump as height, Speed, Control, Wall Bounce), 0 = unchanged. Exactly <see cref="TierRule.BoostedStats"/> of these four are above 0 (<see cref="TierRules.Validate"/>).</summary>
        public int JumpPct, SpeedPct, ControlPct, WallPct;
        /// <summary>Added to the Common hero's own end-of-run bonus (docs/29): coins in multiples of 10, gems in ones, inside the tier rule's range.</summary>
        public int RunCoinsAdd, RunGemsAdd;
    }

    /// <summary>The tier characters and the code that turns a row into a <see cref="CharacterSpec"/>. Pure data plus a builder: it never reads CharacterDefs statics itself (the bases are passed in).</summary>
    public static class MergeTierDefs
    {
        /// <summary>
        /// The five Uncommon heroes (rule: <see cref="TierRules"/>). Drawn ONCE with <see cref="TierRules.Generate"/> (seed = <see cref="TierRules.BaseSeed"/> 20261104 + line index 0..4) and
        /// written down as constants; nothing is random at run time. Each hero boosts exactly 2 of the 4 shown stats by 10 to 25 percent over the Common hero, adds 50 to 100 run coins (steps of 10) and 1 to 5 run gems
        /// on top of the Common hero's own bonus, and carries 1 region power (<see cref="TierRules.Pools"/>). To re-randomise a hero: Generate with a new seed and paste the new numbers here.
        /// </summary>
        public static readonly TierRow[] Rows =
        {
            new TierRow { LineId = "momo", Id = "momo_u", ArtId = "momo_u", Name = "Cloudguard Momo", Rarity = CharacterRarity.Uncommon, SpeedPct = 17, WallPct = 13, RunCoinsAdd = 80, RunGemsAdd = 4 },
            new TierRow { LineId = "forestgirl", Id = "forestgirl_u", ArtId = "forestgirl_u", Name = "Thornveil Capi", Rarity = CharacterRarity.Uncommon, JumpPct = 20, ControlPct = 13, RunCoinsAdd = 90, RunGemsAdd = 1 },
            new TierRow { LineId = "beachotter", Id = "beachotter_u", ArtId = "beachotter_u", Name = "Tidecrest Otto", Rarity = CharacterRarity.Uncommon, JumpPct = 15, WallPct = 12, RunCoinsAdd = 50, RunGemsAdd = 2 },
            new TierRow { LineId = "candygirl", Id = "candygirl_u", ArtId = "candygirl_u", Name = "Sugarplate Bonbon", Rarity = CharacterRarity.Uncommon, JumpPct = 24, ControlPct = 14, RunCoinsAdd = 90, RunGemsAdd = 5 },
            new TierRow { LineId = "monk", Id = "monk_u", ArtId = "monk_u", Name = "Ironbamboo Bao", Rarity = CharacterRarity.Uncommon, JumpPct = 16, ControlPct = 19, RunCoinsAdd = 80, RunGemsAdd = 4 },
            // Region 1 tiers (F4.1, owner 2026-11-06): Momo Rare / Elite / Legendary and Uncommon to Legendary of the four new heroes. Drawn once by Tools/tier_rows.py (the TierRules.Generate arithmetic,
            // seed BaseSeed + line index + 1000 x tier, the hero's identity stat always boosted: Bumble speed, Ribbi jump, Dandy control, Pip wall bounce), pasted as constants. The comments are the SHOWN stats.
            // momo common shown: Jump 4.5 Speed 9.0 Control 30.0 Wall 6.9
            new TierRow { LineId = "momo", Id = "momo_r", ArtId = "momo_r", Name = "Cloudknight Momo", Rarity = CharacterRarity.Rare, SpeedPct = 14, ControlPct = 16, WallPct = 19, RunCoinsAdd = 350, RunGemsAdd = 6 }, // shown 4.5 / 12.0 / 34.8 / 9.3
            new TierRow { LineId = "momo", Id = "momo_e", ArtId = "momo_e", Name = "Skyplate Momo", Rarity = CharacterRarity.Epic, SpeedPct = 18, ControlPct = 12, WallPct = 23, RunCoinsAdd = 690, RunGemsAdd = 7 }, // shown 4.5 / 14.2 / 39.0 / 11.4
            new TierRow { LineId = "momo", Id = "momo_l", ArtId = "momo_l", Name = "Sunlord Momo", Rarity = CharacterRarity.Legendary, JumpPct = 24, SpeedPct = 13, ControlPct = 17, WallPct = 22, RunCoinsAdd = 1470, RunGemsAdd = 8 }, // shown 5.6 / 16.0 / 45.6 / 13.9
            // bumble common shown: Jump 4.5 Speed 10.0 Control 31.0 Wall 7.6
            new TierRow { LineId = "bumble", Id = "bumble_u", ArtId = "bumble_u", Name = "Honeycloak Bumble", Rarity = CharacterRarity.Uncommon, SpeedPct = 15, ControlPct = 15, RunCoinsAdd = 60, RunGemsAdd = 2 }, // shown 4.5 / 11.5 / 35.6 / 7.6
            new TierRow { LineId = "bumble", Id = "bumble_r", ArtId = "bumble_r", Name = "Hivemage Bumble", Rarity = CharacterRarity.Rare, JumpPct = 16, SpeedPct = 21, ControlPct = 12, RunCoinsAdd = 410, RunGemsAdd = 8 }, // shown 5.2 / 13.9 / 39.9 / 7.6
            new TierRow { LineId = "bumble", Id = "bumble_e", ArtId = "bumble_e", Name = "Combweaver Bumble", Rarity = CharacterRarity.Epic, JumpPct = 12, SpeedPct = 13, WallPct = 18, RunCoinsAdd = 690, RunGemsAdd = 5 }, // shown 5.8 / 15.7 / 39.9 / 9.0
            new TierRow { LineId = "bumble", Id = "bumble_l", ArtId = "bumble_l", Name = "Archmage Bumble", Rarity = CharacterRarity.Legendary, JumpPct = 19, SpeedPct = 11, ControlPct = 22, WallPct = 21, RunCoinsAdd = 1390, RunGemsAdd = 6 }, // shown 6.9 / 17.4 / 48.7 / 10.9
            // ribbi common shown: Jump 5.5 Speed 9.0 Control 28.0 Wall 6.7
            new TierRow { LineId = "ribbi", Id = "ribbi_u", ArtId = "ribbi_u", Name = "Leafcrown Ribbi", Rarity = CharacterRarity.Uncommon, JumpPct = 13, WallPct = 22, RunCoinsAdd = 100, RunGemsAdd = 1 }, // shown 6.2 / 9.0 / 28.0 / 8.2
            new TierRow { LineId = "ribbi", Id = "ribbi_r", ArtId = "ribbi_r", Name = "Barkplate Ribbi", Rarity = CharacterRarity.Rare, JumpPct = 11, SpeedPct = 11, ControlPct = 18, RunCoinsAdd = 330, RunGemsAdd = 10 }, // shown 6.9 / 10.0 / 33.0 / 8.2
            new TierRow { LineId = "ribbi", Id = "ribbi_e", ArtId = "ribbi_e", Name = "Mossknight Ribbi", Rarity = CharacterRarity.Epic, JumpPct = 23, ControlPct = 24, WallPct = 22, RunCoinsAdd = 530, RunGemsAdd = 7 }, // shown 8.5 / 10.0 / 40.9 / 10.0
            new TierRow { LineId = "ribbi", Id = "ribbi_l", ArtId = "ribbi_l", Name = "Jadecrown Ribbi", Rarity = CharacterRarity.Legendary, JumpPct = 12, SpeedPct = 13, ControlPct = 17, WallPct = 12, RunCoinsAdd = 1350, RunGemsAdd = 9 }, // shown 9.5 / 11.3 / 47.9 / 11.2
            // dandy common shown: Jump 4.5 Speed 8.0 Control 33.0 Wall 5.8
            new TierRow { LineId = "dandy", Id = "dandy_u", ArtId = "dandy_u", Name = "Seedling Dandy", Rarity = CharacterRarity.Uncommon, JumpPct = 16, ControlPct = 24, RunCoinsAdd = 70, RunGemsAdd = 5 }, // shown 5.2 / 8.0 / 40.9 / 5.8
            new TierRow { LineId = "dandy", Id = "dandy_r", ArtId = "dandy_r", Name = "Blossom Dandy", Rarity = CharacterRarity.Rare, JumpPct = 10, SpeedPct = 13, ControlPct = 19, RunCoinsAdd = 210, RunGemsAdd = 8 }, // shown 5.7 / 9.0 / 48.7 / 5.8
            new TierRow { LineId = "dandy", Id = "dandy_e", ArtId = "dandy_e", Name = "Petalcloak Dandy", Rarity = CharacterRarity.Epic, JumpPct = 12, ControlPct = 22, WallPct = 21, RunCoinsAdd = 560, RunGemsAdd = 5 }, // shown 6.4 / 9.0 / 59.4 / 7.0
            new TierRow { LineId = "dandy", Id = "dandy_l", ArtId = "dandy_l", Name = "Bloomqueen Dandy", Rarity = CharacterRarity.Legendary, JumpPct = 23, SpeedPct = 23, ControlPct = 17, WallPct = 10, RunCoinsAdd = 1550, RunGemsAdd = 6 }, // shown 7.9 / 11.1 / 69.5 / 7.7
            // pip common shown: Jump 4.5 Speed 9.0 Control 30.0 Wall 7.6
            new TierRow { LineId = "pip", Id = "pip_u", ArtId = "pip_u", Name = "Redcape Pip", Rarity = CharacterRarity.Uncommon, SpeedPct = 20, WallPct = 21, RunCoinsAdd = 50, RunGemsAdd = 4 }, // shown 4.5 / 10.8 / 30.0 / 9.2
            new TierRow { LineId = "pip", Id = "pip_r", ArtId = "pip_r", Name = "Crownguard Pip", Rarity = CharacterRarity.Rare, SpeedPct = 15, ControlPct = 16, WallPct = 13, RunCoinsAdd = 390, RunGemsAdd = 10 }, // shown 4.5 / 12.4 / 34.8 / 10.4
            new TierRow { LineId = "pip", Id = "pip_e", ArtId = "pip_e", Name = "Thornplate Pip", Rarity = CharacterRarity.Epic, JumpPct = 16, ControlPct = 18, WallPct = 13, RunCoinsAdd = 980, RunGemsAdd = 5 }, // shown 5.2 / 12.4 / 41.1 / 11.8
            new TierRow { LineId = "pip", Id = "pip_l", ArtId = "pip_l", Name = "Goldshell Pip", Rarity = CharacterRarity.Legendary, JumpPct = 25, SpeedPct = 19, ControlPct = 22, WallPct = 15, RunCoinsAdd = 2000, RunGemsAdd = 9 }, // shown 6.5 / 14.8 / 50.1 / 13.6
            // Region 2 tiers (F4.2, owner 2026-11-06): Capi Rare / Elite / Legendary and Nib, Spike, Moss, Fenn Uncommon to Legendary, drawn the same way (Tools/tier_rows.py; identity: Moss wall bounce; Nib and Fenn take the better of two draws for run coins and gems).
            // forestgirl common shown: Jump 4.5 Speed 10.0 Control 29.0 Wall 7.9
            new TierRow { LineId = "forestgirl", Id = "forestgirl_r", ArtId = "forestgirl_r", Name = "Acornplate Capi", Rarity = CharacterRarity.Rare, JumpPct = 22, SpeedPct = 24, WallPct = 22, RunCoinsAdd = 270, RunGemsAdd = 8 }, // shown 6.6 / 12.4 / 32.8 / 9.6
            new TierRow { LineId = "forestgirl", Id = "forestgirl_e", ArtId = "forestgirl_e", Name = "Oakmantle Capi", Rarity = CharacterRarity.Epic, JumpPct = 20, SpeedPct = 15, WallPct = 24, RunCoinsAdd = 980, RunGemsAdd = 5 }, // shown 7.9 / 14.3 / 32.8 / 11.9
            new TierRow { LineId = "forestgirl", Id = "forestgirl_l", ArtId = "forestgirl_l", Name = "Grovequeen Capi", Rarity = CharacterRarity.Legendary, JumpPct = 20, SpeedPct = 10, ControlPct = 15, WallPct = 23, RunCoinsAdd = 1910, RunGemsAdd = 7 }, // shown 9.5 / 15.7 / 37.7 / 14.6
            // nib common shown: Jump 4.5 Speed 10.0 Control 29.0 Wall 7.5
            new TierRow { LineId = "nib", Id = "nib_u", ArtId = "nib_u", Name = "Leafscarf Nib", Rarity = CharacterRarity.Uncommon, ControlPct = 24, WallPct = 23, RunCoinsAdd = 90, RunGemsAdd = 3 }, // shown 4.5 / 10.0 / 36.0 / 9.2
            new TierRow { LineId = "nib", Id = "nib_r", ArtId = "nib_r", Name = "Barkguard Nib", Rarity = CharacterRarity.Rare, JumpPct = 22, SpeedPct = 21, WallPct = 25, RunCoinsAdd = 230, RunGemsAdd = 9 }, // shown 5.5 / 12.1 / 36.0 / 11.5
            new TierRow { LineId = "nib", Id = "nib_e", ArtId = "nib_e", Name = "Oakknight Nib", Rarity = CharacterRarity.Epic, SpeedPct = 13, ControlPct = 19, WallPct = 21, RunCoinsAdd = 810, RunGemsAdd = 5 }, // shown 5.5 / 13.7 / 42.8 / 13.9
            new TierRow { LineId = "nib", Id = "nib_l", ArtId = "nib_l", Name = "Acornking Nib", Rarity = CharacterRarity.Legendary, JumpPct = 13, SpeedPct = 24, ControlPct = 13, WallPct = 17, RunCoinsAdd = 1940, RunGemsAdd = 6 }, // shown 6.2 / 17.0 / 48.4 / 16.3
            // spike common shown: Jump 5.0 Speed 8.0 Control 28.0 Wall 6.4
            new TierRow { LineId = "spike", Id = "spike_u", ArtId = "spike_u", Name = "Logplate Spike", Rarity = CharacterRarity.Uncommon, SpeedPct = 20, ControlPct = 12, RunCoinsAdd = 50, RunGemsAdd = 3 }, // shown 5.0 / 9.6 / 31.4 / 6.4
            new TierRow { LineId = "spike", Id = "spike_r", ArtId = "spike_r", Name = "Thornguard Spike", Rarity = CharacterRarity.Rare, JumpPct = 10, ControlPct = 11, WallPct = 19, RunCoinsAdd = 220, RunGemsAdd = 8 }, // shown 5.5 / 9.6 / 34.9 / 7.6
            new TierRow { LineId = "spike", Id = "spike_e", ArtId = "spike_e", Name = "Oakbrace Spike", Rarity = CharacterRarity.Epic, JumpPct = 24, ControlPct = 11, WallPct = 16, RunCoinsAdd = 870, RunGemsAdd = 9 }, // shown 6.8 / 9.6 / 38.7 / 8.8
            new TierRow { LineId = "spike", Id = "spike_l", ArtId = "spike_l", Name = "Quillking Spike", Rarity = CharacterRarity.Legendary, JumpPct = 16, SpeedPct = 14, ControlPct = 11, WallPct = 25, RunCoinsAdd = 1970, RunGemsAdd = 8 }, // shown 7.9 / 10.9 / 43.0 / 11.0
            // moss common shown: Jump 4.5 Speed 8.0 Control 31.0 Wall 6.8
            new TierRow { LineId = "moss", Id = "moss_u", ArtId = "moss_u", Name = "Vinebelt Moss", Rarity = CharacterRarity.Uncommon, JumpPct = 24, WallPct = 16, RunCoinsAdd = 70, RunGemsAdd = 2 }, // shown 5.6 / 8.0 / 31.0 / 7.9
            new TierRow { LineId = "moss", Id = "moss_r", ArtId = "moss_r", Name = "Shellplate Moss", Rarity = CharacterRarity.Rare, SpeedPct = 16, ControlPct = 14, WallPct = 14, RunCoinsAdd = 260, RunGemsAdd = 5 }, // shown 5.6 / 9.3 / 35.3 / 9.0
            new TierRow { LineId = "moss", Id = "moss_e", ArtId = "moss_e", Name = "Ferncrown Moss", Rarity = CharacterRarity.Epic, JumpPct = 23, SpeedPct = 16, WallPct = 12, RunCoinsAdd = 850, RunGemsAdd = 6 }, // shown 6.9 / 10.8 / 35.3 / 10.1
            new TierRow { LineId = "moss", Id = "moss_l", ArtId = "moss_l", Name = "Shellking Moss", Rarity = CharacterRarity.Legendary, JumpPct = 13, SpeedPct = 12, ControlPct = 19, WallPct = 23, RunCoinsAdd = 1040, RunGemsAdd = 6 }, // shown 7.8 / 12.1 / 42.0 / 12.4
            // fenn common shown: Jump 5.0 Speed 10.0 Control 31.0 Wall 7.4
            new TierRow { LineId = "fenn", Id = "fenn_u", ArtId = "fenn_u", Name = "Scoutvest Fenn", Rarity = CharacterRarity.Uncommon, JumpPct = 12, SpeedPct = 13, RunCoinsAdd = 100, RunGemsAdd = 4 }, // shown 5.6 / 11.3 / 31.0 / 7.4
            new TierRow { LineId = "fenn", Id = "fenn_r", ArtId = "fenn_r", Name = "Coinguard Fenn", Rarity = CharacterRarity.Rare, JumpPct = 20, SpeedPct = 25, WallPct = 14, RunCoinsAdd = 290, RunGemsAdd = 10 }, // shown 6.7 / 14.1 / 31.0 / 8.4
            new TierRow { LineId = "fenn", Id = "fenn_e", ArtId = "fenn_e", Name = "Goldcape Fenn", Rarity = CharacterRarity.Epic, SpeedPct = 19, ControlPct = 20, WallPct = 11, RunCoinsAdd = 960, RunGemsAdd = 10 }, // shown 6.7 / 16.8 / 37.2 / 9.3
            new TierRow { LineId = "fenn", Id = "fenn_l", ArtId = "fenn_l", Name = "Foxking Fenn", Rarity = CharacterRarity.Legendary, JumpPct = 16, SpeedPct = 23, ControlPct = 25, WallPct = 10, RunCoinsAdd = 1960, RunGemsAdd = 10 }, // shown 7.8 / 20.7 / 46.5 / 10.2
            // Region 3 tiers (F4.3, owner 2026-11-06): Otto Rare / Elite / Legendary and Pinch, Shelly, Bloop, Coco Uncommon to Legendary, drawn the same way (Tools/tier_rows.py; identity: Pinch wall bounce, Shelly control, Bloop jump; Coco takes the better of two draws for run coins and gems).
            // beachotter common shown: Jump 4.1 Speed 8.0 Control 32.0 Wall 5.8
            new TierRow { LineId = "beachotter", Id = "beachotter_r", ArtId = "beachotter_r", Name = "Shellplate Otto", Rarity = CharacterRarity.Rare, SpeedPct = 20, ControlPct = 12, WallPct = 17, RunCoinsAdd = 220, RunGemsAdd = 8 }, // shown 4.7 / 9.6 / 35.8 / 7.6
            new TierRow { LineId = "beachotter", Id = "beachotter_e", ArtId = "beachotter_e", Name = "Wavecloak Otto", Rarity = CharacterRarity.Epic, JumpPct = 23, SpeedPct = 22, WallPct = 21, RunCoinsAdd = 900, RunGemsAdd = 5 }, // shown 5.8 / 11.7 / 35.8 / 9.2
            new TierRow { LineId = "beachotter", Id = "beachotter_l", ArtId = "beachotter_l", Name = "Pearlking Otto", Rarity = CharacterRarity.Legendary, JumpPct = 19, SpeedPct = 24, ControlPct = 14, WallPct = 16, RunCoinsAdd = 1070, RunGemsAdd = 9 }, // shown 6.9 / 14.5 / 40.8 / 10.7
            // pinch common shown: Jump 4.5 Speed 8.0 Control 29.0 Wall 6.6
            new TierRow { LineId = "pinch", Id = "pinch_u", ArtId = "pinch_u", Name = "Shellvest Pinch", Rarity = CharacterRarity.Uncommon, SpeedPct = 13, WallPct = 24, RunCoinsAdd = 100, RunGemsAdd = 3 }, // shown 4.5 / 9.0 / 29.0 / 8.2
            new TierRow { LineId = "pinch", Id = "pinch_r", ArtId = "pinch_r", Name = "Clawguard Pinch", Rarity = CharacterRarity.Rare, JumpPct = 18, ControlPct = 23, WallPct = 20, RunCoinsAdd = 360, RunGemsAdd = 9 }, // shown 5.3 / 9.0 / 35.7 / 9.8
            new TierRow { LineId = "pinch", Id = "pinch_e", ArtId = "pinch_e", Name = "Tidecrown Pinch", Rarity = CharacterRarity.Epic, JumpPct = 11, ControlPct = 11, WallPct = 19, RunCoinsAdd = 710, RunGemsAdd = 8 }, // shown 5.9 / 9.0 / 39.6 / 11.7
            new TierRow { LineId = "pinch", Id = "pinch_l", ArtId = "pinch_l", Name = "Reefking Pinch", Rarity = CharacterRarity.Legendary, JumpPct = 10, SpeedPct = 14, ControlPct = 23, WallPct = 25, RunCoinsAdd = 1040, RunGemsAdd = 6 }, // shown 6.5 / 10.3 / 48.7 / 14.6
            // shelly common shown: Jump 4.1 Speed 8.0 Control 33.0 Wall 5.9
            new TierRow { LineId = "shelly", Id = "shelly_u", ArtId = "shelly_u", Name = "Coralstrap Shelly", Rarity = CharacterRarity.Uncommon, ControlPct = 16, WallPct = 10, RunCoinsAdd = 100, RunGemsAdd = 3 }, // shown 4.1 / 8.0 / 38.3 / 6.5
            new TierRow { LineId = "shelly", Id = "shelly_r", ArtId = "shelly_r", Name = "Reefplate Shelly", Rarity = CharacterRarity.Rare, JumpPct = 17, ControlPct = 14, WallPct = 15, RunCoinsAdd = 470, RunGemsAdd = 9 }, // shown 4.8 / 8.0 / 43.7 / 7.5
            new TierRow { LineId = "shelly", Id = "shelly_e", ArtId = "shelly_e", Name = "Pearltiara Shelly", Rarity = CharacterRarity.Epic, JumpPct = 17, SpeedPct = 23, ControlPct = 13, RunCoinsAdd = 660, RunGemsAdd = 6 }, // shown 5.6 / 9.8 / 49.4 / 7.5
            new TierRow { LineId = "shelly", Id = "shelly_l", ArtId = "shelly_l", Name = "Coralqueen Shelly", Rarity = CharacterRarity.Legendary, JumpPct = 20, SpeedPct = 11, ControlPct = 16, WallPct = 19, RunCoinsAdd = 1880, RunGemsAdd = 9 }, // shown 6.7 / 10.9 / 57.3 / 8.9
            // bloop common shown: Jump 5.5 Speed 9.0 Control 28.0 Wall 6.6
            new TierRow { LineId = "bloop", Id = "bloop_u", ArtId = "bloop_u", Name = "Coralband Bloop", Rarity = CharacterRarity.Uncommon, JumpPct = 18, SpeedPct = 12, RunCoinsAdd = 70, RunGemsAdd = 3 }, // shown 6.5 / 10.1 / 28.0 / 6.6
            new TierRow { LineId = "bloop", Id = "bloop_r", ArtId = "bloop_r", Name = "Reefmail Bloop", Rarity = CharacterRarity.Rare, JumpPct = 11, ControlPct = 15, WallPct = 14, RunCoinsAdd = 260, RunGemsAdd = 5 }, // shown 7.2 / 10.1 / 32.2 / 7.5
            new TierRow { LineId = "bloop", Id = "bloop_e", ArtId = "bloop_e", Name = "Bubblecrown Bloop", Rarity = CharacterRarity.Epic, JumpPct = 17, SpeedPct = 13, WallPct = 13, RunCoinsAdd = 530, RunGemsAdd = 7 }, // shown 8.4 / 11.4 / 32.2 / 8.5
            new TierRow { LineId = "bloop", Id = "bloop_l", ArtId = "bloop_l", Name = "Bubbleking Bloop", Rarity = CharacterRarity.Legendary, JumpPct = 20, SpeedPct = 22, ControlPct = 17, WallPct = 16, RunCoinsAdd = 1140, RunGemsAdd = 10 }, // shown 10.1 / 13.9 / 37.7 / 9.9
            // coco common shown: Jump 4.5 Speed 10.0 Control 30.0 Wall 7.5
            new TierRow { LineId = "coco", Id = "coco_u", ArtId = "coco_u", Name = "Palmwrap Coco", Rarity = CharacterRarity.Uncommon, SpeedPct = 14, ControlPct = 16, RunCoinsAdd = 100, RunGemsAdd = 3 }, // shown 4.5 / 11.4 / 34.8 / 7.5
            new TierRow { LineId = "coco", Id = "coco_r", ArtId = "coco_r", Name = "Leafmail Coco", Rarity = CharacterRarity.Rare, JumpPct = 22, SpeedPct = 13, ControlPct = 15, RunCoinsAdd = 330, RunGemsAdd = 8 }, // shown 5.5 / 12.9 / 40.0 / 7.5
            new TierRow { LineId = "coco", Id = "coco_e", ArtId = "coco_e", Name = "Junglecape Coco", Rarity = CharacterRarity.Epic, JumpPct = 13, SpeedPct = 10, ControlPct = 23, RunCoinsAdd = 900, RunGemsAdd = 8 }, // shown 6.2 / 14.2 / 49.2 / 7.5
            new TierRow { LineId = "coco", Id = "coco_l", ArtId = "coco_l", Name = "Palmking Coco", Rarity = CharacterRarity.Legendary, JumpPct = 19, SpeedPct = 25, ControlPct = 24, WallPct = 12, RunCoinsAdd = 1670, RunGemsAdd = 5 }, // shown 7.4 / 17.8 / 61.0 / 8.4
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
                ShownStats shown = ShownOf(row, ShownStats.From(b.Stats ?? CharacterStats.Default, new SimConfig()));
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
                    Shown = shown,
                    Stats = Cap(shown.ToPhysics(new SimConfig())),
                });
            }
            return list.ToArray();
        }

        /// <summary>
        /// The SHOWN stats of a row's hero: start at the Common hero's shown stats and apply every tier up to this row's rarity in order (the chain, owner 2026-11-03: each tier is
        /// a percent over the previous tier's shown numbers). A missing middle tier stops the chain there. NOT limited: the hero sheet and the merge text show this.
        /// </summary>
        public static ShownStats ShownOf(TierRow row, ShownStats common)
        {
            ShownStats cur = common;
            for (int r = 1; r <= (int)row.Rarity; r++)
            {
                TierRow t = null;
                foreach (TierRow x in Rows) if (x.LineId == row.LineId && (int)x.Rarity == r) { t = x; break; }
                if (t == null) break;
                cur = TierRules.Apply(cur, common, t);
            }
            return cur;
        }

        /// <summary>
        /// The effective stats: the four physics stats clamped to the documented safe limits (docs/29 section 7) so every level stays solvable and not trivialised. The run bonus
        /// (meta only, never in the simulation) is kept in full. A shown value above a limit is shown on the sheet but plays at the limit (the chain over four tiers can pass them: docs/34 section 2b).
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

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
