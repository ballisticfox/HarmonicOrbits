using System;

namespace HarmonicOrbits
{
    /// <summary>
    /// One orbital element as a function of time: a secular polynomial plus a sum of
    /// sinusoids.
    /// </summary>
    public sealed class ElementSeries
    {
        private readonly double[] _secular;   //highest power first, Horner order
        private readonly double _constant;
        private readonly double[] _omegas;    //rad/day
        private readonly double[] _cos;
        private readonly double[] _sin;

        /// <summary>True for an element that grows without bound (e.g. mean longitude).</summary>
        public bool Circulating { get; private set; }

        public int TermCount => _omegas.Length;
        public int SecularDegree => _secular.Length - 1;


        public ElementSeries(bool circulating, double[] secular, double constant,
            double[] omegas, double[] cos, double[] sin)
        {
            if (secular == null) throw new ArgumentNullException("secular");
            if (omegas == null) throw new ArgumentNullException("omegas");
            if (cos == null) throw new ArgumentNullException("cos");
            if (sin == null) throw new ArgumentNullException("sin");
            if (secular.Length == 0)
            {
                throw new ArgumentException("secular must hold at least one coefficient");
            }
            if (omegas.Length != cos.Length || omegas.Length != sin.Length)
            {
                throw new ArgumentException(string.Format(
                    "term counts disagree: {0} omegas, {1} cos, {2} sin",
                    omegas.Length, cos.Length, sin.Length));
            }

            Circulating = circulating;
            _secular = secular;
            _constant = constant;
            _omegas = omegas;
            _cos = cos;
            _sin = sin;
        }

        /// <summary>Value at <paramref name="t"/> days after the model epoch.</summary>
        public double Evaluate(double t)
        {
            // Horner order, matching numpy's polyval.
            double v = 0.0;
            for (int i = 0; i < _secular.Length; i++)
            {
                v = v * t + _secular[i];
            }
            v += _constant;
            for (int i = 0; i < _omegas.Length; i++)
            {
                double wt = _omegas[i] * t;
                v += _cos[i] * Math.Cos(wt) + _sin[i] * Math.Sin(wt);
            }
            return v;
        }

        /// <summary>Angular frequency of one term, rad/day. Diagnostics only.</summary>
        public double Omega(int index)
        {
            return _omegas[index];
        }
    }
}
