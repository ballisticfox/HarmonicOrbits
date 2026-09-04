using Xunit;
using Xunit.Abstractions;

namespace HarmonicOrbits.Verification
{
    /// <summary>Outside-window policy.</summary>
    public class ValidityWindowTests(ITestOutputHelper output)
    {
        private const double Year = 365.25 * 86400.0;

        private readonly ITestOutputHelper _out = output;

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0e9)]
        [InlineData(ValidityWindow.EndUt)]
        public void InsideTheWindowBothPoliciesAgree(double ut)
        {
            Assert.False(ValidityWindow.IsOutside(ut));
            Assert.Equal(ut, ValidityWindow.EvaluationTime(ut, OutsideWindowAction.Continue));
            Assert.Equal(ut, ValidityWindow.EvaluationTime(ut, OutsideWindowAction.Clamp));
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData(ValidityWindow.EndUt + 1.0)]
        public void OutsideTheWindowIsDetected(double ut)
        {
            Assert.True(ValidityWindow.IsOutside(ut));
        }

        [Fact]
        public void ContinuePassesTheTimeThroughUnchanged()
        {
            double ut = ValidityWindow.EndUt + 50.0 * Year;
            Assert.Equal(ut, ValidityWindow.EvaluationTime(ut, OutsideWindowAction.Continue));
        }

        [Fact]
        public void ClampPinsToTheWindowEdges()
        {
            Assert.Equal(ValidityWindow.EndUt, ValidityWindow.EvaluationTime(
                ValidityWindow.EndUt + 50.0 * Year, OutsideWindowAction.Clamp));
            Assert.Equal(ValidityWindow.StartUt, ValidityWindow.EvaluationTime(
                -Year, OutsideWindowAction.Clamp));
        }

        [Fact]
        public void ClampedElementsAreTheWindowEdgeElements()
        {
            // The point of clamp: past the edge the elements stop moving, so KSP is left
            // propagating a fixed conic exactly as it does for a stock body.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double past = ValidityWindow.EndUt + 20.0 * Year;

            ClassicalElements edge = m.EvaluateAtUniversalTime(
                ValidityWindow.EvaluationTime(ValidityWindow.EndUt, OutsideWindowAction.Clamp));
            ClassicalElements clamped = m.EvaluateAtUniversalTime(
                ValidityWindow.EvaluationTime(past, OutsideWindowAction.Clamp));

            Assert.Equal(edge.SemiMajorAxis, clamped.SemiMajorAxis);
            Assert.Equal(edge.Inclination, clamped.Inclination);
            Assert.Equal(edge.MeanAnomaly, clamped.MeanAnomaly);
        }

        [Fact]
        public void ContinueStaysPhysicalWellPastTheWindow()
        {
            // Continue is the default because the fit holds past 2100. Guard against the
            // polynomials running away: a lunar semi-major axis of the right order, and an
            // eccentricity still bound to an ellipse.
            BodyModel m = Fixture.ReadPack(Fixture.Body)[0];
            double ut2100 = ValidityWindow.EndUt + 49.0 * Year;

            ClassicalElements c = m.EvaluateAtUniversalTime(ut2100);
            _out.WriteLine("at 2100: a={0:F1} km e={1:F6} inc={2:F4}",
                c.SemiMajorAxis, c.Eccentricity, c.Inclination);

            Assert.InRange(c.SemiMajorAxis, 350000.0, 420000.0);
            Assert.InRange(c.Eccentricity, 0.0, 0.3);
            Assert.InRange(c.Inclination, 0.0, 90.0);
        }
    }
}
