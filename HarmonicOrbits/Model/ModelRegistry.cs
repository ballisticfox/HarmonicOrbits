using System;
using System.Collections.Generic;

namespace HarmonicOrbits
{
    /// <summary>Body name to model lookup. Case-insensitive.</summary>
    public sealed class ModelRegistry
    {
        private readonly Dictionary<string, BodyModel> _models =
            new Dictionary<string, BodyModel>(StringComparer.OrdinalIgnoreCase);

        public int Count => _models.Count;

        public IEnumerable<BodyModel> Models => _models.Values;

        /// <summary>Registers a model. Last one wins for a given name.</summary>
        public void Add(BodyModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            _models[model.Name] = model;
        }

        public void AddRange(IEnumerable<BodyModel> models)
        {
            if (models == null) throw new ArgumentNullException("models");
            foreach (BodyModel model in models)
            {
                Add(model);
            }
        }

        public bool TryGet(string bodyName, out BodyModel model)
        {
            if (string.IsNullOrEmpty(bodyName))
            {
                model = null;
                return false;
            }
            return _models.TryGetValue(bodyName, out model);
        }

        public bool Contains(string bodyName)
        {
            return TryGet(bodyName, out BodyModel ignored);
        }
    }
}
