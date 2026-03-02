using System.Threading;
using System.Threading.Tasks;

namespace GamingTests.Latest.Casino.ExtInt
{
    public interface IProviderWalletRules
    {
        string Provider { get; }
        Task<WalletResult> ValidateRequestAsync(WalletContext context, CancellationToken cancellationToken = default);
        Task<WalletResult> CheckSessionAsync(WalletContext context, IWalletSharedHelpersContract helpers, CancellationToken cancellationToken = default);
        Task<WalletResult> CheckIdempotencyAsync(WalletContext context, IWalletSharedHelpersContract helpers, CancellationToken cancellationToken = default);
        WalletMovement CreateMovement(WalletContext context);
        Task<WalletResult> ExecuteWalletAsync(WalletContext context, IWalletSharedHelpersContract helpers, CancellationToken cancellationToken = default);
    }
}
