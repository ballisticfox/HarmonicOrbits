using System;
using Xunit;
using Xunit.Abstractions;

namespace HarmonicOrbits.Verification
{
    /// <summary>The spin rate handed to a tidally locked body.</summary>
    public class RotationPeriodTests(ITestOutputHelper output)
    {
        private const double EarthGm = 398600.4354;             // Kopernicus, km^3/s^2
        private const double SolRotationPeriod = 2360584.68479999;
        private const double TrueSiderealMonth = 27.321661 * 86400.0;

        private readonly ITestOutputHelper _out = output;

        [Fact]
        public void MeanPeriodMatchesTheSiderealMonth()
        {
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double p = m.MeanOrbitalPeriod();

            _out.WriteLine("model {0:F3} s, Sol config {1:F3} s, true {2:F3} s",
                p, SolRotationPeriod, TrueSiderealMonth);
            _out.WriteLine("model vs true: {0:+0.0;-0.0} s ({1:E2} relative)",
                p - TrueSiderealMonth, Math.Abs(p - TrueSiderealMonth) / TrueSiderealMonth);

            Assert.InRange(Math.Abs(p - TrueSiderealMonth) / TrueSiderealMonth, 0.0, 1e-5);
        }

        [Fact]
        public void MeanPeriodIsStableWhereTheOsculatingPeriodIsNot()
        {
            // The bug: CBUpdate takes rotationPeriod from orbit.period, which KSP derives
            // from the osculating a. Across the window that swings by hours.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double lo = double.MaxValue;
            double hi = double.MinValue;

            for (int i = 0; i <= 2000; i++)
            {
                double ut = i * (ModelEpoch.WindowEndUt / 2000.0);
                double a = m.EvaluateAtUniversalTime(ut).SemiMajorAxis;
                double keplerian = 2.0 * Math.PI * Math.Sqrt(a * a * a / EarthGm);
                lo = Math.Min(lo, keplerian);
                hi = Math.Max(hi, keplerian);
            }

            _out.WriteLine("osculating period spans {0:F0} to {1:F0} s, a swing of {2:F0} s "
                + "({3:F1} hours)", lo, hi, hi - lo, (hi - lo) / 3600.0);
            _out.WriteLine("mean period is a single value: {0:F3} s", m.MeanOrbitalPeriod());

            Assert.True(hi - lo > 3600.0, "expected the osculating period to swing");
            Assert.InRange(m.MeanOrbitalPeriod(), lo, hi);
        }

        [Fact]
        public void FixedPeriodKeepsSpinRateSaneAcrossTheCentury()
        {
            // rotationAngle = initialRotation + 360 * UT / period, so a period error is
            // multiplied by the elapsed revolution count. With a fixed period the per-tick
            // step is constant; with the osculating one it is not.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double period = m.MeanOrbitalPeriod();
            const double tick = 1.0 / 50.0;

            double expected = 360.0 * tick / period;
            double worstRatio = 0.0;

            for (int i = 1; i <= 500; i++)
            {
                double ut = i * (ModelEpoch.WindowEndUt / 500.0);
                double a0 = m.EvaluateAtUniversalTime(ut).SemiMajorAxis;
                double a1 = m.EvaluateAtUniversalTime(ut + tick).SemiMajorAxis;
                double p0 = 2.0 * Math.PI * Math.Sqrt(a0 * a0 * a0 / EarthGm);
                double p1 = 2.0 * Math.PI * Math.Sqrt(a1 * a1 * a1 / EarthGm);
                double osculating = Math.Abs(360.0 * (ut + tick) / p1 - 360.0 * ut / p0);
                worstRatio = Math.Max(worstRatio, osculating / expected);
            }

            _out.WriteLine("fixed-period step {0:E3} deg/tick", expected);
            _out.WriteLine("osculating-period step reaches {0:F0}x that", worstRatio);

            Assert.True(worstRatio > 10.0, "expected the osculating path to be badly wrong");
        }

        [Fact]
        public void EveryShippedModelGivesAPlausiblePeriod()
        {
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double days = m.MeanOrbitalPeriod() / 86400.0;
            Assert.InRange(days, 0.1, 1.0e5);
            Assert.True(m.MeanOrbitalPeriod() > 0.0, "period must be positive");
        }
    }
}
