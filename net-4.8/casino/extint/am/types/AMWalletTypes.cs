namespace GamingTests.Net48.Casino.ExtInt.AM.Types
{
    public class AMWithdrawRequest
    {
        public string playerId { get; set; }
        public string transferId { get; set; }
        public string sessionId { get; set; }
        public string gameId { get; set; }
        public string gameNumber { get; set; }
        public long amount { get; set; }
    }

    public class AMDepositRequest
    {
        public string playerId { get; set; }
        public string transferId { get; set; }
        public string sessionId { get; set; }
        public string gameId { get; set; }
        public string gameNumber { get; set; }
        public long amount { get; set; }
        public bool forceRoundClose { get; set; }
    }

    public class AMRollbackRequest
    {
        public string playerId { get; set; }
        public string transferId { get; set; }
        public string sessionId { get; set; }
        public string gameNumber { get; set; }
    }
}
