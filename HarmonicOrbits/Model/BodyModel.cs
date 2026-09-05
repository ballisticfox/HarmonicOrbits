using System;

namespace HarmonicOrbits
{
    /// <summary>One body's complete model: six element series plus epoch and GM.</summary>
    public sealed class BodyModel
    {
        // Positional order matches the pack format.
        public const int IndexA = 0;
        public const int IndexH = 1;
        public const int IndexK = 2;
        public const int IndexP = 3;
        public const int IndexQ = 4;
        public const int IndexLambda = 5;
        public const int ElementCount = 6;

        private readonly ElementSeries[] _series;

        /// <summary>Body name as KSP knows it.</summary>
        public string Name { get; private set; }

        /// <summary>Julian date at which model time is zero.</summary>
        public double EpochJd { get; private set; }

        /// <summary>Days of fit span from the epoch.</summary>
        public double SpanDays { get; private set; }

        /// <summary>GM in km^3/s^2</summary>
        public double GravParameter { get; private set; }

        public BodyModel(string name, double epochJd, double spanDays, double gravParameter,
            ElementSeries[] series)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name is empty");
            if (series == null) throw new ArgumentNullException("series");
            if (series.Length != ElementCount)
            {
                throw new ArgumentException(string.Format(
                    "{0}: expected {1} element series, got {2}",
                    name, ElementCount, series.Length));
            }
            for (int i = 0; i < series.Length; i++)
            {
                if (series[i] == null)
                {
                    throw new ArgumentException(string.Format(
                        "{0}: element {1} is null", name, i));
                }
            }

            Name = name;
            EpochJd = epochJd;
            SpanDays = spanDays;
            GravParameter = gravParameter;
            _series = series;
        }

        public ElementSeries Series(int index)
        {
            return _series[index];
        }
        /// <summary>Total stored coefficients.</summary>
        public int CoefficientCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _series.Length; i++)
                {
                    n += _series[i].CoefficientCount;
                }
                return n;
            }
        }

        /// <summary>Sidereal orbital period in seconds, from the secular mean-longitude rate.</summary>
        // Not 2*pi*sqrt(a^3/GM): that reads the osculating a, which swings 8,000 km for the
        // Moon and drags the period 21 hours with it. A tidally locked body spins uniformly,
        // so the mean rate is the physical one.
        public double MeanOrbitalPeriod()
        {
            ElementSeries lambda = _series[IndexLambda];
            double t0 = ModelEpoch.ToModelTime(ValidityWindow.StartUt, EpochJd);
            double t1 = ModelEpoch.ToModelTime(ValidityWindow.EndUt, EpochJd);
            double degreesPerDay = (lambda.Evaluate(t1) - lambda.Evaluate(t0)) / (t1 - t0);
            return 360.0 / degreesPerDay * ModelEpoch.SecondsPerDay;
        }

        /// <summary>Elements at <paramref name="t"/> days after the epoch.</summary>
        public EquinoctialElements Evaluate(double t)
        {
            EquinoctialElements e;
            e.A = _series[IndexA].Evaluate(t);
            e.H = _series[IndexH].Evaluate(t);
            e.K = _series[IndexK].Evaluate(t);
            e.P = _series[IndexP].Evaluate(t);
            e.Q = _series[IndexQ].Evaluate(t);
            e.Lambda = _series[IndexLambda].Evaluate(t);
            return e;
        }

        public ClassicalElements EvaluateClassical(double t)
        {
            return Evaluate(t).ToClassical();
        }

        /// <summary>Elements at a KSP universal time.</summary>
        public ClassicalElements EvaluateAtUniversalTime(double ut)
        {
            return EvaluateClassical(ModelEpoch.ToModelTime(ut, EpochJd));
        }
    }
}
