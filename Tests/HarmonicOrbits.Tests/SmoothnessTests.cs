using System;
using Xunit;
using Xunit.Abstractions;

namespace HarmonicOrbits.Verification
{
    /// <summary>Per-frame element motion, at frame and warp step sizes.</summary>
    // Discontinuity between ticks is the failure mode; cheaper to catch here than in-game.
    public class SmoothnessTests(ITestOutputHelper output)
    {
        private const double Tick = 1.0 / 50.0;                 // one physics tick
        private const double MaxWarpStep = 100000.0 * Tick;     // 2000 s at 100,000x

        private readonly ITestOutputHelper _out = output;

        [Fact]
        public void ElementsMoveSmoothlyBetweenPhysicsTicks()
        {
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double ut = 1.0e9;

            double worstJerk = 0.0;
            double prevStep = double.NaN;
            for (int i = 0; i < 2000; i++)
            {
                double a0 = m.EvaluateAtUniversalTime(ut + i * Tick).SemiMajorAxis;
                double a1 = m.EvaluateAtUniversalTime(ut + (i + 1) * Tick).SemiMajorAxis;
                double step = a1 - a0;
                if (!double.IsNaN(prevStep))
                {
                    worstJerk = Math.Max(worstJerk, Math.Abs(step - prevStep));
                }
                prevStep = step;
            }

            _out.WriteLine("semi-major axis: worst tick-to-tick change in step = {0:E3} km",
                worstJerk);

            // A jump would show as a step change orders of magnitude above this.
            Assert.True(worstJerk < 1.0e-6, "semi-major axis jerks by " + worstJerk + " km");
        }

        [Fact]
        public void MeanLongitudeRateGivesTheSiderealMonth()
        {
            // Over the whole window the periodic terms average out and the secular rate is
            // left, which is a physical constant the fit never saw.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];

            double lam0 = m.Evaluate(ModelEpoch.ToModelTime(0.0, m.EpochJd)).Lambda;
            double lam1 = m.Evaluate(ModelEpoch.ToModelTime(
                ModelEpoch.WindowEndUt, m.EpochJd)).Lambda;
            double perDay = (lam1 - lam0) / (ModelEpoch.WindowEndUt / 86400.0);
            double month = 360.0 / perDay;

            _out.WriteLine("secular rate {0:F6} deg/day, sidereal month {1:F5} d "
                + "(true 27.32166)", perDay, month);

            Assert.InRange(month, 27.3210, 27.3225);
        }

        [Fact]
        public void InstantaneousRateStaysBounded()
        {
            // The one-day rate swings roughly 12.7 to 13.6 deg/day because evection and the
            // other periodic terms ride on top of the secular one. Bounded, not constant.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double lo = double.MaxValue;
            double hi = double.MinValue;

            for (int i = 0; i < 2000; i++)
            {
                double t = ModelEpoch.ToModelTime(i * (ModelEpoch.WindowEndUt / 2000.0),
                    m.EpochJd);
                double rate = m.Evaluate(t + 1.0).Lambda - m.Evaluate(t).Lambda;
                lo = Math.Min(lo, rate);
                hi = Math.Max(hi, rate);
            }

            _out.WriteLine("one-day rate spans {0:F6} to {1:F6} deg/day", lo, hi);
            Assert.InRange(lo, 12.0, 13.2);
            Assert.InRange(hi, 13.2, 14.5);
        }

        [Fact]
        public void ElementsMoveModestlyAcrossAMaximumWarpStep()
        {
            // Bounds how far one rewrite can move a body at the highest warp. Evaluation is
            // a pure function of UT, so jump-vs-walk is identical; path-dependent drift needs
            // the game.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double worst = 0.0;

            for (int i = 0; i < 500; i++)
            {
                double ut = i * (ModelEpoch.WindowEndUt / 500.0);
                double before = m.EvaluateAtUniversalTime(ut).SemiMajorAxis;
                double after = m.EvaluateAtUniversalTime(ut + MaxWarpStep).SemiMajorAxis;
                worst = Math.Max(worst, Math.Abs(after - before));
            }

            _out.WriteLine("semi-major axis moves at most {0:F3} km across a {1:F0} s "
                + "warp step", worst, MaxWarpStep);

            Assert.True(worst < 100.0, "semi-major axis moved " + worst + " km in one step");
        }

        [Fact]
        public void WrappedMeanAnomalyOnlyJumpsAtTheWrap()
        {
            // SetOrbit takes a wrapped anomaly, so exactly one 2pi step per revolution is
            // expected; anything else is a modelling discontinuity. Span is long enough that
            // +/-1 tolerance can't accidentally accept zero wraps.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            const double ut = 1.0e9;
            const double step = 3600.0;
            const int steps = 10000;
            int jumps = 0;

            double prev = KspElements.From(m.EvaluateAtUniversalTime(ut)).MeanAnomalyAtEpoch;
            for (int i = 1; i < steps; i++)
            {
                double now = KspElements.From(
                    m.EvaluateAtUniversalTime(ut + i * step)).MeanAnomalyAtEpoch;
                if (Math.Abs(now - prev) > Math.PI)
                {
                    jumps++;
                }
                prev = now;
            }

            double revolutions = steps * step / (27.321661 * 86400.0);
            _out.WriteLine("{0} wraps over {1:F2} revolutions", jumps, revolutions);
            Assert.InRange(jumps, (int)revolutions, (int)revolutions + 1);
        }
    }
}
