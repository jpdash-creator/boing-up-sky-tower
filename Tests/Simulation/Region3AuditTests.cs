using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>Owner report (QA region 3): a drifting platform swung through the platform beside it on the same row, so two platforms looked stacked and one could be touched inside the other. Region 3 (Sunny Beach, drifting platforms) audit: swing-aware overlap and pickup/platform checks.</summary>
    public class Region3AuditTests
    {
        readonly ITestOutputHelper output;
        public Region3AuditTests(ITestOutputHelper output) { this.output = output; }

        static double D(Fx v) { return v.Raw / 65536.0; }
        // sweep interval of a platform's horizontal extent over time
        static double L(Platform p) { return D(p.X) - D(p.Width) / 2 - D(p.Amplitude); }
        static double R(Platform p) { return D(p.X) + D(p.Width) / 2 + D(p.Amplitude); }

        [Fact]
        public void DriftingPlatformsNeverSweepThroughNeighbours()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            int total = 0;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 3, 3))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Platform[] p = d.Platforms;
                int n = 0, moving = 0, offscreen = 0;
                for (int i = 0; i < p.Length; i++)
                {
                    if (p[i].Kind == PlatformKind.Moving) moving++;
                    if (L(p[i]) < -0.001 || R(p[i]) > 10.001) offscreen++;
                    for (int j = i + 1; j < p.Length; j++)
                    {
                        if (p[i].Amplitude.Raw == 0 && p[j].Amplitude.Raw == 0) continue;
                        double dy = Math.Abs(D(p[i].Y) - D(p[j].Y));
                        if (dy > 0.9) continue;
                        double gap = Math.Max(L(p[j]) - R(p[i]), L(p[i]) - R(p[j]));
                        if (gap < 0.05) { n++; if (n <= 3) output.WriteLine(ll.Id + " #" + p[i].Id + " " + p[i].Kind + " x=" + D(p[i].X).ToString("F1") + " w=" + D(p[i].Width).ToString("F1") + " a=" + D(p[i].Amplitude).ToString("F1") + " vs #" + p[j].Id + " " + p[j].Kind + " x=" + D(p[j].X).ToString("F1") + " w=" + D(p[j].Width).ToString("F1") + " a=" + D(p[j].Amplitude).ToString("F1") + " dy=" + dy.ToString("F2") + " gap=" + gap.ToString("F2")); }
                    }
                }
                total += n;
                output.WriteLine(ll.Id + " plats=" + p.Length + " moving=" + moving + " swingOverlaps=" + n + " offscreen=" + offscreen);
            }
            output.WriteLine("TOTAL " + total);
            Assert.Equal(0, total);
        }
        [Fact]
        public void PickupsNeverSitInsideAPlatform()
        {
            int bad = CountPickupsInPlatforms();
            Assert.Equal(0, bad);
        }

        int CountPickupsInPlatforms()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return 0;
            SimConfig cfg = new SimConfig();
            int total = 0;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 3, 3))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                int n = 0; string first = "";
                foreach (Pickup k in d.Pickups)
                    foreach (Platform p in d.Platforms)
                    {
                        double dy = D(k.Y) - D(p.Y);
                        if (dy < -0.45 || dy > 0.25) continue;
                        if (D(k.X) < L(p) || D(k.X) > R(p)) continue;
                        n++; if (first == "") first = k.Kind + "#" + k.Id + "@" + D(k.X).ToString("F1") + "," + D(k.Y).ToString("F2") + " in plat#" + p.Id + " " + p.Kind + " x=" + D(p.X).ToString("F1") + " w=" + D(p.Width).ToString("F1") + " y=" + D(p.Y).ToString("F2");
                    }
                int stars = 0, gems = 0, coins = 0;
                foreach (Pickup k in d.Pickups) { if (k.Kind == PickupKind.Star) stars++; else if (k.Kind == PickupKind.Gem || k.Kind == PickupKind.HiddenGem) gems++; else coins++; }
                output.WriteLine(ll.Id + " goal=" + ll.Spec.Goal.Type + " coins=" + coins + " stars=" + stars + " gems=" + gems + " inPlatform=" + n + " " + first);
                total += n;
            }
            return total;
        }
    }
}
