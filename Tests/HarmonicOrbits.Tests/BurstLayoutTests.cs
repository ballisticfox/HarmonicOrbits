using System;
using Xunit;

namespace HarmonicOrbits.Verification
{
    /// <summary>The packed coefficient layout the Burst kernel indexes.</summary>
    // The layout contract (CopyTo writes, Kernel indexes) is testable without Burst.
    // Pack() and Read() mirror BurstEvaluator.Pack and Kernel.
    public class BurstLayoutTests
    {
        private const int MetaPerSeries = 3;

        [Theory]
        [MemberData(nameof(Fixture.AllBodies), MemberType = typeof(Fixture))]
        public void PackedLayoutReproducesEverySeries(string body)
        {
            BodyModel m = Fixture.ReadPack(body)[0];

            Pack(m, out double[] coef, out int[] meta);

            // Sampled across the shipped window, where the secular terms are largest.
            double t0 = ModelEpoch.ToModelTime(ValidityWindow.StartUt, m.EpochJd);
            double t1 = ModelEpoch.ToModelTime(ValidityWindow.EndUt, m.EpochJd);

            for (int step = 0; step <= 8; step++)
            {
                double t = t0 + (t1 - t0) * step / 8.0;
                for (int s = 0; s < BodyModel.ElementCount; s++)
                {
                    double expected = m.Series(s).Evaluate(t);
                    double actual = Read(coef, meta, s, t);
                    double scale = Math.Max(Math.Abs(expected), 1e-12);
                    Assert.True(Math.Abs(actual - expected) / scale < 1e-12, string.Format(
                        "{0} series {1} at t={2:F1}: packed {3:R} vs series {4:R}",
                        body, s, t, actual, expected));
                }
            }
        }

        [Theory]
        [MemberData(nameof(Fixture.AllBodies), MemberType = typeof(Fixture))]
        public void PackConsumesExactlyTheReportedCoefficientCount(string body)
        {
            BodyModel m = Fixture.ReadPack(body)[0];

            int total = 0;
            for (int s = 0; s < BodyModel.ElementCount; s++)
            {
                total += m.Series(s).CoefficientCount;
            }

            // The allocation is sized from CoefficientCount, so an over-report wastes memory
            // and an under-report writes past the buffer.
            Assert.Equal(m.CoefficientCount, total);

            Pack(m, out double[] coef, out int[] meta);
            Assert.Equal(m.CoefficientCount, coef.Length);
        }

        [Fact]
        public void CopyToRejectsATooSmallDestination()
        {
            ElementSeries s = Fixture.ReadPack(Fixture.Body)[0].Series(BodyModel.IndexA);
            var small = new double[s.CoefficientCount - 1];
            Assert.Throws<ArgumentException>(() => s.CopyTo(small, 0));
        }

        // Mirrors BurstEvaluator.Pack for a single body.
        private static void Pack(BodyModel m, out double[] coef, out int[] meta)
        {
            coef = new double[m.CoefficientCount];
            meta = new int[BodyModel.ElementCount * MetaPerSeries];

            int at = 0;
            for (int s = 0; s < BodyModel.ElementCount; s++)
            {
                ElementSeries series = m.Series(s);
                meta[s * MetaPerSeries] = at;
                meta[s * MetaPerSeries + 1] = series.SecularDegree + 1;
                meta[s * MetaPerSeries + 2] = series.TermCount;
                at += series.CopyTo(coef, at);
            }
        }

        // Mirrors BurstEvaluator.Kernel for a single series.
        private static double Read(double[] coef, int[] meta, int s, double t)
        {
            int at = meta[s * MetaPerSeries];
            int secularLength = meta[s * MetaPerSeries + 1];
            int terms = meta[s * MetaPerSeries + 2];

            double v = 0.0;
            for (int i = 0; i < secularLength; i++)
            {
                v = v * t + coef[at + i];
            }
            v += coef[at + secularLength];

            int omega = at + secularLength + 1;
            int cos = omega + terms;
            int sin = cos + terms;
            double sum = 0.0;
            for (int i = 0; i < terms; i++)
            {
                double wt = coef[omega + i] * t;
                sum += coef[cos + i] * Math.Cos(wt) + coef[sin + i] * Math.Sin(wt);
            }
            return v + sum;
        }
    }
}
