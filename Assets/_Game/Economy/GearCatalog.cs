using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using UnityEngine;

namespace BoingUpSkyTower.Economy
{
    /// <summary>The three wearable slots (docs/19 section A), in the order head, body, boots. There is no pants slot.</summary>
    public enum GearSlot
    {
        Head = 0,
        Body = 1,
        Boots = 2
    }

    /// <summary>Rarity, lowest first. Shown by tile colour AND by 1 to 5 pips, never colour alone (CUX-6).</summary>
    public enum GearRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>One of the 180 wearable pieces. Boost: the single power-up it makes stronger, by Percent of that power-up's duration or effect.</summary>
    public sealed class GearDef
    {
        public string Id;
        public GearSlot Slot;
        public GearRarity Rarity;
        /// <summary>1-based number of the piece inside its slot and rarity (1..20 for Common ...).</summary>
        public int Number;
        public string Name;
        public PowerUp Boost;
        /// <summary>Fixed boost: 10, 20, 40, 75 or 100 percent by rarity.</summary>
        public int BoostPercent;
        /// <summary>Folder name of the piece's polygon parts (Resources/Characters/gear/&lt;ArtId&gt;/): same as Id.</summary>
        public string ArtId;
    }

    /// <summary>
    /// The gear table (docs/19 section A): 3 slots x (Common 20, Uncommon 16, Rare 12, Epic 8, Legendary 4) = 180 pieces. Each piece boosts one
    /// power-up, rotating inside a rarity band: head Shield / Slow Time, body Magnet / Flight / Wall Stick, boots Double Jump / Rocket.
    /// Built in code (deterministic, names from word lists) so the table never drifts from the rules.
    /// </summary>
    public static class GearCatalog
    {
        public const int SlotCount = 3;
        public const int RarityCount = 5;

        public static readonly int[] PiecesPerRarity = { 20, 16, 12, 8, 4 };
        public static readonly int[] BoostPercentByRarity = { 10, 20, 40, 75, 100 };
        public static readonly string[] SlotNames = { "Head", "Body", "Boots" };
        public static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };

        static readonly PowerUp[][] SlotBoosts =
        {
            new[] { PowerUp.Shield, PowerUp.SlowTime },
            new[] { PowerUp.Magnet, PowerUp.Flight, PowerUp.WallStick },
            new[] { PowerUp.DoubleJump, PowerUp.Rocket },
        };

        // Name word lists: a distinct adjective per piece of a rarity band (60 in all) and six nouns per slot, so every name is unique in its slot.
        static readonly string[][] Adjectives =
        {
            new[] { "Plain", "Sunny", "Leafy", "Mossy", "Dusty", "Cozy", "Patchy", "Woolly", "Rustic", "Breezy", "Pebble", "Acorn", "Clover", "Daisy", "Tidy", "Merry", "Soft", "Cheery", "Humble", "Snug" },
            new[] { "Sturdy", "Brave", "Zesty", "Swift", "Jolly", "Lucky", "Spry", "Bold", "Nimble", "Dapper", "Gleam", "Frosty", "Amber", "Coral", "Ivory", "Mint" },
            new[] { "Royal", "Mystic", "Stormy", "Blazing", "Crystal", "Marine", "Velvet", "Thunder", "Aurora", "Cosmic", "Lunar", "Solar" },
            new[] { "Dragon", "Phantom", "Titan", "Phoenix", "Eclipse", "Nebula", "Ember", "Tempest" },
            new[] { "Celestial", "Eternal", "Mythic", "Radiant" },
        };

        static readonly string[][] Nouns =
        {
            new[] { "Cap", "Helm", "Hood", "Crown", "Bandana", "Visor" },
            new[] { "Vest", "Jacket", "Tunic", "Cloak", "Jersey", "Armor" },
            new[] { "Boots", "Sneakers", "Sandals", "Clogs", "Treads", "Slippers" },
        };

        static readonly char[] RarityLetter = { 'c', 'u', 'r', 'e', 'l' };

        /// <summary>Resources folder of the gear parts: Characters/gear/&lt;pieceId&gt;/&lt;part&gt;.png.</summary>
        public const string ArtRoot = "Characters/gear/";

        static GearDef[] all;
        static Dictionary<string, GearDef> byId;
        static GearDef[][][] bySlotRarity;

        public static IReadOnlyList<GearDef> All { get { Ensure(); return all; } }

        static void Ensure()
        {
            if (all != null) return;
            var list = new List<GearDef>(180);
            var map = new Dictionary<string, GearDef>(256);
            var grid = new GearDef[SlotCount][][];
            for (int s = 0; s < SlotCount; s++)
            {
                grid[s] = new GearDef[RarityCount][];
                for (int r = 0; r < RarityCount; r++)
                {
                    int n = PiecesPerRarity[r];
                    grid[s][r] = new GearDef[n];
                    for (int i = 0; i < n; i++)
                    {
                        string id = "gear_" + SlotNames[s].ToLowerInvariant() + "_" + RarityLetter[r] + (i + 1).ToString("D2");
                        var def = new GearDef
                        {
                            Id = id, Slot = (GearSlot)s, Rarity = (GearRarity)r, Number = i + 1,
                            Name = Adjectives[r][i] + " " + Nouns[s][i % Nouns[s].Length],
                            Boost = SlotBoosts[s][i % SlotBoosts[s].Length], BoostPercent = BoostPercentByRarity[r], ArtId = id
                        };
                        list.Add(def);
                        map[id] = def;
                        grid[s][r][i] = def;
                    }
                }
            }
            byId = map;
            bySlotRarity = grid;
            all = list.ToArray();
        }

