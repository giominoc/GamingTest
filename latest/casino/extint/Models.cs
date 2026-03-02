using System;
using System.Collections.Generic;

namespace GamingTests.Latest.Casino.ExtInt
{
    public enum WalletOperation
    {
        Bet,
        Win,
        Cancel
    }

    public sealed class WalletRequest
    {
        public required string Provider { get; init; }
        public required string PlayerId { get; init; }
        public required string TransferId { get; init; }
        public required string SessionId { get; init; }
        public string GameId { get; init; } = string.Empty;
        public string GameRound { get; init; } = string.Empty;
        public long Amount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public bool ForceRoundClose { get; init; }
    }

    public sealed class WalletMovement
    {
        public required string Provider { get; init; }
        public required WalletOperation Operation { get; init; }
        public required string TransferId { get; init; }
        public required string GameRound { get; init; }
        public required long Amount { get; init; }
        public required DateTime UtcDate { get; init; }
        public string Status { get; set; } = "New";
    }

    public sealed class WalletResult
    {
        public bool IsOk { get; init; }
        public string Code { get; init; } = "500";
        public string Error { get; init; } = string.Empty;
        public long Balance { get; init; }
        public string CasinoTransferId { get; init; } = string.Empty;

        public static WalletResult Ok(long balance, string transferId) => new WalletResult
        {
            IsOk = true,
            Code = "200",
            Balance = balance,
            CasinoTransferId = transferId,
            Error = string.Empty
        };

        public static WalletResult Fail(string code, string error, long balance) => new WalletResult
        {
            IsOk = false,
            Code = code,
            Error = error,
            Balance = balance
        };
    }

    public sealed class WalletContext
    {
        public required WalletOperation Operation { get; init; }
        public required WalletRequest Request { get; init; }
        public WalletMovement? Movement { get; set; }
        public WalletResult? Result { get; set; }
        public IDictionary<string, object> Bag { get; } = new Dictionary<string, object>();
    }

    public sealed class WalletStepResult
    {
        public bool Continue { get; init; } = true;
        public WalletResult? Result { get; init; }

        public static WalletStepResult Next() => new WalletStepResult { Continue = true };
        public static WalletStepResult Stop(WalletResult result) => new WalletStepResult { Continue = false, Result = result };
    }
}
