using System;
using Xunit;
using Xunit.Abstractions;

namespace HarmonicOrbits.Verification
{
    /// <summary>Model at UT 0 against Sol's own Moon config.</summary>
    // Sol samples at JD 2433647.5, which is UT 0, so its Orbit node is this model evaluated
    // there and the two must agree. Sol-Configs/Configs/03_Earth-System/03-01_Luna.
    public class SolConfigTests(ITestOutputHelper output)
    {
        private const double SolSma = 383397753.9407505;        // metres
        private const double SolEccentricity = 0.05553563342334286;
        private const double SolInclination = 28.46603091672149;
        private const double SolLan = 358.62963698717;
        private const double SolArgPe = 278.297186632491;
        private const double SolMeanAnomaly = 277.0475751508865;

        private readonly ITestOutputHelper _out = output;

        [Fact]
        public void MeanLongitudeMatchesSol()
        {
            // argPe and M are degenerate individually; only their sum with LAN is meaningful.
            ClassicalElements c = Elements();
            double mine = Norm(c.LongitudeOfAscendingNode + c.ArgumentOfPeriapsis + c.MeanAnomaly);
            double sol = Norm(SolLan + SolArgPe + SolMeanAnomaly);
            double diff = Delta(mine, sol);

            _out.WriteLine("mean longitude: Sol {0:F6}, model {1:F6}, diff {2:+0.000000;-0.000000}",
                sol, mine, diff);
            _out.WriteLine("along-track at {0:F0} km: {1:F2} km",
                c.SemiMajorAxis, Math.Abs(diff) * Math.PI / 180.0 * c.SemiMajorAxis);

            Assert.True(Math.Abs(diff) < 0.005,
                string.Format("mean longitude differs by {0:F6} deg", diff));
        }

        [Fact]
        public void InclinationAndNodeMatchSol()
        {
            // Both instantaneous at the epoch in Sol's config, so both should agree.
            ClassicalElements c = Elements();
            double dInc = Delta(c.Inclination, SolInclination);
            double dLan = Delta(Norm(c.LongitudeOfAscendingNode), Norm(SolLan));

            _out.WriteLine("inclination diff {0:+0.000000;-0.000000}, LAN diff {1:+0.000000;-0.000000}",
                dInc, dLan);

            Assert.True(Math.Abs(dInc) < 0.01, "inclination differs by " + dInc);
            Assert.True(Math.Abs(dLan) < 0.01, "LAN differs by " + dLan);
        }

        [Fact]
        public void PeriapsisAndAnomalyErrorsCancel()
        {
            // Sol splits the same longitude differently. Correcting either alone would move
            // the body; this asserts the split stays degenerate rather than drifting.
            ClassicalElements c = Elements();
            double dArgPe = Delta(Norm(c.ArgumentOfPeriapsis), Norm(SolArgPe));
            double dMeanAnomaly = Delta(Norm(c.MeanAnomaly), Norm(SolMeanAnomaly));

            _out.WriteLine("argPe diff {0:+0.000000;-0.000000}, M diff {1:+0.000000;-0.000000}, "
                + "sum {2:+0.000000;-0.000000}", dArgPe, dMeanAnomaly, dArgPe + dMeanAnomaly);

            Assert.True(Math.Abs(dArgPe) > 0.01, "expected the split to differ from Sol's");
            Assert.True(Math.Abs(dArgPe + dMeanAnomaly) < 0.005, "the two no longer cancel");
        }

        [Fact]
        public void SemiMajorAxisIsBelowSolsCenturyAverage()
        {
            // Sol averages a and inclination over 1951-2051 while the angles are
            // instantaneous, so an osculating a is expected to differ by thousands of km.
            ClassicalElements c = Elements();
            double diffKm = c.SemiMajorAxis - SolSma / 1000.0;

            _out.WriteLine("sma: Sol {0:F3} km, model {1:F3} km, diff {2:F1} km",
                SolSma / 1000.0, c.SemiMajorAxis, diffKm);

            Assert.InRange(diffKm, -5000.0, -2000.0);
        }

        [Fact]
        public void EccentricityIsCloseToSols()
        {
            ClassicalElements c = Elements();
            Assert.InRange(c.Eccentricity - SolEccentricity, -0.002, 0.002);
        }

        [Fact]
        public void UnitConversionMatchesSetOrbitsExpectations()
        {
            KspElements e = KspElements.From(Elements());

            Assert.InRange(e.SemiMajorAxis, 3.6e8, 4.1e8);           // metres, not km
            Assert.InRange(e.MeanAnomalyAtEpoch, 0.0, Math.PI * 2.0); // radians, wrapped
            Assert.InRange(e.Inclination, 0.0, 90.0);                 // degrees, not radians
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(-1.0, Math.PI * 2.0 - 1.0)]
        [InlineData(Math.PI * 2.0 + 0.5, 0.5)]
        [InlineData(-Math.PI * 4.0 - 0.25, Math.PI * 2.0 - 0.25)]
        public void WrapReducesToOneTurn(double input, double expected)
        {
            Assert.Equal(expected, KspElements.Wrap(input), 12);
        }

        [Fact]
        public void LambdaIsLargeEnoughToNeedWrapping()
        {
            // 486,499 degrees at the end of the window; unwrapped it would cost precision in
            // Orbit.Init's ObT.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            EquinoctialElements end = m.Evaluate(ModelEpoch.ToModelTime(
                ModelEpoch.WindowEndUt, m.EpochJd));
            _out.WriteLine("lambda at the end of the window: {0:F0} deg", end.Lambda);
            Assert.True(Math.Abs(end.Lambda) > 100000.0);
        }

        private static ClassicalElements Elements()
        {
            return Fixture.ReadPack(Fixture.Body)[0].EvaluateAtUniversalTime(0.0);
        }

        private static double Norm(double deg)
        {
            double d = deg % 360.0;
            return d < 0.0 ? d + 360.0 : d;
        }

        private static double Delta(double a, double b)
        {
            return (a - b + 540.0) % 360.0 - 180.0;
        }
    }
}
