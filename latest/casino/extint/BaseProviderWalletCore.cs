using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GamingTests.Latest.Casino.ExtInt
{
    public abstract class BaseProviderWalletCore : IWalletPipelineContract
    {
        private readonly WalletPipelineOrchestrator _orchestrator;

        protected BaseProviderWalletCore(IProviderWalletRules rules, IWalletSharedHelpersContract helpers)
        {
            _orchestrator = new WalletPipelineOrchestrator(new List<IWalletStep>
            {
                new ValidateRequestStep(rules),
                new SessionStep(rules, helpers),
                new IdempotencyStep(rules, helpers),
                new MovementStep(rules, helpers),
                new ExternalWalletStep(rules, helpers),
                new FinalizeStep()
            });
        }

        public Task<WalletResult> ExecuteAsync(WalletOperation operation, WalletRequest request, CancellationToken cancellationToken = default)
        {
            return _orchestrator.ExecuteAsync(operation, request, cancellationToken);
        }
    }
}
