using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GamingTests.Latest.Casino.ExtInt
{
    public interface ICasinoExtIntFaceContract
    {
        Task<WalletResult> GetAuxInfosAsync(string action, IDictionary<string, object> payload, CancellationToken cancellationToken = default);
    }

    public interface IWalletPipelineContract
    {
        Task<WalletResult> ExecuteAsync(WalletOperation operation, WalletRequest request, CancellationToken cancellationToken = default);
    }

    public interface IWalletSharedHelpersContract
    {
        Task<bool> IsDuplicateAsync(string provider, string transferId, CancellationToken cancellationToken = default);
        Task<bool> HasValidSessionAsync(string provider, string playerId, string sessionId, CancellationToken cancellationToken = default);
        Task<long> GetBalanceAsync(string provider, string playerId, CancellationToken cancellationToken = default);
        Task SetBalanceAsync(string provider, string playerId, long newBalance, CancellationToken cancellationToken = default);
        Task<WalletMovement> PersistMovementAsync(WalletMovement movement, CancellationToken cancellationToken = default);
        Task MarkCancelledAsync(string provider, string gameRound, string transferId, CancellationToken cancellationToken = default);
    }

    public interface IWalletStep
    {
        Task<WalletStepResult> ExecuteAsync(WalletContext context, CancellationToken cancellationToken = default);
    }
}
