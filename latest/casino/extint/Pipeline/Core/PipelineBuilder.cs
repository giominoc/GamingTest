using System;
using System.Collections.Generic;

namespace GamingTests.Latest.Casino.ExtInt.Pipeline.Core
{
    /// <summary>
    /// Builder for creating customizable pipelines
    /// </summary>
    /// <typeparam name="TContext">The type of shared context</typeparam>
    public sealed class PipelineBuilder<TContext>
    {
        private readonly PipelinePlan<TContext> _plan;

        public PipelineBuilder()
        {
            _plan = new PipelinePlan<TContext>();
        }

        public PipelineBuilder(PipelinePlan<TContext> basePlan)
        {
            _plan = basePlan.Clone();
        }

        /// <summary>
        /// Adds a step to the pipeline
        /// </summary>
        public PipelineBuilder<TContext> AddStep(IPipelineStep<TContext> step)
        {
            _plan.Add(step);
            return this;
        }

        /// <summary>
        /// Applies a customization function to the pipeline plan
        /// </summary>
        public PipelineBuilder<TContext> Customize(Action<PipelinePlan<TContext>> customization)
        {
            if (customization == null) throw new ArgumentNullException(nameof(customization));
            customization(_plan);
            return this;
        }

        /// <summary>
        /// Builds the pipeline engine
        /// </summary>
        public PipelineEngine<TContext> Build()
        {
            return new PipelineEngine<TContext>(_plan);
        }

        /// <summary>
        /// Gets the current plan (for inspection or further customization)
        /// </summary>
        public PipelinePlan<TContext> GetPlan()
        {
            return _plan;
        }
    }
}
