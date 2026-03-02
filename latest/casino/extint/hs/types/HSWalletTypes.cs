namespace GamingTests.Latest.Casino.ExtInt.HS.Types
{
    public sealed class HSBetRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public required string gameId { get; init; }
        public string roundRef { get; init; } = string.Empty;
        public long amount { get; init; }
    }

    public sealed class HSWinRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public required string gameId { get; init; }
        public string roundRef { get; init; } = string.Empty;
        public long amount { get; init; }
    }

    public sealed class HSRollbackRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public string roundRef { get; init; } = string.Empty;
    }
}
