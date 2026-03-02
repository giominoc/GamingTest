using System;
using System.Collections.Generic;

namespace GamingTests.Latest.Casino.ExtInt.Pipeline.Core
{
    /// <summary>
    /// Registry for pre-compiled (pre-configured) pipeline steps.
    /// Allows reusing step instances across multiple pipelines.
    /// </summary>
    /// <typeparam name="TContext">The type of shared context</typeparam>
    public sealed class PrecompiledSteps<TContext>
    {
        private readonly Dictionary<string, IPipelineStep<TContext>> _steps;

        public PrecompiledSteps()
        {
            _steps = new Dictionary<string, IPipelineStep<TContext>>();
        }

        /// <summary>
        /// Registers a pre-compiled step with a given name
        /// </summary>
        public PrecompiledSteps<TContext> Register(string name, IPipelineStep<TContext> step)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or empty", nameof(name));
            if (step == null) throw new ArgumentNullException(nameof(step));
            
            _steps[name] = step;
            return this;
        }

        /// <summary>
        /// Gets a pre-compiled step by name
        /// </summary>
        public IPipelineStep<TContext> Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or empty", nameof(name));
            
            if (!_steps.TryGetValue(name, out var step))
                throw new InvalidOperationException($"Pre-compiled step '{name}' not found");
            
            return step;
        }

        /// <summary>
        /// Tries to get a pre-compiled step by name
        /// </summary>
        public bool TryGet(string name, out IPipelineStep<TContext> step)
        {
            step = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _steps.TryGetValue(name, out step);
        }

        /// <summary>
        /// Checks if a pre-compiled step exists
        /// </summary>
        public bool Contains(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _steps.ContainsKey(name);
        }

        /// <summary>
        /// Gets all registered step names
        /// </summary>
        public IEnumerable<string> GetNames()
        {
            return _steps.Keys;
        }
    }
}
