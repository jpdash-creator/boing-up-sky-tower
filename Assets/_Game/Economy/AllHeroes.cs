using System;
using System.Collections.Generic;

namespace BoingUpSkyTower.Economy
{
    /// <summary>One card of the All Heroes screen: a real character, or a placeholder for a tier that has no hero data yet (Rare, Elite, Legendary).</summary>
    public sealed class AllHeroEntry
    {
        public string Id, Name, BaseName, LineId, HowToGet;
        public CharacterRarity Rarity;
        public int Region;
        /// <summary>True when no CharacterSpec exists for this tier yet (the card is a locked placeholder with the rarity colour and a silhouette of the line's Common hero).</summary>
        public bool Placeholder;
        /// <summary>True when the player does not own it (every placeholder is locked).</summary>
        public bool Locked;
        /// <summary>The real character, or null for a placeholder.</summary>
        public CharacterSpec Spec;
        /// <summary>The Common hero of the line (art for the silhouette).</summary>
        public CharacterSpec BaseSpec;
        public string RarityName { get { return CharacterDefs.RarityNames[(int)Rarity]; } }
    }

    /// <summary>One region banner and its heroes.</summary>
    public sealed class AllHeroGroup
    {
        public int Region;
        public string RegionName;
        public List<AllHeroEntry> Entries = new List<AllHeroEntry>(8);
    }

    /// <summary>
    /// The data of the All Heroes screen (docs/34 section 10), pure and unit-testable: EVERY character of the game grouped by region (data driven: a region exists as a group
    /// as soon as a Common hero names it, more heroes per region later need no code). Per Common hero the line is listed Common, Uncommon, Rare, Elite, Legendary; a tier that
    /// has no hero data yet becomes a locked placeholder with the rarity colour, so the grid already shows the whole ladder.
    /// </summary>
    public static class AllHeroes
    {
        /// <summary>Id of a placeholder tier, for example "momo_rare".</summary>
        public static string PlaceholderId(string lineId, CharacterRarity r) { return lineId + "_" + r.ToString().ToLowerInvariant(); }

        /// <summary>The line's tier of this rarity: the real character, or null.</summary>
        static CharacterSpec TierOf(string lineId, CharacterRarity r)
        {
            foreach (CharacterSpec c in CharacterInventory.All) if (c.LineId == lineId && c.Rarity == r) return c;
            return null;
        }

        /// <summary>
        /// Builds the groups, ordered by region index (region 0 = heroes tied to no region, last). <paramref name="regionName"/> names a region (null result = "Region n").
        /// <paramref name="owns"/> says whether the player owns a real character id (placeholders are always locked).
        /// </summary>
        public static List<AllHeroGroup> Build(Func<int, string> regionName, Func<string, bool> owns)
        {
            var groups = new List<AllHeroGroup>(6);
            foreach (CharacterSpec b in CharacterInventory.All)
            {
                if (b.IsMergeTier) continue;   // the tiers are listed under their Common hero
                AllHeroGroup g = null;
                foreach (AllHeroGroup x in groups) if (x.Region == b.Region) { g = x; break; }
                if (g == null)
                {
                    string rn = regionName != null ? regionName(b.Region) : null;
                    g = new AllHeroGroup { Region = b.Region, RegionName = !string.IsNullOrEmpty(rn) ? rn : (b.Region > 0 ? "Region " + b.Region : "More heroes") };
                    groups.Add(g);
                }
                for (int r = (int)CharacterRarity.Common; r <= (int)CharacterRarity.Legendary; r++)
                {
                    var rar = (CharacterRarity)r;
                    CharacterSpec s = rar == b.Rarity ? b : TierOf(b.Id, rar);
                    var e = new AllHeroEntry { LineId = b.Id, BaseName = b.Name, Rarity = rar, Region = b.Region, Spec = s, BaseSpec = b, Placeholder = s == null };
                    e.Id = s != null ? s.Id : PlaceholderId(b.Id, rar);
                    e.Name = s != null ? s.Name : CharacterDefs.RarityNames[r] + " " + b.Name;
                    e.Locked = s == null || owns == null || !owns(s.Id);
                    e.HowToGet = HowToGet(e, regionName);
                    g.Entries.Add(e);
                }
            }
            groups.Sort((a, c) => (a.Region == 0 ? int.MaxValue : a.Region).CompareTo(c.Region == 0 ? int.MaxValue : c.Region));
            return groups;
        }

        /// <summary>How to get a hero: "Merge 3 &lt;tier below&gt;" for a merge tier (real or not made yet), the region for a Common hero, else the reward sources.</summary>
        public static string HowToGet(AllHeroEntry e, Func<int, string> regionName)
        {
            if (e.Rarity > CharacterRarity.Common)
            {
                var below = (CharacterRarity)((int)e.Rarity - 1);
                return "Merge " + MergeRules.CopiesNeeded + " " + CharacterDefs.RarityNames[(int)below] + " " + e.BaseName;
            }
            if (e.Region > 0)
            {
                string rn = regionName != null ? regionName(e.Region) : null;
                return "Unlock " + (string.IsNullOrEmpty(rn) ? "Region " + e.Region : rn) + " or find it in Bundles / Chests / Rewards";
            }
            return "Bundles / Chests / Rewards";
        }

        /// <summary>Every entry of every group, in screen order.</summary>
        public static List<AllHeroEntry> Flatten(IList<AllHeroGroup> groups)
        {
            var list = new List<AllHeroEntry>(32);
            if (groups != null) foreach (AllHeroGroup g in groups) list.AddRange(g.Entries);
            return list;
        }
    }
}
