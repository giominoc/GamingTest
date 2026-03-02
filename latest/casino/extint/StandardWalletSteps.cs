using System.Threading;
using System.Threading.Tasks;

namespace GamingTests.Latest.Casino.ExtInt
{
    public sealed class ValidateRequestStep : IWalletStep
    {
        private readonly IProviderWalletRules _rules;

        public ValidateRequestStep(IProviderWalletRules rules)
        {
            _rules = rules;
        }

        public async Task<WalletStepResult> ExecuteAsync(WalletContext context, CancellationToken cancellationToken = default)
        {
            var result = await _rules.ValidateRequestAsync(context, cancellationToken).ConfigureAwait(false);
            return result.IsOk || result.Code == "200" ? WalletStepResult.Next() : WalletStepResult.Stop(result);
        }
    }

    public sealed class SessionStep : IWalletStep
    {
        private readonly IProviderWalletRules _rules;
        private readonly IWalletSharedHelpersContract _helpers;

        public SessionStep(IProviderWalletRules rules, IWalletSharedHelpersContract helpers)
        {
            _rules = rules;
            _helpers = helpers;
        }

        public async Task<WalletStepResult> ExecuteAsync(WalletContext context, CancellationToken cancellationToken = default)
        {
            var result = await _rules.CheckSessionAsync(context, _helpers, cancellationToken).ConfigureAwait(false);
            return result.IsOk || result.Code == "200" ? WalletStepResult.Next() : WalletStepResult.Stop(result);
        }
    }

    public sealed class IdempotencyStep : IWalletStep
    {
        private readonly IProviderWalletRules _rules;
        private readonly IWalletSharedHelpersContract _helpers;

        public IdempotencyStep(IProviderWalletRules rules, IWalletSharedHelpersContract helpers)
        {
            _rules = rules;
            _helpers = helpers;
        }

        public async Task<WalletStepResult> ExecuteAsync(WalletContext context, CancellationToken cancellationToken = default)
        {
            var result = await _rules.CheckIdempotencyAsync(context, _helpers, cancellationToken).ConfigureAwait(false);
            return result.IsOk || result.Code == "200" ? WalletStepResult.Next() : WalletStepResult.Stop(result);
        }
    }

    public sealed class MovementStep : IWalletStep
    {
        private readonly IProviderWalletRules _rules;
        private readonly IWalletSharedHelpersContract _helpers;

        public MovementStep(IProviderWalletRules rules, IWalletSharedHelpersContract helpers)
        {
            _rules = rules;
            _helpers = helpers;
        }

        public async Task<WalletStepResult> ExecuteAsync(WalletContext context, CancellationToken cancellationToken = default)
        {
            context.Movement = _rules.CreateMovement(context);
            context.Movement = await _helpers.PersistMovementAsync(context.Movement, cancellationToken).ConfigureAwait(false);
            return WalletStepResult.Next();
        }
    }

    public sealed class ExternalWalletStep : IWalletStep
    {
        private readonly IProviderWalletRules _rules;
        private readonly IWalletSharedHelpersContract _helpers;

        public ExternalWalletStep(IProviderWalletRules rules, IWalletSharedHelpersContract helpers)
        {
            _rules = rules;
            _helpers = helpers;
        }

        public async Task<WalletStepResult> ExecuteAsync(WalletContext context, CancellationToken cancellationToken = default)
        {
            var result = await _rules.ExecuteWalletAsync(context, _helpers, cancellationToken).ConfigureAwait(false);
            if (!result.IsOk)
            {
                return WalletStepResult.Stop(result);
            }

            context.Result = result;
            return WalletStepResult.Next();
        }
    }

    public sealed class FinalizeStep : IWalletStep
    {
        public Task<WalletStepResult> ExecuteAsync(WalletContext context, CancellationToken cancellationToken = default)
        {
            if (context.Result is null)
            {
                return Task.FromResult(WalletStepResult.Stop(WalletResult.Fail("500", "MISSING_RESULT", 0)));
            }

            return Task.FromResult(WalletStepResult.Stop(context.Result));
        }
    }
}
