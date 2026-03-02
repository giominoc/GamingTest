using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GamingTests.Latest.Casino.ExtInt
{
    public sealed class WalletPipelineOrchestrator : IWalletPipelineContract
    {
        private readonly IReadOnlyList<IWalletStep> _steps;

        public WalletPipelineOrchestrator(IReadOnlyList<IWalletStep> steps)
        {
            _steps = steps;
        }

        public async Task<WalletResult> ExecuteAsync(WalletOperation operation, WalletRequest request, CancellationToken cancellationToken = default)
        {
            var context = new WalletContext
            {
                Operation = operation,
                Request = request
            };

            foreach (var step in _steps)
            {
                var stepResult = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                if (!stepResult.Continue)
                {
                    return stepResult.Result ?? WalletResult.Fail("500", "EMPTY_PIPELINE_RESULT", 0);
                }
            }

            return context.Result ?? WalletResult.Fail("500", "MISSING_FINAL_RESULT", 0);
        }
    }
}
