using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GamingTests.Net48.Casino.ExtInt;

namespace GamingTests.Net48.Casino.ExtInt.AM
{
    public sealed class CasinoExtIntAMSWCore : CasinoExtIntBase
    {
        #region Singleton and Static Members
        //keep from old implementation
        public static CasinoExtIntAMSWCore _def;
        public static bool defReload;

        public static CasinoExtIntAMSWCore def
        {
            get
            {
                if (_def == null || defReload)
                {
                    _def = new CasinoExtIntAMSWCore();
                    defReload = false;
                }
                return _def;
            }
        }

        #endregion

        #region helpers

        #region  helpers customization if needed
        private sealed class CasinoAMHelpers : WalletSharedHelpers
        {
            protected override string BuildMovementKey(string provider, string transferId)
            {
                return "AM::" + transferId;
            }
        }

        private sealed class CasinoAMPipelines : WalletPipelineBase
        {
            protected override string BuildMovementKey(string provider, string transferId)
            {
                return "AM::" + transferId;
            }
        }

        public CasinoExtIntAMSWCore() : base(new CasinoAMHelpers(), new CasinoAMPipelines())
        {
        }
        #endregion

        #region base helpers
        
        // public CasinoExtIntAMSWCore() : base()
        // {
        // }

        #endregion
        #endregion

        #region Caching Mechanisms
        //Keep from old implementation
        public static readonly ConcurrentDictionary<string, object> sessionInfosByTicketCache = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> sessionFastBalanceByTicketCache = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> authTokenCache = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> jackpotValueCache = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> gameDecodeCache = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> casinoGameCache = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> sessionFastJackpotByUserGame = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> monitorByTicketCache = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> committedByExtIdCache = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> stakeByRoundRef = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> refundByRoundRef = new ConcurrentDictionary<string, object>();
        public static readonly ConcurrentDictionary<string, object> openRoundsByRoundRefCache = new ConcurrentDictionary<string, object>();
        public static DateTime _cachedGamesExpireUtc = DateTime.MinValue;
        public static DateTime _cachedGamesAllExpireUtc = DateTime.MinValue;
        public static Dictionary<string, HashParams> _cachedGamesAll = new Dictionary<string, HashParams>();
        public static Dictionary<string, HashParams> _cachedGames = new Dictionary<string, HashParams>();

        #endregion

        public HashResult getAuxInfos(string auxInfo, HashParams auxPars)
        {
            var result = new HashResult { IsOk = true };
            var callPars = auxPars.getTypedValue("callPars", new HashParams(), false);

            switch ((auxInfo ?? string.Empty).ToLowerInvariant())
            {
                case "withdraw":
                    result["MSGRESULT"] = ExecuteBet(Map(callPars));
                    break;
                case "deposit":
                    result["MSGRESULT"] = ExecuteWin(Map(callPars));
                    break;
                case "rollback":
                    result["MSGRESULT"] = ExecuteCancel(Map(callPars));
                    break;
                default:
                    result.IsOk = false;
                    result.ErrorMessage = "INVALID ACTION:" + auxInfo;
                    break;
            }

            return result;
        }
    }
}
