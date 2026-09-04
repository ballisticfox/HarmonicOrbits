using System.Text;
using UnityEngine;

namespace HarmonicOrbits
{
    /// <summary>Logs the contents of a loaded pack.</summary>
    public static class ModelDump
    {
        public static void Log(ModelRegistry registry)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[HarmonicOrbits]: " + registry.Count + " model(s) loaded");
            foreach (BodyModel model in registry.Models)
            {
                sb.AppendLine(Describe(model));
            }
            Debug.Log(sb.ToString());
        }

        public static string Describe(BodyModel model)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("  {0}: epochJD {1} span {2} d, GM {3}, {4} coefficients",
                model.Name, model.EpochJd, model.SpanDays, model.GravParameter,
                model.CoefficientCount);
            sb.AppendLine();

            string[] names = { "a", "h", "k", "p", "q", "lam" };
            for (int i = 0; i < BodyModel.ElementCount; i++)
            {
                ElementSeries s = model.Series(i);
                sb.AppendFormat("    {0,-3} circ={1} secular={2} terms={3}",
                    names[i], s.Circulating ? 1 : 0, s.SecularDegree + 1, s.TermCount);
                sb.AppendLine();
            }

            // Angles normalised so they can be compared against a Kopernicus Orbit node.
            ClassicalElements c = model.EvaluateAtUniversalTime(0.0);
            sb.AppendFormat("    at UT 0: a={0:F3} km e={1:F8} inc={2:F6} lan={3:F6} "
                + "argPe={4:F6} M={5:F6}",
                c.SemiMajorAxis, c.Eccentricity, c.Inclination,
                Norm(c.LongitudeOfAscendingNode), Norm(c.ArgumentOfPeriapsis),
                Norm(c.MeanAnomaly));
            return sb.ToString();
        }

        private static double Norm(double deg)
        {
            double d = deg % 360.0;
            return d < 0.0 ? d + 360.0 : d;
        }
    }
}
