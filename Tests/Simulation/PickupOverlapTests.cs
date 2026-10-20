using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>Owner report 2026-10-21: gems and stars sometimes sit in the same place and overlap. Stars and gems must keep a visible gap.</summary>
    public class PickupOverlapTests
    {
        readonly ITestOutputHelper output;
        public PickupOverlapTests(ITestOutputHelper output) { this.output = output; }

        /// <summary>Minimum centre distance, in world units, between a star or gem and any other star, gem or hidden gem.</summary>
        const double MinGap = 1.0;

        [Fact]
        public void StarsAndGemsNeverOverlapEachOther()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) { output.WriteLine("Content not found; skipped"); return; }
            SimConfig cfg = new SimConfig();
            var offenders = new List<string>();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Pickup[] p = d.Pickups;
                for (int i = 0; i < p.Length; i++)
                {
                    if (p[i].Kind == PickupKind.Coin) continue;
                    for (int j = i + 1; j < p.Length; j++)
                    {
                        if (p[j].Kind == PickupKind.Coin) continue;
                        double dx = (p[i].X - p[j].X).Raw / 65536.0, dy = (p[i].Y - p[j].Y).Raw / 65536.0;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < MinGap)
                            offenders.Add(ll.Id + " " + p[i].Kind + "@" + (p[i].X.Raw / 65536.0).ToString("F1") + "," + (p[i].Y.Raw / 65536.0).ToString("F1") + " vs " + p[j].Kind + "#" + p[j].Id + "@" + (p[j].X.Raw / 65536.0).ToString("F2") + "," + (p[j].Y.Raw / 65536.0).ToString("F2") + " (i#" + p[i].Id + ") d=" + dist.ToString("F2"));
                    }
                }
            }
            foreach (string o in offenders) output.WriteLine(o);
            Assert.Empty(offenders);
        }
    }
}
