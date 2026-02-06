using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using it.capecod.data;
using it.capecod.gridgame.business.elements2.logic.casino.extint.gm.types;
using it.capecod.util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace it.capecod.gridgame.business.elements2.logic.casino.extint.am.types
{
    #region response status

    public enum Cam_ResponseStatus
    {
        RS_200_Success = 200,
        RS_401_UnauthorizedAccess = 401,
        RS_403_Forbidden = 403,
        RS_408_RequestTimeout = 408,
        RS_409_Conflict = 409,
        RS_5xx_InternalServerError = 500 // Nota: "5xx" indica tutte le 500, qui rappresentata come 500 generico.
    }

    #endregion response status

    #region classi di def delle comms

    public class Cam_BaseResponse: DTOFace, DTOFaceAllLevel
    {
        public string responseCodeReason { get; set; }
    }

    public class Cam_AuthDefenceCodeRequest
    {
        public string portalCode { get; set; }
        public string playerId { get; set; }
        public string defenceCode { get; set; }
        public string sessionId { get; set; }
        public string gameId { get; set; }
    }

    public class Cam_AuthTokenRequest
    {
        public string portalCode { get; set; }
        public string playerId { get; set; }
        public string authenticationToken { get; set; }
        public string sessionId { get; set; }
        public string gameId { get; set; }
    }

    public class Cam_AuthResponse : Cam_BaseResponse
    {
        public long balance { get; set; }
        public string authenticationToken { get; set; }
        public string errorMessage { get; set; }
    }

    public class Cam_BonusSpinData
    {
        public string identification { get; set; }
        public long campaignUniqueCode { get; set; }
        public long total { get; set; }
        public long remaining { get; set; }
    }

    public class Cam_BonusData
    {
        public string bonusType { get; set; }
        public long bundleId { get; set; }
        public long promotionId { get; set; }
    }

    public class Cam_WithdrawRequest
    {
        public string playerId { get; set; }
        public string transferId { get; set; }
        public string gameId { get; set; }
        public string gameNumber { get; set; }
        public string sessionId { get; set; }
        public long amount { get; set; }
        public long baseBet { get; set; }
        public string currency { get; set; }
        public string reason { get; set; }
        public string portalCode { get; set; }
        public string platformType { get; set; }
        public Cam_BonusSpinData bonusSpin { get; set; }
        public Cam_BonusData bonusData { get; set; }
    }

    public class Cam_WithdrawResponse : Cam_BaseResponse
    {
        public long balance { get; set; }
        public string casinoTransferId { get; set; }
        public string errorMessage { get; set; }
        public long totalBet { get; set; }
        public long totalWin { get; set; }
        public Cam_BetDistribution betDistribution { get; set; }
        public long playTime { get; set; }
    }

    public class Cam_BetDistribution
    {
        public long real { get; set; }
        public long bonus { get; set; }
    }

    public class Cam_DepositRequest
    {
        public string playerId { get; set; }
        public string transferId { get; set; }
        public string gameId { get; set; }
        public string gameNumber { get; set; }
        public string sessionId { get; set; }
        public long amount { get; set; }
        public string currency { get; set; }
        public string reason { get; set; }
        public string portalCode { get; set; }
        public string platformType { get; set; }
        public Cam_BonusSpinData bonusSpin { get; set; }
        public Cam_BonusData bonusData { get; set; }
    }

    public class Cam_DepositResponse : Cam_BaseResponse
    {
        public long balance { get; set; }
        public string casinoTransferId { get; set; }
        public string errorMessage { get; set; }
        public long totalBet { get; set; }
        public long totalWin { get; set; }
        public long playTime { get; set; }
    }

    public class Cam_WithdrawDepositRequest
    {
        public string playerId { get; set; }
        public string transferId { get; set; }
        public string gameId { get; set; }
        public string gameNumber { get; set; }
        public string sessionId { get; set; }
        public long? amount { get; set; }
        public long baseBet { get; set; }
        public string currency { get; set; }
        public string reason { get; set; }
        public string portalCode { get; set; }
        public string platformType { get; set; }
        public Cam_BonusSpinData bonusSpin { get; set; }
        public Cam_BonusData bonusData { get; set; }
        public long? winAmount { get; set; }
    }

    public class Cam_WithdrawDepositResponse : Cam_BaseResponse
    {
        public long balance { get; set; }
        public string casinoTransferId { get; set; }
        public string errorMessage { get; set; }
        public long totalBet { get; set; }
        public long totalWin { get; set; }
        public Cam_BetDistribution betDistribution { get; set; }
        public long playTime { get; set; }
    }

    public class Cam_AwardDepositRequest
    {
        public string playerId { get; set; }
        public string transferId { get; set; }
        public string awardType { get; set; }
        public long? amount { get; set; }
        public string currency { get; set; }
    }

    public class Cam_AwardDepositResponse : Cam_BaseResponse
    {
        public string casinoTransferId { get; set; }
        public long balance { get; set; }
        public string errorMessage { get; set; }
    }

    #endregion classi di def delle comms

    #region core calls

    public class Cam_OGOAuthRequest
    {
        public int casinoOperatorId { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string messageId { get; set; }
        public int eventTimestamp { get; set; }
    }

    public class Cam_OGOAuthResponse
    {
        public string gameLaunchToken { get; set; }
        public string messageId { get; set; }
        public long eventTimestamp { get; set; }
        public int errorCode { get; set; }
    }

    public class Cam_CasinoGame
    {
        public int gin { get; set; }
        public Portal portal { get; set; }
        public string currency { get; set; }
        public List<string> betAmounts { get; set; }
        public List<string> supportedBonusTypes { get; set; }
        public List<BuyBonusFeature> buyBonusFeatures { get; set; }
    }

    public class Portal
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class BuyBonusFeature
    {
        public string id { get; set; }
        public string name { get; set; }
        public string priceMultiplier { get; set; }
    }

    public class Cam_HashCodes
    {
        public string Name { get; set; }
        public string GameId { get; set; }
        public Dictionary<string, Dictionary<string, string>> checksums { get; set; }
    }

    public class Cam_ComplianceAuthRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    #region history response
    public class Cam_HistoryResponse
    {
        public long Gin { get; set; }                              // int64
        public string GameName { get; set; }
        public string GameNumber { get; set; }
        public decimal BetAmount { get; set; }                     // number
        public decimal TotalBetAmount { get; set; }                // number
        public decimal TotalWinAmount { get; set; }                // number
        public decimal WinAmount { get; set; }                     // number
        public decimal ChoiceWinAmount { get; set; }               // number
        public decimal MysteryWinAmount { get; set; }              // number
        public decimal MysteryWheelWinAmount { get; set; }         // number
        public string Currency { get; set; }
        public bool Finished { get; set; }
        public string StartTimestamp { get; set; }                 // string (UTC)
        public string EndTimestamp { get; set; }                   // string (UTC)
        public string Status { get; set; }
        public decimal Overflow { get; set; }                         // int64
        public bool IsMysteryWin { get; set; }
        public string Rtp { get; set; }                            // string
        public Cam_JackpotContributions JackpotContribution { get; set; }
        public decimal BombJackpotWinAmount { get; set; }          // number
        public Cam_Outcome Outcome { get; set; }
        public List<Dictionary<string, object>> Rounds { get; set; }
        public List<Cam_MultiplayerRound> MultiplayerRounds { get; set; }
        public Cam_Gambles Gambles { get; set; }
        public Cam_FreeSpins FreeSpins { get; set; }
        public Cam_Respins Respins { get; set; }
        public List<Cam_PickMultiplier> PickMultipliers { get; set; }
        public bool JackpotTransfer { get; set; }
        public List<Cam_CoinSpins> CoinSpins { get; set; }
        public string GameClientVersion { get; set; }
        public string GameBackendVersion { get; set; }
        public string GameType { get; set; }
        public bool SupportsRandomPresents { get; set; }
        public Cam_Combos Combos { get; set; }
        public Cam_Collectables Collectables { get; set; }
        public Cam_JackpotWin JackpotWin { get; set; }
        public Cam_MaxExposure MaxExposure { get; set; }
    }

    public class Cam_JackpotContributions
    {
        public decimal JpCards { get; set; }
        public decimal CashBomb { get; set; }
        public decimal GoldenCoinsLink { get; set; }
    }

    public class Cam_Outcome
    {
        public string OutcomeUrl { get; set; }
        public string Timestamp { get; set; }                      // string (UTC)
        public decimal WinAmountBefore { get; set; }
        public decimal WinAmountFrom { get; set; }
        public decimal WinAmountAfter { get; set; }
        public int ScatterCombinations { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public List<Cam_Cascade> Cascades { get; set; }
        public Cam_Choices Choices { get; set; }
    }

    public class Cam_Cascade
    {
        public string OutcomeUrl { get; set; }
        public decimal WinAmount { get; set; }
    }

    public class Cam_Choices
    {
        public decimal WinAmount { get; set; }
        public List<Cam_ChoiceItem> ClosedPositions { get; set; }
        public List<Cam_ChoiceItem> OpenedAfterCollect { get; set; }
        public List<Cam_ChoiceItem> Winning { get; set; }
    }

    public class Cam_ChoiceItem
    {
        public int Position { get; set; }
        public decimal Coefficient { get; set; }                   // number
        public bool Collect { get; set; }
        public decimal Multiplayer { get; set; }                   // number (come da JSON)
        public decimal WinAmount { get; set; }
    }

    public class Cam_MultiplayerRound
    {
        public int RoundNumber { get; set; }
        public decimal TotalBetAmount { get; set; }
        public decimal TotalWinAmount { get; set; }
        public Cam_Outcome Outcome { get; set; }
        public Cam_FreeSpins FreeSpins { get; set; }
        public Cam_Respins Respins { get; set; }
        public List<Cam_PlayerBet> PlayerBets { get; set; }
    }

    public class Cam_FreeSpins
    {
        public int Available { get; set; }
        public int Used { get; set; }
        public int BonusSpins { get; set; }
        public int AvailableBonusSpins { get; set; }
        public int UsedBonusSpins { get; set; }
        public List<Cam_Outcome> FreeSpins { get; set; }
        public string BonusWinType { get; set; }                   // "FREE_SPIN", ecc.
        public decimal BonusWinAmount { get; set; }
        public decimal BonusCoefficient { get; set; }
    }

    public class Cam_Respins
    {
        public int Available { get; set; }
        public int Used { get; set; }
        public List<Cam_Outcome> Respins { get; set; }
    }

    public class Cam_PlayerBet
    {
        public string PlayerUid { get; set; }
        public decimal BetAmount { get; set; }
        public decimal WinAmount { get; set; }
        public string Currency { get; set; }
        public Cam_JackpotWin JackpotWin { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class Cam_JackpotWin
    {
        public string JackpotType { get; set; }                    // es. "MLM"
        public int JackpotLevel { get; set; }
        public decimal WinAmount { get; set; }
    }

    public class Cam_Gambles
    {
        public int Available { get; set; }
        public int Used { get; set; }
        public List<Cam_GambleItem> Gambles { get; set; }
    }

    public class Cam_GambleItem
    {
        public int Card { get; set; }
        public int Color { get; set; }
        public decimal WinAmount { get; set; }
        public string Timestamp { get; set; }                      // string (UTC)
    }

    public class Cam_PickMultiplier
    {
        public string Id { get; set; }
        public decimal WinMultiplier { get; set; }
        public decimal LoseMultiplier { get; set; }
        public decimal TotalMultiplier { get; set; }
        public string PickTimestamp { get; set; }                  // string (UTC)
    }

    public class Cam_CoinSpins
    {
        public int Available { get; set; }
        public int Used { get; set; }
        public Cam_Coins Coins { get; set; }
        public decimal WheelMultiplier { get; set; }
        public Cam_Outcome Outcome { get; set; }
    }

    public class Cam_Coins
    {
        public Cam_CoinStats Before { get; set; }
        public Cam_CoinStats Novel { get; set; }
        public Cam_CoinStats Total { get; set; }
        public Cam_CoinStats Mini { get; set; }
        public Cam_CoinStats Minor { get; set; }
        public Cam_CoinStats Major { get; set; }
        public Cam_CoinStats Grand { get; set; }
        public Cam_CoinStats Boost { get; set; }
    }

    public class Cam_CoinStats
    {
        public int Count { get; set; }
        public decimal WinAmount { get; set; }
    }

    public class Cam_Combos
    {
        public int Count { get; set; }
        public decimal Coefficient { get; set; }
    }

    public class Cam_Collectables
    {
        public int Coins { get; set; }
    }

    public class Cam_MaxExposure
    {
        public bool Reached { get; set; }
        public decimal WinOverflow { get; set; }
    }
    #endregion history response

    #region jackpot response

    public class Cam_Jackpot
    {
        #region unnecesary pars
        // Snapshot principale
        //public string Currency { get; set; }

        //public long CurrentLevelI { get; set; }
        //public long WinsLevelI { get; set; }
        //public decimal LargestWinLevelI { get; set; }
        //public string LargestWinDateLevelI { get; set; }
        //public string LargestWinUserLevelI { get; set; }
        //public decimal LastWinLevelI { get; set; }
        //public string LastWinDateLevelI { get; set; }
        //public string LastWinUserLevelI { get; set; }
        //public List<Cam_WinnerStringGin> LastWinnersLevelI { get; set; }
        //public List<Cam_WinnerStringGin> TopMonthlyWinnersLevelI { get; set; }
        //public List<Cam_WinnerStringGin> TopYearlyWinnersLevelI { get; set; }

        //public long CurrentLevelII { get; set; }
        //public long WinsLevelII { get; set; }
        //public decimal LargestWinLevelII { get; set; }
        //public string LargestWinDateLevelII { get; set; }
        //public string LargestWinUserLevelII { get; set; }
        //public decimal LastWinLevelII { get; set; }
        //public string LastWinDateLevelII { get; set; }
        //public string LastWinUserLevelII { get; set; }
        //public List<Cam_WinnerStringGin> LastWinnersLevelII { get; set; }
        //public List<Cam_WinnerStringGin> TopMonthlyWinnersLevelII { get; set; }
        //public List<Cam_WinnerStringGin> TopYearlyWinnersLevelII { get; set; }

        //public long CurrentLevelIII { get; set; }
        //public long WinsLevelIII { get; set; }
        //public decimal LargestWinLevelIII { get; set; }
        //public string LargestWinDateLevelIII { get; set; }
        //public string LargestWinUserLevelIII { get; set; }
        //public decimal LastWinLevelIII { get; set; }
        //public string LastWinDateLevelIII { get; set; }
        //public string LastWinUserLevelIII { get; set; }
        //public List<Cam_WinnerStringGin> LastWinnersLevelIII { get; set; }
        //public List<Cam_WinnerStringGin> TopMonthlyWinnersLevelIII { get; set; }
        //public List<Cam_WinnerStringGin> TopYearlyWinnersLevelIII { get; set; }

        //public long CurrentLevelIV { get; set; }
        //public long WinsLevelIV { get; set; }
        //public decimal LargestWinLevelIV { get; set; }
        //public string LargestWinDateLevelIV { get; set; }
        //public string LargestWinUserLevelIV { get; set; }
        //public decimal LastWinLevelIV { get; set; }
        //public string LastWinDateLevelIV { get; set; }
        //public string LastWinUserLevelIV { get; set; }
        //public List<Cam_WinnerStringGin> LastWinnersLevelIV { get; set; }
        //public List<Cam_WinnerStringGin> TopMonthlyWinnersLevelIV { get; set; }
        //public List<Cam_WinnerStringGin> TopYearlyWinnersLevelIV { get; set; }

        //public string Timestamp { get; set; }
        //public List<Cam_WinnerStringGin> LastWinners { get; set; }
        //public List<Cam_WinnerStringGin> TopWinners { get; set; }
        #endregion unnecesary pars
        // Sezione "jackpotCards" (stessa struttura del blocco principale)
        public Cam_JackpotCards JackpotCards { get; set; }

        // Sezione "cashBomb"
        public Cam_CashBomb CashBomb { get; set; }

        // Sezione "goldenCoinsLink"
        public Cam_GoldenCoinsLink GoldenCoinsLink { get; set; }
    }

    public class Cam_WinnerStringGin
    {
        public string WinUser { get; set; }
        public decimal WinAmount { get; set; }
        public string WinDate { get; set; }
        public string Gin { get; set; }      // nel blocco principale è string
        public string WinLevel { get; set; } // nel blocco principale è string
    }

    public class Cam_JackpotCards
    {
        public string Currency { get; set; }

        public long CurrentLevelI { get; set; }
        public long WinsLevelI { get; set; }
        public decimal LargestWinLevelI { get; set; }
        public string LargestWinDateLevelI { get; set; }
        public string LargestWinUserLevelI { get; set; }
        public decimal LastWinLevelI { get; set; }
        public string LastWinDateLevelI { get; set; }
        public string LastWinUserLevelI { get; set; }
        public List<Cam_WinnerStringGin> LastWinnersLevelI { get; set; }
        public List<Cam_WinnerStringGin> TopMonthlyWinnersLevelI { get; set; }
        public List<Cam_WinnerStringGin> TopYearlyWinnersLevelI { get; set; }

        public long CurrentLevelII { get; set; }
        public long WinsLevelII { get; set; }
        public decimal LargestWinLevelII { get; set; }
        public string LargestWinDateLevelII { get; set; }
        public string LargestWinUserLevelII { get; set; }
        public decimal LastWinLevelII { get; set; }
        public string LastWinDateLevelII { get; set; }
        public string LastWinUserLevelII { get; set; }
        public List<Cam_WinnerStringGin> LastWinnersLevelII { get; set; }
        public List<Cam_WinnerStringGin> TopMonthlyWinnersLevelII { get; set; }
        public List<Cam_WinnerStringGin> TopYearlyWinnersLevelII { get; set; }

        public long CurrentLevelIII { get; set; }
        public long WinsLevelIII { get; set; }
        public decimal LargestWinLevelIII { get; set; }
        public string LargestWinDateLevelIII { get; set; }
        public string LargestWinUserLevelIII { get; set; }
        public decimal LastWinLevelIII { get; set; }
        public string LastWinDateLevelIII { get; set; }
        public string LastWinUserLevelIII { get; set; }
        public List<Cam_WinnerStringGin> LastWinnersLevelIII { get; set; }
        public List<Cam_WinnerStringGin> TopMonthlyWinnersLevelIII { get; set; }
        public List<Cam_WinnerStringGin> TopYearlyWinnersLevelIII { get; set; }

        public long CurrentLevelIV { get; set; }
        public long WinsLevelIV { get; set; }
        public decimal LargestWinLevelIV { get; set; }
        public string LargestWinDateLevelIV { get; set; }
        public string LargestWinUserLevelIV { get; set; }
        public decimal LastWinLevelIV { get; set; }
        public string LastWinDateLevelIV { get; set; }
        public string LastWinUserLevelIV { get; set; }
        public List<Cam_WinnerStringGin> LastWinnersLevelIV { get; set; }
        public List<Cam_WinnerStringGin> TopMonthlyWinnersLevelIV { get; set; }
        public List<Cam_WinnerStringGin> TopYearlyWinnersLevelIV { get; set; }

        public string Timestamp { get; set; }
        public List<Cam_WinnerStringGin> LastWinners { get; set; }
        public List<Cam_WinnerStringGin> TopWinners { get; set; }
    }

    #region cashbomb

    public class Cam_CashBomb
    {
        public string Currency { get; set; }
        public long Level { get; set; }
        public decimal Balance { get; set; }
        public List<Cam_CashBombWin> Wins { get; set; }
        public Cam_CashBombPortalSettings PortalSettings { get; set; }
        public Cam_CashBombSchedule ActiveSchedule { get; set; }
        public string Timestamp { get; set; }
    }

    public class Cam_CashBombWin
    {
        public decimal TotalWin { get; set; }
        public List<Cam_CashBombWinner> Winners { get; set; }
        public long RelatedJackpotWinLevel { get; set; }
        public string WinDate { get; set; }
    }

    public class Cam_CashBombWinner
    {
        public string PlayerName { get; set; }
        public long GameIdentifierNumber { get; set; }
        public decimal WinAmount { get; set; }
    }

    public class Cam_CashBombPortalSettings
    {
        public List<long> DisabledGins { get; set; }
    }

    public class Cam_CashBombSchedule
    {
        public string Start { get; set; }
        public string End { get; set; }
    }

    #endregion cashbomb

    #region goldencoinslink

    public class Cam_GoldenCoinsLink
    {
        public List<Cam_GclCurrencyStats> CurrencyStatistics { get; set; }
        public string Timestamp { get; set; }
    }

    public class Cam_GclCurrencyStats
    {
        public string Currency { get; set; }
        public List<Cam_GclLevelStats> Levels { get; set; }
        public Cam_GclSchemaSettings SchemaSettings { get; set; }
        public List<Cam_GclWinner> LastWinners { get; set; }
        public List<Cam_GclWinner> TopWinners { get; set; }
    }

    public class Cam_GclLevelStats
    {
        public decimal Balance { get; set; }
        public long WinCount { get; set; }

        public Cam_GclWinInfo LargestWin { get; set; }
        public Cam_GclWinInfo LastWin { get; set; }

        public List<Cam_GclWinInfo> LastWinners { get; set; }
        public List<Cam_GclWinInfo> TopMonthlyWinners { get; set; }
        public List<Cam_GclWinInfo> TopYearlyWinners { get; set; }
    }

    public class Cam_GclWinInfo
    {
        public long Gin { get; set; }
        public decimal WinAmount { get; set; }
        public long WinLevel { get; set; }
        public string WinDate { get; set; }
        public string WinUser { get; set; }
    }

    public class Cam_GclWinner
    {
        public long Gin { get; set; }
        public decimal WinAmount { get; set; }
        public long WinLevel { get; set; }
        public string WinDate { get; set; }
        public string WinUser { get; set; }
    }

    public class Cam_GclSchemaSettings
    {
        public List<Cam_GclBetSector> BetSectors { get; set; }
        public decimal Coefficient { get; set; }
    }

    public class Cam_GclBetSector
    {
        public List<decimal> BetRanges { get; set; } // range di bet: uso decimal per sicurezza
        public decimal Mini { get; set; }
        public decimal Minor { get; set; }
    }
    #endregion goldencoinslink

    #endregion jackpot response

    #endregion core calls

}
