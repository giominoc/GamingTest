using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamingTests.Latest.Casino.ExtInt;

namespace GamingTests.Latest.Casino.ExtInt.AM
{
    public sealed class AMDispatcher : ICasinoExtIntFaceContract
    {
        private readonly IWalletPipelineContract _pipeline;

        public AMDispatcher(IWalletSharedHelpersContract helpers)
        {
            _pipeline = new AMWalletCore(helpers);
        }

        public Task<WalletResult> GetAuxInfosAsync(string action, IDictionary<string, object> payload, CancellationToken cancellationToken = default)
        {
            var request = Map(payload);
            return (action ?? string.Empty).ToLowerInvariant() switch
            {
                "withdraw" => _pipeline.ExecuteAsync(WalletOperation.Bet, request, cancellationToken),
                "deposit" => _pipeline.ExecuteAsync(WalletOperation.Win, request, cancellationToken),
                "rollback" => _pipeline.ExecuteAsync(WalletOperation.Cancel, request, cancellationToken),
                _ => Task.FromResult(WalletResult.Fail("409", "INVALID_ACTION", 0))
            };
        }

        private static WalletRequest Map(IDictionary<string, object> payload)
        {
            return new WalletRequest
            {
                Provider = "AM",
                PlayerId = payload.TryGetValue("playerId", out var player) ? (string)player : string.Empty,
                TransferId = payload.TryGetValue("transferId", out var transfer) ? (string)transfer : string.Empty,
                SessionId = payload.TryGetValue("sessionId", out var session) ? (string)session : string.Empty,
                GameId = payload.TryGetValue("gameId", out var game) ? (string)game : string.Empty,
                GameRound = payload.TryGetValue("gameNumber", out var round) ? (string)round : string.Empty,
                Amount = payload.TryGetValue("amount", out var amount) ? (long)amount : 0,
                Currency = payload.TryGetValue("currency", out var currency) ? (string)currency : "EUR",
                ForceRoundClose = payload.TryGetValue("forceRoundClose", out var frc) && (bool)frc
            };
        }
    }
}
