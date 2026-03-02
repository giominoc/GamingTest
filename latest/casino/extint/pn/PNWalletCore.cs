using System;
using System.Threading;
using System.Threading.Tasks;
using GamingTests.Latest.Casino.ExtInt;

namespace GamingTests.Latest.Casino.ExtInt.PN
{
    public sealed class PNWalletRules : IProviderWalletRules
    {
        public string Provider => "PN";

        public Task<WalletResult> ValidateRequestAsync(WalletContext context, CancellationToken cancellationToken = default)
        {
            var request = context.Request;
            if (string.IsNullOrWhiteSpace(request.PlayerId) || string.IsNullOrWhiteSpace(request.TransferId) || string.IsNullOrWhiteSpace(request.SessionId))
                return Task.FromResult(WalletResult.Fail("409", "BAD_REQUEST", 0));
            if (context.Operation != WalletOperation.Cancel && request.Amount < 0)
                return Task.FromResult(WalletResult.Fail("409", "BAD_REQUEST", 0));
            return Task.FromResult(WalletResult.Ok(0, string.Empty));
        }

        public async Task<WalletResult> CheckSessionAsync(WalletContext context, IWalletSharedHelpersContract helpers, CancellationToken cancellationToken = default)
        {
            var request = context.Request;
            if (!await helpers.HasValidSessionAsync(Provider, request.PlayerId, request.SessionId, cancellationToken).ConfigureAwait(false))
            {
                var balance = await helpers.GetBalanceAsync(Provider, request.PlayerId, cancellationToken).ConfigureAwait(false);
                return WalletResult.Fail("401", "INVALID_SESSION", balance);
            }
            return WalletResult.Ok(0, string.Empty);
        }

        public async Task<WalletResult> CheckIdempotencyAsync(WalletContext context, IWalletSharedHelpersContract helpers, CancellationToken cancellationToken = default)
        {
            var request = context.Request;
            if (await helpers.IsDuplicateAsync(Provider, request.TransferId, cancellationToken).ConfigureAwait(false))
            {
                var balance = await helpers.GetBalanceAsync(Provider, request.PlayerId, cancellationToken).ConfigureAwait(false);
                return WalletResult.Fail("409", "DUPLICATE", balance);
            }
            return WalletResult.Ok(0, string.Empty);
        }

        public WalletMovement CreateMovement(WalletContext context)
        {
            var request = context.Request;
            return new WalletMovement
            {
                Provider = Provider,
                Operation = context.Operation,
                TransferId = request.TransferId,
                GameRound = request.GameRound,
                Amount = request.Amount,
                UtcDate = DateTime.UtcNow,
                Status = "Created"
            };
        }

        public async Task<WalletResult> ExecuteWalletAsync(WalletContext context, IWalletSharedHelpersContract helpers, CancellationToken cancellationToken = default)
        {
            var request = context.Request;
            var balance = await helpers.GetBalanceAsync(Provider, request.PlayerId, cancellationToken).ConfigureAwait(false);
            switch (context.Operation)
            {
                case WalletOperation.Bet:
                    if (balance < request.Amount) return WalletResult.Fail("409", "Insufficient balance", balance);
                    balance -= request.Amount;
                    break;
                case WalletOperation.Win:
                    balance += request.Amount;
                    break;
                case WalletOperation.Cancel:
                    await helpers.MarkCancelledAsync(Provider, request.GameRound, request.TransferId, cancellationToken).ConfigureAwait(false);
                    balance += request.Amount;
                    break;
            }

            await helpers.SetBalanceAsync(Provider, request.PlayerId, balance, cancellationToken).ConfigureAwait(false);
            return WalletResult.Ok(balance, $"PN-{request.TransferId}");
        }
    }

    public sealed class PNWalletCore : BaseProviderWalletCore
    {
        public PNWalletCore(IWalletSharedHelpersContract helpers) : base(new PNWalletRules(), helpers)
        {
        }
    }
}
