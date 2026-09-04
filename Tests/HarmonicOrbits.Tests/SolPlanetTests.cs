using System;
using Xunit;
using Xunit.Abstractions;

namespace HarmonicOrbits.Verification
{
    /// <summary>Each planet's model at UT 0 against Sol's own Orbit block.</summary>
    // Sol samples its elements at JD 2433647.5, which is UT 0, so its Orbit node is
    // this model evaluated there. Independent of the golden vectors: those check the
    // C# against the Python, this checks the model against the game it ships into,
    // and so catches a wrong reference plane, epoch or time base.
    //
    // Thresholds are the ones SolConfigTests already applies to the Moon, not values
    // tuned per planet. Every planet clears them by more than an order of magnitude.
    public class SolPlanetTests(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _out = output;

        // body, eccentricity, inclination, LAN, argPe, M0 -- all from
        // Sol-Configs/Configs/<n>_<Body>/<Body>-Kopernicus.cfg.
        [Theory]
        [InlineData("mercury", 0.2056264110797241, 28.552245205587443,
            11.00389273075962, 67.47422409882562, 12.23374264650345)]
        [InlineData("venus", 0.006783584260519895, 24.43313318920893,
            8.015191876420234, 123.8986906325162, 176.6227138514177)]
        [InlineData("earth", 0.016725458516403675, 23.439148013195584,
            0.003681190405224785, 101.5983652436283, 358.8791197957159)]
        [InlineData("mars", 0.09337566897226163, 24.677070649563948,
            3.387810351210022, 332.7623776351709, 0.7141639023932382)]
        public void ElementsAtUt0MatchSol(string body, double solEccentricity,
            double solInclination, double solLan, double solArgPe, double solMeanAnomaly)
        {
            ClassicalElements c = Fixture.ReadPack(body)[0].EvaluateAtUniversalTime(0.0);

            // argPe and M are degenerate individually; only the sum is meaningful.
            double mine = Norm(c.LongitudeOfAscendingNode + c.ArgumentOfPeriapsis + c.MeanAnomaly);
            double sol = Norm(solLan + solArgPe + solMeanAnomaly);
            double dLon = Delta(mine, sol);
            double alongTrackKm = Math.Abs(dLon) * Math.PI / 180.0 * c.SemiMajorAxis;

            double dInc = Delta(c.Inclination, solInclination);
            double dLan = Delta(Norm(c.LongitudeOfAscendingNode), Norm(solLan));
            double dEcc = c.Eccentricity - solEccentricity;

            // Plain F-formats: a two-section "+0;-0" renders negative zero as "-+0.0000".
            _out.WriteLine("{0}: mean longitude {1:F6} deg = {2:F1} km along track, "
                + "inc {3:F4}, LAN {4:F4}, e {5:F6}",
                body, dLon, alongTrackKm, dInc, dLan, dEcc);

            Assert.True(Math.Abs(dLon) < 0.005, $"{body} mean longitude differs by {dLon:F6} deg");
            Assert.True(Math.Abs(dInc) < 0.01, $"{body} inclination differs by {dInc:F6} deg");
            Assert.True(Math.Abs(dLan) < 0.01, $"{body} LAN differs by {dLan:F6} deg");
            Assert.InRange(dEcc, -0.002, 0.002);
        }

        // Sol averages a over 1951-2051 while the model is osculating at UT 0, so these
        // disagree by thousands of km by construction. Bounded only to catch a unit slip.
        [Theory]
        [InlineData("mercury", 57909081859.97428)]
        [InlineData("venus", 108208607046.27557)]
        [InlineData("earth", 149598086472.33154)]
        [InlineData("mars", 227939084093.7713)]
        public void SemiMajorAxisIsWithinTheOsculatingSpread(string body, double solSma)
        {
            ClassicalElements c = Fixture.ReadPack(body)[0].EvaluateAtUniversalTime(0.0);
            double diffKm = c.SemiMajorAxis - solSma / 1000.0;

            _out.WriteLine("{0}: sma Sol {1:F0} km, model {2:F0} km, diff {3:F1} km",
                body, solSma / 1000.0, c.SemiMajorAxis, diffKm);

            Assert.InRange(Math.Abs(diffKm) / (solSma / 1000.0), 0.0, 0.001);
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
