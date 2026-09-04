using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace HarmonicOrbits.Verification
{
    /// <summary>C# evaluator vs. Python reference vectors.</summary>
    public class GoldenVectorTests(ITestOutputHelper output)
    {
        // Absorbs summation-order differences between numpy and a plain loop.
        private const double Tolerance = 1e-9;

        private readonly ITestOutputHelper _out = output;

        [Fact]
        public void EquinoctialSeriesMatchThePythonReference()
        {
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            Golden g = Fixture.ReadGolden(Fixture.Body);

            double worst = 0.0;
            string worstWhere = "none";

            foreach (GoldenSample s in g.Samples)
            {
                EquinoctialElements e = m.Evaluate(s.T);
                worst = Track(worst, ref worstWhere, "a", s.T, s.Equinoctial["a"], e.A);
                worst = Track(worst, ref worstWhere, "h", s.T, s.Equinoctial["h"], e.H);
                worst = Track(worst, ref worstWhere, "k", s.T, s.Equinoctial["k"], e.K);
                worst = Track(worst, ref worstWhere, "p", s.T, s.Equinoctial["p"], e.P);
                worst = Track(worst, ref worstWhere, "q", s.T, s.Equinoctial["q"], e.Q);
                worst = Track(worst, ref worstWhere, "lam", s.T, s.Equinoctial["lam"], e.Lambda);
            }

            _out.WriteLine("{0} samples, worst relative error {1:E3} at {2}",
                g.Samples.Count, worst, worstWhere);
            Assert.True(worst < Tolerance, string.Format(
                "worst relative error {0:E3} at {1}, tolerance {2:E0}",
                worst, worstWhere, Tolerance));
        }

        [Fact]
        public void ClassicalElementsMatchThePythonReference()
        {
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            Golden g = Fixture.ReadGolden(Fixture.Body);

            double worst = 0.0;
            string worstWhere = "none";

            foreach (GoldenSample s in g.Samples)
            {
                ClassicalElements c = m.EvaluateClassical(s.T);
                worst = Track(worst, ref worstWhere, "a", s.T,
                    s.Classical["a"], c.SemiMajorAxis);
                worst = Track(worst, ref worstWhere, "e", s.T,
                    s.Classical["e"], c.Eccentricity);
                worst = Track(worst, ref worstWhere, "inc", s.T,
                    s.Classical["inc"], c.Inclination);
                worst = Track(worst, ref worstWhere, "lan", s.T,
                    s.Classical["lan"], c.LongitudeOfAscendingNode);
                worst = Track(worst, ref worstWhere, "argPe", s.T,
                    s.Classical["argPe"], c.ArgumentOfPeriapsis);
                worst = Track(worst, ref worstWhere, "meanAnomaly", s.T,
                    s.Classical["meanAnomaly"], c.MeanAnomaly);
            }

            _out.WriteLine("{0} samples, worst relative error {1:E3} at {2}",
                g.Samples.Count, worst, worstWhere);
            Assert.True(worst < Tolerance, string.Format(
                "worst relative error {0:E3} at {1}, tolerance {2:E0}",
                worst, worstWhere, Tolerance));
        }

        [Fact]
        public void UniversalTimeEntryPointAgreesWithModelTime()
        {
            // UT entry point must agree with model-time path.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            Golden g = Fixture.ReadGolden(Fixture.Body);

            double worst = 0.0;
            string where = "none";
            foreach (GoldenSample s in g.Samples)
            {
                ClassicalElements c = m.EvaluateAtUniversalTime(s.Ut);
                worst = Track(worst, ref where, "a(ut)", s.Ut,
                    s.Classical["a"], c.SemiMajorAxis);
                worst = Track(worst, ref where, "lam(ut)", s.Ut,
                    s.Classical["meanAnomaly"], c.MeanAnomaly);
            }
            _out.WriteLine("worst relative error via UT: {0:E3} at {1}", worst, where);
            Assert.True(worst < Tolerance, "UT entry point diverged: " + where);
        }

        [Fact]
        public void GoldenSamplesCoverTheShippedWindow()
        {
            // Secular-term errors grow toward the edges of the window.
            Golden g = Fixture.ReadGolden(Fixture.Body);
            Assert.True(g.Samples.Count >= 200, "expected at least 200 samples");

            double firstUt = g.Samples[0].Ut;
            double lastUt = g.Samples[g.Samples.Count - 1].Ut;
            Assert.Equal(0.0, firstUt, 6);
            Assert.Equal(ModelEpoch.WindowEndUt, lastUt, 6);
        }

        private static double Track(double worst, ref string where, string name, double t,
            double expected, double actual)
        {
            double scale = Math.Max(Math.Abs(expected), 1e-12);
            double rel = Math.Abs(actual - expected) / scale;
            if (rel > worst)
            {
                where = string.Format("{0} at t={1:F3} (expected {2:R}, got {3:R})",
                    name, t, expected, actual);
                return rel;
            }
            return worst;
        }
    }
}
