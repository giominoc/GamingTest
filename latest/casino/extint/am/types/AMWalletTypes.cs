namespace GamingTests.Latest.Casino.ExtInt.AM.Types
{
    public sealed class AMWithdrawRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public required string gameId { get; init; }
        public string gameNumber { get; init; } = string.Empty;
        public long amount { get; init; }
    }

    public sealed class AMDepositRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public required string gameId { get; init; }
        public string gameNumber { get; init; } = string.Empty;
        public long amount { get; init; }
        public bool forceRoundClose { get; init; }
    }

    public sealed class AMRollbackRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public string gameNumber { get; init; } = string.Empty;
    }
}