        public static GearDef Get(string id)
        {
            Ensure();
            GearDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        /// <summary>The pieces of one slot and rarity (20, 16, 12, 8 or 4 of them).</summary>
        public static GearDef[] Pieces(GearSlot slot, GearRarity rarity)
        {
            Ensure();
            return bySlotRarity[(int)slot][(int)rarity];
        }

        // ---- parts and art: a piece without its art never drops and never shows (owner 2026-10-24) ----

        static readonly string[] HeadParts = { "head" };
        static readonly string[] BodyParts = { "body", "sleeveL", "sleeveR" };
        static readonly string[] BodyRequired = { "body" };
        static readonly string[] BootParts = { "bootL", "bootR" };

        /// <summary>Every part picture a piece of the slot can have (full-canvas PNGs). Body gear adds two short sleeves, boots are a pair.</summary>
        public static string[] PartNames(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Head: return HeadParts;
                case GearSlot.Body: return BodyParts;
                default: return BootParts;
            }
        }

        /// <summary>The parts a piece must have to count as painted: head.png, body.png, or both boots. The sleeves are optional.</summary>
        public static string[] RequiredParts(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Head: return HeadParts;
                case GearSlot.Body: return BodyRequired;
                default: return BootParts;
            }
        }

        static Func<GearDef, bool> artProbe;
        static readonly Dictionary<string, bool> hasArt = new Dictionary<string, bool>();

        /// <summary>Overrides the art check (tests). Null restores the default: the required part pictures exist in Resources/Characters/gear/&lt;id&gt;/.</summary>
        public static Func<GearDef, bool> ArtProbe
        {
            get { return artProbe; }
            set { artProbe = value; hasArt.Clear(); }
        }

        /// <summary>Forgets the cached art checks (art files arrived or were removed).</summary>
        public static void ClearArtCache() { hasArt.Clear(); }

        /// <summary>True when the piece has its part pictures. All 180 definitions stay; only these can drop, show, or be worn.</summary>
        public static bool HasArt(GearDef d)
        {
            if (d == null) return false;
            bool v;
            if (hasArt.TryGetValue(d.Id, out v)) return v;
            if (artProbe != null) v = artProbe(d);
            else
            {
                v = true;
                foreach (string part in RequiredParts(d.Slot))
                    if (Resources.Load<Sprite>(ArtRoot + d.ArtId + "/" + part) == null) { v = false; break; }
            }
            hasArt[d.Id] = v;
            return v;
        }

        /// <summary>The pieces of one slot and rarity that have art (possibly none).</summary>
        public static List<GearDef> PiecesWithArt(GearSlot slot, GearRarity rarity)
        {
            var list = new List<GearDef>();
            foreach (GearDef d in Pieces(slot, rarity)) if (HasArt(d)) list.Add(d);
            return list;
        }
    }

    /// <summary>Total boost per power-up from what is worn, applied to the run config at run start.</summary>
    public static class GearBoosts
    {
        /// <summary>
        /// Percent boost per PowerUp (index = enum value 0..11). Different power-ups are separate; when two worn pieces boost the same power-up
        /// the larger one applies, they do not stack (docs/19 section A: nothing stacks across powers).
        /// </summary>
        public static int[] Compute(IList<GearDef> worn)
        {
            var total = new int[PowerUps.MaxType + 1];
            if (worn == null) return total;
            for (int i = 0; i < worn.Count; i++)
            {
                GearDef d = worn[i];
                if (d == null) continue;
                int k = (int)d.Boost;
                if (d.BoostPercent > total[k]) total[k] = d.BoostPercent;
            }
            return total;
        }

        /// <summary>Scales the effect of each boosted power-up in <paramref name="cfg"/>: duration for most, the pull radius for Magnet. Does nothing for zero boosts.</summary>
        public static void Apply(SimConfig cfg, int[] boost)
        {
            if (cfg == null || boost == null) return;
            cfg.FlightTicks = Scale(cfg.FlightTicks, boost[(int)PowerUp.Flight]);
            cfg.RocketTicks = Scale(cfg.RocketTicks, boost[(int)PowerUp.Rocket]);
            cfg.DoubleJumpWindowTicks = Scale(cfg.DoubleJumpWindowTicks, boost[(int)PowerUp.DoubleJump]);
            cfg.WallStickWindowTicks = Scale(cfg.WallStickWindowTicks, boost[(int)PowerUp.WallStick]);
            cfg.ShieldTicks = Scale(cfg.ShieldTicks, boost[(int)PowerUp.Shield]);
            cfg.SlowTimeTicks = Scale(cfg.SlowTimeTicks, boost[(int)PowerUp.SlowTime]);
            int mag = boost[(int)PowerUp.Magnet];
            if (mag > 0) cfg.MagnetExtraRadius = cfg.MagnetExtraRadius * (100 + mag) / 100;
        }

        static int Scale(int ticks, int percent)
        {
            return percent <= 0 ? ticks : (int)((long)ticks * (100 + percent) / 100);
        }
    }
}
