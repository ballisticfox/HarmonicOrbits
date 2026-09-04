using System.Collections.Generic;
using System.IO;
using Xunit;

namespace HarmonicOrbits.Verification
{
    /// <summary>Pack decoding: header, coefficients, and error cases.</summary>
    public class ReaderTests
    {
        private static readonly string[] Names = ["a", "h", "k", "p", "q", "lam"];

        [Fact]
        public void PackHeaderMatchesTheGoldenFile()
        {
            List<BodyModel> bodies = Fixture.ReadPack(Fixture.Body);
            Golden g = Fixture.ReadGolden(Fixture.Body);

            Assert.Single(bodies);
            BodyModel m = bodies[0];
            Assert.Equal(g.Body, m.Name);
            Assert.Equal(g.EpochJD, m.EpochJd);
            Assert.Equal(g.SpanDays, m.SpanDays);
            Assert.Equal(g.Gm, m.GravParameter);
        }

        [Fact]
        public void EveryCoefficientRoundTripsBitForBit()
        {
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            Golden g = Fixture.ReadGolden(Fixture.Body);

            for (int i = 0; i < BodyModel.ElementCount; i++)
            {
                string name = Names[i];
                GoldenSeries expected = g.Coefficients[name];
                ElementSeries actual = m.Series(i);

                Assert.Equal(expected.Circulating != 0, actual.Circulating);
                Assert.Equal(expected.Secular.Length - 1, actual.SecularDegree);
                Assert.Equal(expected.Omegas.Length, actual.TermCount);

                // Exact equality: no arithmetic between pack and golden, so any diff is a bug.
                for (int j = 0; j < expected.Omegas.Length; j++)
                {
                    Assert.Equal(expected.Omegas[j], actual.Omega(j));
                }
            }
        }

        [Fact]
        public void PackIsWithinTheSizeBudget()
        {
            // Moon is ~914 doubles, 7.1 KB payload.
            long bytes = new FileInfo(Fixture.PackPath(Fixture.Body)).Length;
            Assert.InRange(bytes, 7000, 9000);
        }

        [Fact]
        public void WrongMagicIsRejected()
        {
            byte[] raw = File.ReadAllBytes(Fixture.PackPath(Fixture.Body));
            raw[0] = (byte)'X';
            using var ms = new MemoryStream(raw);
            InvalidDataException ex = Assert.Throws<InvalidDataException>(
                () => ModelReader.Read(ms));
            Assert.Contains("magic", ex.Message);
        }

        [Fact]
        public void UnknownVersionIsRejected()
        {
            byte[] raw = File.ReadAllBytes(Fixture.PackPath(Fixture.Body));
            raw[4] = 99;
            using var ms = new MemoryStream(raw);
            InvalidDataException ex = Assert.Throws<InvalidDataException>(
                () => ModelReader.Read(ms));
            Assert.Contains("version", ex.Message);
        }

        [Fact]
        public void TruncatedPackIsRejected()
        {
            byte[] raw = File.ReadAllBytes(Fixture.PackPath(Fixture.Body));
            byte[] cut = new byte[raw.Length / 2];
            System.Array.Copy(raw, cut, cut.Length);
            using var ms = new MemoryStream(cut);
            Assert.Throws<EndOfStreamException>(() => ModelReader.Read(ms));
        }
    }
}
