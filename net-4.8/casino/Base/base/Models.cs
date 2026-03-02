using System;

namespace GamingTests.Net48.Casino.ExtInt
{
    public enum WalletOperation
    {
        Bet,
        Win,
        Cancel
    }

    public class WalletRequest
    {
        public string Provider { get; set; }
        public string PlayerId { get; set; }
        public string TransferId { get; set; }
        public string SessionId { get; set; }
        public string GameId { get; set; }
        public string GameRound { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public bool ForceRoundClose { get; set; }
    }

    public class WalletMovement
    {
        public string Provider { get; set; }
        public WalletOperation Operation { get; set; }
        public string TransferId { get; set; }
        public string GameRound { get; set; }
        public long Amount { get; set; }
        public DateTime UtcDate { get; set; }
        public string Status { get; set; }
    }

    public class WalletResult
    {
        public bool IsOk { get; set; }
        public string Code { get; set; }
        public string Error { get; set; }
        public long Balance { get; set; }
        public string CasinoTransferId { get; set; }

        public static WalletResult Ok(long balance, string transferId)
        {
            return new WalletResult
            {
                IsOk = true,
                Code = "200",
                Balance = balance,
                CasinoTransferId = transferId,
                Error = string.Empty
            };
        }

        public static WalletResult Fail(string code, string error, long balance)
        {
            return new WalletResult
            {
                IsOk = false,
                Code = code,
                Error = error,
                Balance = balance,
                CasinoTransferId = string.Empty
            };
        }
    }
}
