using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace HarmonicOrbits.Verification
{
    /// <summary>Model/ boundary and epoch arithmetic.</summary>
    public partial class ModelLayerTests
    {
        [Fact]
        public void ModelSourcesImportNothingFromKspOrUnity()
        {
            string dir = Path.Combine(Fixture.RepoRoot, "HarmonicOrbits", "Model");
            string[] banned = ["UnityEngine", "HarmonyLib", "KSP.", "MonoBehaviour"];

            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    string trimmed = line.TrimStart();
                    if (!trimmed.StartsWith("using ", StringComparison.Ordinal)) continue;
                    foreach (string bad in banned)
                    {
                        Assert.False(trimmed.Contains(bad), string.Format(
                            "{0} imports {1}: Model/ must stay free of game types",
                            Path.GetFileName(file), bad));
                    }
                }
            }
        }

        [Fact]
        public void ModelSourcesUseNoSinglePrecision()
        {
            string dir = Path.Combine(Fixture.RepoRoot, "HarmonicOrbits", "Model");
            Regex singles = MyRegex();

            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    Assert.False(singles.IsMatch(code), string.Format(
                        "{0}:{1} uses single precision: {2}",
                        Path.GetFileName(file), i + 1, lines[i].Trim()));
                }
            }
        }

        [Fact]
        public void Ut0IsTheEpochSolSamplesAt()
        {
            // 1951-01-01 00:00 GMT, from Sol-KopernicusSettings.cfg.
            Assert.Equal(2433647.5, ModelEpoch.Ut0Jd);
        }

        [Fact]
        public void ModelTimeOffsetIsPerBody()
        {
            // Eight bodies at JD 2433282.5, Europa at 2415020.5.
            Assert.Equal(365.0, ModelEpoch.ToModelTime(0.0, 2433282.5), 9);
            Assert.Equal(18627.0, ModelEpoch.ToModelTime(0.0, 2415020.5), 9);
        }

        [Fact]
        public void EpochConversionRoundTrips()
        {
            double ut = 1.234567e9;
            double t = ModelEpoch.ToModelTime(ut, 2433282.5);
            Assert.Equal(ut, ModelEpoch.ToUniversalTime(t, 2433282.5), 6);
        }

        [Fact]
        public void ShippedWindowIsExactlyOneCentury()
        {
            Assert.Equal(36525.0 * 86400.0, ModelEpoch.WindowEndUt);
            Assert.Equal(3155760000.0, ModelEpoch.WindowEndUt);
        }

        [Fact]
        public void SeriesRejectsMismatchedTermCounts()
        {
            Assert.Throws<ArgumentException>(() => new ElementSeries(
                false, [1.0], 0.0, [1.0, 2.0], [1.0], [1.0]));
        }

        [Fact]
        public void ModelRejectsWrongElementCount()
        {
            var s = new ElementSeries(false, [1.0], 0.0,
                [], [], []);
            Assert.Throws<ArgumentException>(
                () => new BodyModel("x", 0.0, 0.0, 1.0, [s, s, s]));
        }

        [Fact]
        public void RegistryMatchesNamesCaseInsensitively()
        {
            // Pack uses lower-case; KSP uses "Moon".
            var registry = new ModelRegistry();
            registry.AddRange(Fixture.ReadPack(Fixture.Body));

            Assert.True(registry.TryGet("Moon", out BodyModel model));
            Assert.True(registry.TryGet("moon", out model));
            Assert.False(registry.TryGet("Mun", out model));
            Assert.False(registry.TryGet(null, out model));
        }

        private static string StripComment(string line)
        {
            int i = line.IndexOf("//", StringComparison.Ordinal);
            return i < 0 ? line : line.Substring(0, i);
        }

        [GeneratedRegex(@"\b(float|Single)\b")]
        private static partial Regex MyRegex();

    }
}
