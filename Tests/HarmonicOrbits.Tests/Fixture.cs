using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HarmonicOrbits.Verification
{
    /// <summary>Loads golden vectors and the shipped pack for one body.</summary>
    public static class Fixture
    {
        public const string Body = "moon";

        /// <summary>Repository root.</summary>
        public static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HarmonicOrbits.sln")))
                {
                    dir = dir.Parent;
                }
                if (dir == null)
                {
                    throw new InvalidOperationException(
                        "could not find HarmonicOrbits.sln above " + AppContext.BaseDirectory);
                }
                return dir.FullName;
            }
        }

        public static string PackPath(string body)
        {
            return Path.Combine(RepoRoot, "Resources", "Bodies", body + ".bin");
        }

        public static string GoldenPath(string body)
        {
            return Path.Combine(RepoRoot, "Tests", "HarmonicOrbits.Tests", "Fixtures",
                body + ".golden.json");
        }

        public static List<BodyModel> ReadPack(string body)
        {
            using FileStream fs = File.OpenRead(PackPath(body));
            return ModelReader.Read(fs);
        }

        public static Golden ReadGolden(string body)
        {
            using FileStream fs = File.OpenRead(GoldenPath(body));
            return JsonSerializer.Deserialize<Golden>(fs, Options);
        }

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }

    public sealed class Golden
    {
        public string Body { get; set; }
        public double EpochJD { get; set; }
        public double SpanDays { get; set; }
        public double Gm { get; set; }
        public int NPeaks { get; set; }
        public double Ut0JD { get; set; }
        public Dictionary<string, GoldenSeries> Coefficients { get; set; }
        public List<GoldenSample> Samples { get; set; }
    }

    public sealed class GoldenSeries
    {
        public int Circulating { get; set; }
        public double[] Secular { get; set; }
        public double Constant { get; set; }
        public double[] Omegas { get; set; }
        public double[] Cos { get; set; }
        public double[] Sin { get; set; }
    }

    public sealed class GoldenSample
    {
        public double T { get; set; }
        public double Ut { get; set; }
        public Dictionary<string, double> Equinoctial { get; set; }
        public Dictionary<string, double> Classical { get; set; }
    }
}
