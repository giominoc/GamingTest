using System;
using System.Collections.Generic;
using System.Linq;

namespace GamingTests.Latest.Casino.ExtInt.Pipeline.Core
{
    /// <summary>
    /// Represents a modifiable plan for a pipeline execution.
    /// Allows customization of step ordering through composition.
    /// </summary>
    /// <typeparam name="TContext">The type of context shared across pipeline steps</typeparam>
    public sealed class PipelinePlan<TContext>
    {
        private readonly List<IPipelineStep<TContext>> _steps;

        public PipelinePlan()
        {
            _steps = new List<IPipelineStep<TContext>>();
        }

        public PipelinePlan(IEnumerable<IPipelineStep<TContext>> steps)
        {
            _steps = new List<IPipelineStep<TContext>>(steps);
        }

        /// <summary>
        /// Gets all steps in the current plan
        /// </summary>
        public IReadOnlyList<IPipelineStep<TContext>> Steps => _steps.AsReadOnly();

        /// <summary>
        /// Adds a step to the end of the pipeline
        /// </summary>
        public PipelinePlan<TContext> Add(IPipelineStep<TContext> step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Inserts a step before the first occurrence of the specified step type
        /// </summary>
        public PipelinePlan<TContext> InsertBefore<TStep>(IPipelineStep<TContext> newStep) where TStep : IPipelineStep<TContext>
        {
            if (newStep == null) throw new ArgumentNullException(nameof(newStep));
            
            var index = _steps.FindIndex(s => s is TStep);
            if (index == -1)
                throw new InvalidOperationException($"Step of type {typeof(TStep).Name} not found in pipeline");
            
            _steps.Insert(index, newStep);
            return this;
        }

        /// <summary>
        /// Inserts a step after the first occurrence of the specified step type
        /// </summary>
        public PipelinePlan<TContext> InsertAfter<TStep>(IPipelineStep<TContext> newStep) where TStep : IPipelineStep<TContext>
        {
            if (newStep == null) throw new ArgumentNullException(nameof(newStep));
            
            var index = _steps.FindIndex(s => s is TStep);
            if (index == -1)
                throw new InvalidOperationException($"Step of type {typeof(TStep).Name} not found in pipeline");
            
            _steps.Insert(index + 1, newStep);
            return this;
        }

        /// <summary>
        /// Replaces the first occurrence of the specified step type with a new step
        /// </summary>
        public PipelinePlan<TContext> Replace<TStep>(IPipelineStep<TContext> newStep) where TStep : IPipelineStep<TContext>
        {
            if (newStep == null) throw new ArgumentNullException(nameof(newStep));
            
            var index = _steps.FindIndex(s => s is TStep);
            if (index == -1)
                throw new InvalidOperationException($"Step of type {typeof(TStep).Name} not found in pipeline");
            
            _steps[index] = newStep;
            return this;
        }

        /// <summary>
        /// Removes all occurrences of the specified step type
        /// </summary>
        public PipelinePlan<TContext> Remove<TStep>() where TStep : IPipelineStep<TContext>
        {
            _steps.RemoveAll(s => s is TStep);
            return this;
        }

        /// <summary>
        /// Wraps the first occurrence of the specified step type with a wrapper step
        /// </summary>
        public PipelinePlan<TContext> Wrap<TStep>(Func<IPipelineStep<TContext>, IPipelineStep<TContext>> wrapperFactory) 
            where TStep : IPipelineStep<TContext>
        {
            if (wrapperFactory == null) throw new ArgumentNullException(nameof(wrapperFactory));
            
            var index = _steps.FindIndex(s => s is TStep);
            if (index == -1)
                throw new InvalidOperationException($"Step of type {typeof(TStep).Name} not found in pipeline");
            
            var originalStep = _steps[index];
            var wrappedStep = wrapperFactory(originalStep);
            _steps[index] = wrappedStep;
            return this;
        }

        /// <summary>
        /// Creates a copy of this plan
        /// </summary>
        public PipelinePlan<TContext> Clone()
        {
            return new PipelinePlan<TContext>(_steps);
        }
    }
}
