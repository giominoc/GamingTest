using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace GamingTests.Latest.Casino.ExtInt
{
    public sealed class InMemoryWalletSharedHelpers : IWalletSharedHelpersContract
    {
        private readonly ConcurrentDictionary<string, WalletMovement> _movements = new();
        private readonly ConcurrentDictionary<string, long> _balances = new();
        private readonly ConcurrentDictionary<string, string> _sessions = new();

        private static string MovementKey(string provider, string transferId) => $"{provider}::{transferId}";
        private static string PlayerKey(string provider, string playerId) => $"{provider}::{playerId}";

        public Task<bool> IsDuplicateAsync(string provider, string transferId, CancellationToken cancellationToken = default)
            => Task.FromResult(_movements.ContainsKey(MovementKey(provider, transferId)));

        public Task<bool> HasValidSessionAsync(string provider, string playerId, string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(sessionId)) return Task.FromResult(false);

            var key = PlayerKey(provider, playerId);
            if (_sessions.TryGetValue(key, out var existing)) return Task.FromResult(existing == sessionId);

            _sessions[key] = sessionId;
            return Task.FromResult(true);
        }

        public Task<long> GetBalanceAsync(string provider, string playerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_balances.TryGetValue(PlayerKey(provider, playerId), out var value) ? value : 0L);

        public Task SetBalanceAsync(string provider, string playerId, long newBalance, CancellationToken cancellationToken = default)
        {
            _balances[PlayerKey(provider, playerId)] = newBalance;
            return Task.CompletedTask;
        }

        public Task<WalletMovement> PersistMovementAsync(WalletMovement movement, CancellationToken cancellationToken = default)
        {
            _movements[MovementKey(movement.Provider, movement.TransferId)] = movement;
            return Task.FromResult(movement);
        }

        public Task MarkCancelledAsync(string provider, string gameRound, string transferId, CancellationToken cancellationToken = default)
        {
            if (_movements.TryGetValue(MovementKey(provider, transferId), out var movement)) movement.Status = "Cancelled";
            return Task.CompletedTask;
        }
    }
}
