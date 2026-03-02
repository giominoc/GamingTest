using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GamingTests.Latest.Casino.ExtInt.Pipeline.Core
{
    /// <summary>
    /// Executes a pipeline of steps with a shared context
    /// </summary>
    /// <typeparam name="TContext">The type of shared context</typeparam>
    public sealed class PipelineEngine<TContext>
    {
        private readonly IReadOnlyList<IPipelineStep<TContext>> _steps;

        public PipelineEngine(IReadOnlyList<IPipelineStep<TContext>> steps)
        {
            _steps = steps;
        }

        public PipelineEngine(PipelinePlan<TContext> plan)
        {
            _steps = plan.Steps;
        }

        /// <summary>
        /// Executes all steps in order until completion or a step returns Stop
        /// </summary>
        public async Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        {
            foreach (var step in _steps)
            {
                var result = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                if (!result.Continue)
                {
                    break;
                }
            }
        }
    }
}
