using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>The hidden master list (docs/36): complete, ordered by hero then tier, and a copy so nobody can change it.</summary>
    public class CharacterInventoryTests
    {
        [Fact]
        public void HoldsEveryCharacterExactlyOnce()
        {
            Assert.Equal(CharacterDefs.All.Length, CharacterInventory.Count);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (CharacterSpec c in CharacterInventory.All) Assert.True(seen.Add(c.Id), c.Id);
        }

        [Fact]
        public void OrderedByHeroThenTier()
        {
            CharacterSpec[] all = CharacterInventory.All;
            Assert.Equal("momo", all[0].Id);
            Assert.Equal("momo_u", all[1].Id);
            for (int i = 1; i < all.Length; i++)
                if (all[i].LineId == all[i - 1].LineId) Assert.True(all[i].Rarity > all[i - 1].Rarity);
            Assert.Equal(0, CharacterInventory.TierOf("forestgirl"));
            Assert.Equal(1, CharacterInventory.TierOf("forestgirl_u"));
            Assert.Equal(-1, CharacterInventory.OrderOf("ghost"));
        }

        [Fact]
        public void AllIsACopy()
        {
            CharacterSpec[] a = CharacterInventory.All;
            a[0] = null;
            Assert.NotNull(CharacterInventory.All[0]);
        }
    }
}
