using System;
using System.Collections.Generic;
using BoingUpSkyTower.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>Owner issue 15 (Sunny Beach level 7): two platforms sat together and read as one broken image. Platforms near the same height keep a visible gap.</summary>
    public class PlatformSeparationTests
    {
        readonly ITestOutputHelper output;
        public PlatformSeparationTests(ITestOutputHelper output) { this.output = output; }

        const double MinGapX = 0.05;     // visible air between the end caps of two platforms at about the same height
        const double SameHeight = 0.9;   // world units

        static double Left(Platform p) { return (p.X.Raw - p.Width.Raw / 2) / 65536.0; }
        static double Right(Platform p) { return (p.X.Raw + p.Width.Raw / 2) / 65536.0; }

        [Fact]
        public void PlatformsAtTheSameHeightNeverTouch()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) { output.WriteLine("Content not found; skipped"); return; }
            SimConfig cfg = new SimConfig();
            var offenders = new List<string>();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Platform[] p = d.Platforms;
                for (int i = 0; i < p.Length; i++)
                {
                    if (p[i].Width.Raw >= 9 * 65536) continue;   // the floor
                    for (int j = i + 1; j < p.Length; j++)
                    {
                        if (p[j].Width.Raw >= 9 * 65536) continue;
                        double dy = Math.Abs((p[i].Y.Raw - p[j].Y.Raw) / 65536.0);
                        if (dy > SameHeight) continue;
                        double gap = Math.Max(Left(p[j]) - Right(p[i]), Left(p[i]) - Right(p[j]));
                        if (gap < MinGapX) offenders.Add(ll.Id + " #" + p[i].Id + " (" + p[i].Kind + " x=" + (p[i].X.Raw / 65536.0).ToString("F1") + " w=" + (p[i].Width.Raw / 65536.0).ToString("F1") + ") vs #" + p[j].Id + " (" + p[j].Kind + " x=" + (p[j].X.Raw / 65536.0).ToString("F1") + " w=" + (p[j].Width.Raw / 65536.0).ToString("F1") + ") dy=" + dy.ToString("F2") + " gap=" + gap.ToString("F2"));
                    }
                }
            }
            foreach (string s in offenders) output.WriteLine(s);
            Assert.True(offenders.Count == 0, offenders.Count + " close platform pairs, first: " + (offenders.Count > 0 ? offenders[0] : ""));
        }
    }
}
