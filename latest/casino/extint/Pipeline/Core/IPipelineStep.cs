using System.Threading;
using System.Threading.Tasks;

namespace GamingTests.Latest.Casino.ExtInt.Pipeline.Core
{
    /// <summary>
    /// Represents a single step in a pipeline
    /// </summary>
    /// <typeparam name="TContext">The type of shared context</typeparam>
    public interface IPipelineStep<TContext>
    {
        /// <summary>
        /// Executes the step with the given context
        /// </summary>
        /// <param name="context">The shared pipeline context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating whether to continue or stop the pipeline</returns>
        Task<PipelineStepResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result from executing a pipeline step
    /// </summary>
    public sealed class PipelineStepResult
    {
        /// <summary>
        /// Whether the pipeline should continue to the next step
        /// </summary>
        public bool Continue { get; init; } = true;

        /// <summary>
        /// Creates a result that continues to the next step
        /// </summary>
        public static PipelineStepResult Next() => new PipelineStepResult { Continue = true };

        /// <summary>
        /// Creates a result that stops pipeline execution
        /// </summary>
        public static PipelineStepResult Stop() => new PipelineStepResult { Continue = false };
    }
}
