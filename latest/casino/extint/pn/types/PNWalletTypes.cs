namespace GamingTests.Latest.Casino.ExtInt.PN.Types
{
    public sealed class PNBetRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public required string gameId { get; init; }
        public string roundRef { get; init; } = string.Empty;
        public long amount { get; init; }
    }

    public sealed class PNWinRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public required string gameId { get; init; }
        public string roundRef { get; init; } = string.Empty;
        public long amount { get; init; }
    }

    public sealed class PNRollbackRequest
    {
        public required string playerId { get; init; }
        public required string transferId { get; init; }
        public required string sessionId { get; init; }
        public string roundRef { get; init; } = string.Empty;
    }
}
