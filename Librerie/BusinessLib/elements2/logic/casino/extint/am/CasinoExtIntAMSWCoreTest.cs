using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using businesslib2.elements2;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint;
using it.capecod.config;
using it.capecod.cryptography;
using it.capecod.data;
using it.capecod.fx4.lang;
using it.capecod.fx4.servicemodel;
using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.aams;
using it.capecod.gridgame.business.elements2.logic.account;
using it.capecod.gridgame.business.elements2.logic.account.ext;
using it.capecod.gridgame.business.elements2.logic.casino;
using it.capecod.gridgame.business.elements2.logic.casino.accounting;
using it.capecod.gridgame.business.elements2.logic.casino.extaccount;
using it.capecod.gridgame.business.elements2.logic.casino.extint.am.types;
using it.capecod.gridgame.business.elements2.logic.general;
using it.capecod.gridgame.business.elements2.model.table;
using it.capecod.gridgame.business.util.data;
using it.capecod.gridgame.business.util.ebroker;
using it.capecod.gridgame.business.util.session;
using it.capecod.inject;
using it.capecod.lang;
using it.capecod.log;
using it.capecod.random;
using it.capecod.servlet;
using it.capecod.system;
using it.capecod.threading;
using it.capecod.util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GamingTest.BusinessLib.elements2.logic.casino.extint.am
{
    public class CasinoExtIntAMSWCoreTest : CasinoExtIntFaceTest
    {
        #region Singleton and Static Members

        public static CasinoExtIntAMSWCoreTest _def;
        public static bool defReload;

        public static CasinoExtIntAMSWCoreTest def
        {
            get
            {
                if (_def == null || defReload)
                {
                    _def = (CasinoExtIntAMSWCoreTest)CCFactory.Get(typeof(CasinoExtIntAMSWCoreTest));
                    defReload = false;
                }
                return _def;
            }
        }

        public static readonly string ENANCHE_DEBUG_ENTITIES_CALLS = CcConfig.AppSettingStr(typeof(CasinoExtIntAMSWCoreTest), "ENANCHE_DEBUG_LOG_CALLS", "CasinoAM.Entities");
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static long _uniquer;
        private static readonly object _uniquerLock = new object();
        private static readonly SafeRandom _random = new SafeRandom();
        private static long _callId;
        private static SmartHash _methodInfos;
        public static bool ENANCHE_DEBUG = CcConfig.AppSettingBool(typeof(CasinoExtIntAMSWCoreTest), "ENANCHE_DEBUG", true);
        public static string ENANCHE_DEBUG_LOG = CcConfig.AppSettingStr(typeof(CasinoExtIntAMSWCoreTest), "ENANCHE_DEBUG_LOG", "CasinoAM", "ENANCHE_DEBUG_LOG");
        public static int ENANCHE_DEBUG_LEVEL = CcConfig.AppSettingInt(typeof(CasinoExtIntAMSWCoreTest), "ENANCHE_DEBUG_LEVEL", 1000);
        public static DateTime _cachedGamesExpireUtc;
        public static DateTime _cachedGamesAllExpireUtc;
        public static int Cam_MAX_WAIT_ON_MONITOR_SECS = CcConfig.AppSettingInt("CAM_MAX_WAIT_ON_MONITOR_SECS", 120);
        #endregion

        #region Caching Mechanisms

        public static readonly Cache _staticDataCache = new Cache(
            CcConfig.AppSettingInt("CASINOEXTINTAM_STAT_CACHE_DIM", 100),
            CcConfig.AppSettingInt("CASINOEXTINTAM_STAT_CACHE_EXPIRE", 10000)
        );

        public static readonly Cache _staticLongDataCache = new Cache(
            CcConfig.AppSettingInt("CASINOEXTINTAM_STATLONG_CACHE_DIM", 100),
            CcConfig.AppSettingInt("CASINOEXTINTAM_STATLONG_CACHE_EXPIRE", 900000)
        );
        public static readonly Cache _msgReserveCache = new Cache(
            CcConfig.AppSettingInt("CASINOEXTINTAM_RESERVE_CACHE_DIM", 10000), 
            CcConfig.AppSettingLong("CASINOEXTINTAM_RESERVE_CACHE_EXPIRE", 300000)
        );
        private static readonly Cache defenceCodeCache = new Cache(
            CcConfig.AppSettingInt("CASINOAM_DEFENCE_CODE_CACHE_DIM", 1000),
            CcConfig.AppSettingInt("CASINOAM_DEFENCE_CODE_TTL_SECONDS", 120) * 1000
        );

        public static readonly Cache sessionInfosByTicketCache = new Cache(1000, 3600L * 1000);
        public static readonly Cache sessionFastBalanceByTicketCache = new Cache(1000, 3600L * 1000);
        public static readonly Cache authTokenCache = new Cache(1000, 3600L * 1000);
        public static Cache jackpotValueCache = new Cache(CcConfig.AppSettingInt("CASINOAM_JKPVALUE_CACHE_DIM", 10), CcConfig.AppSettingLong("CASINOAM_JKPVALUE_CACHE_EXPIRE", 30000));
        public static Cache gameDecodeCache = new Cache(100, 120000);
        public static Cache casinoGameCache = new Cache(100, 10000);
        public static Cache sessionFastJackpotByUserGame = new Cache(1000, 3600L * 1000);
        public static Cache monitorByTicketCache = new Cache(1000, 300L * 1000);
        public static Cache committedByExtIdCache = new Cache(10000, 300L * 1000);
        public static Cache stakeByRoundRef = new Cache(10000, 300L * 1000);
        public static Cache refundByRoundRef = new Cache(10000, 300L * 1000);
        public static Cache openRoundsByRoundRefCache = new Cache(1000, 300L * 1000); 
        public static Dictionary<string, Hashtable> _cachedGamesAll;

        public static Dictionary<string, Hashtable> _cachedGames;

        #endregion

        #region Main Dispatcher

        public override HashResult getAuxInfos(string auxInfo, HashParams auxPars)
        {
            HashResult result = new HashResult();
            string reserveCacheToken = string.Empty;

            switch (auxInfo.ToLower())
            {
                case "authenticate":
                case "withdraw":
                case "deposit":
                case "withdrawanddeposit":
                case "awarddeposit":
                    auxInfo = auxInfo.ToLower();
                    reserveCacheToken = string.Format("EU_{0}", auxPars.getTypedValue("euId", -1, false));
                    break;
                case "getgames":
                case "getjackpotvalues":
                case "hashcodes":
                case "getfastbalance":
                case "swext_getfastbalance":
                case "swext_refreshbalance":
                case "swext_setfastbalance":
                case "swext_getsessioninfos":
                case "swext_clearmatchcache":
                    auxInfo = auxInfo.ToLower();
                    break;
            }

            bool useReservedFlow = CcConfig.AppSettingBool("CASINOAM_RESERVE_CACHE_ENABLE", false) && reserveCacheToken.Length > 0;
            using (useReservedFlow ? (IDisposable)new SmartReserve(reserveCacheToken, _msgReserveCache, SmartHash.byPP("MAX_WAIT_MILLIS", _msgReserveCache.ExpireTimeout)) : new DisposableEmpty())
            {
                switch (auxInfo)
                {
                    case "authenticate":
                        result = HandleAuthenticate(auxPars);
                        break;
                    case "withdraw":
                        result = HandleWithdraw(auxPars);
                        break;
                    case "deposit":
                        result = HandleDeposit(auxPars);
                        break;
                    case "withdrawanddeposit":
                        result = HandleWithdrawAndDeposit(auxPars);
                        break;
                    case "awarddeposit":
                        result = HandleAwardDeposit(auxPars);
                        break;
                    case "hashcodes":
                        result = receiveCheckSums();
                        break;
                    case "getgames":
                        result = HandleGetGames(auxPars);
                        break;
                    case "getjackpotvalues":
                        result = HandleGetJackpotValues(auxPars);
                        break;
                    case "getfastbalance":
                    case "swext_getfastbalance":
                        result = HandleGetFastBalance(auxPars);
                        break;
                    case "swext_refreshbalance":
                        result = HandleRefreshBalance(auxPars);
                        break;
                    case "swext_setfastbalance":
                        result = HandleSetFastBalance(auxPars);
                        break;
                    case "swext_getsessioninfos":
                        result = HandleGetSessionInfos(auxPars);
                        break;
                    case "swext_clearmatchcache":
                        _clearOpenDebitsCache("SWEXT_ClearMatchCache", auxPars.getTypedValue<HashParams>("sessionInfos", null, false));
                        result.IsOk = true;
                        break;
                    default:
                        result.ErrorMessage = "INVALID ACTION:" + auxInfo;
                        break;
                }
            }
            return result;
        }

        #endregion

        #region Action Handlers (called from getAuxInfos)

        public virtual HashResult HandleAuthenticate(HashParams auxPars)
        {
            HashResult result = new HashResult(){ IsOk = true };
            HashParams callPars = auxPars.getTypedValue<HashParams>("callPars", null, false);
            result["MSGRESULT"] = _CasinoAM_Auth(auxPars.getTypedValue("euId", -1, false), callPars);
            return result;
        }

        public virtual HashResult HandleWithdraw(HashParams auxPars)
        {
            HashResult result = new HashResult(){ IsOk = true };
            HashParams callPars = auxPars.getTypedValue<HashParams>("callPars", null, false);
            result["MSGRESULT"] = _CasinoAM_Withdraw(auxPars.getTypedValue("euId", -1, false), callPars);
            return result;
        }

        public virtual HashResult HandleDeposit(HashParams auxPars)
        {
            HashResult result = new HashResult() { IsOk = true };
            HashParams callPars = auxPars.getTypedValue<HashParams>("callPars", null, false);
            string reason = callPars.getTypedValue("reason", "").ToLowerInvariant();

            //FIX JACKPOT WIN --> ADMIRAL APPROVA ROUND SEMPRE APERTI, JACKPOT WIN CHIUDIAMO IN QUANTO è UNICO CASO IN CUI NON CI SI ASPETTANO ALTRI MOVIMENTI
            if (reason.Contains("cancel"))
            {
                result["MSGRESULT"] = _CasinoAM_RollBack(auxPars.getTypedValue("euId", -1, false), callPars);
            }
            else if (reason.Contains("end"))
            {
                callPars.Add("forceRoundClose", true);
                result["MSGRESULT"] = _CasinoAM_Deposit(auxPars.getTypedValue("euId", -1, false), callPars);
            }
            else
            {
                result["MSGRESULT"] = _CasinoAM_Deposit(auxPars.getTypedValue("euId", -1, false), callPars);
            }

            return result;
        }

        public virtual HashResult HandleWithdrawAndDeposit(HashParams auxPars)
        {
            var result = new HashResult() { IsOk = true };
            var callPars = auxPars.getTypedValue<HashParams>("callPars", null, false);

            var transferId = callPars["transferId"];

            #region Prepare CallPars
            Cam_WithdrawResponse withResp;
            HashParams withCallPars = new HashParams(callPars);
            withCallPars["transferId"] = transferId + "_B";

            // DEPOSIT
            Cam_DepositResponse depoResp;
            HashParams depoCallPars = new HashParams(callPars);
            depoCallPars["amount"] = callPars["winAmount"];
            depoCallPars["transferId"] = transferId + "_W"; 
            depoCallPars["forceRoundClose"] = true;

            

            #endregion Prepare CallPars
            //NO NEED TO STOP DEPOSIT BEACUSE IF WITH FAILS DEPOSIT DOESN'T HAVE A VALID RELATED
            result["MSGRESULT_B"] = withResp = _CasinoAM_Withdraw(auxPars.getTypedValue("euId", -1, false), withCallPars);
            
            result["MSGRESULT_W"] = depoResp = _CasinoAM_Deposit(auxPars.getTypedValue("euId", -1, false), depoCallPars);
                
            // CREA LA RESP PER BETWIN
            result["MSGRESULT"] = new Cam_WithdrawDepositResponse
            {
                balance = withResp.responseCodeReason != "200" ? withResp.balance : depoResp.balance,
                casinoTransferId = withResp.casinoTransferId,
                totalBet = withResp.responseCodeReason != "200" ? withResp.totalBet : depoResp.totalBet,
                totalWin = withResp.responseCodeReason != "200" ? withResp.totalWin : depoResp.totalWin,
                errorMessage = withResp.responseCodeReason != "200" ? withResp.errorMessage : depoResp.errorMessage,
                responseCodeReason = withResp.responseCodeReason != "200" ? withResp.responseCodeReason : depoResp.responseCodeReason,
            };
                
            return result;
        }

        //TODO: implementare
        public virtual HashResult HandleAwardDeposit(HashParams auxPars)
        {
            HashResult result = new HashResult();
            result.IsOk = true;
            HashParams callPars = auxPars.getTypedValue<HashParams>("callPars", null, false);
            //result["MSGRESULT"] = _CasinoAM_SpecialWin(auxPars.getTypedValue("euId", -1, false), callPars);
            return result;
        }

        public virtual HashResult HandleGetGames(HashParams auxPars)
        {
            HashResult result = new HashResult();
            List<SmartHash> amGameList = getGames(auxPars);
            result.IsOk = amGameList.Count > 0;
            result["GameList"] = amGameList;
            if (!result.IsOk)
                result.ErrorMessage = "GetGames:empty list";
            return result;
        }

        public virtual HashResult HandleGetJackpotValues(HashParams auxPars)
        {
            HashResult result = new HashResult();
            string key = "";
            if (auxPars.ContainsKey("callPars"))
            {
                key = auxPars.getTypedValue<HashParams>("callPars", (HashParams)null, false).getTypedValue("key", "", false);
                auxPars.Add("extKey", auxPars.getTypedValue<HashParams>("callPars", (HashParams)null, false).getTypedValue("extKey", "", false));
            }
            else
            {
                key = auxPars.getTypedValue("key", "", false);
            }

            List<SmartHash> jackpotValues = getJackpotValues(key, auxPars).get("jackpotValues", (List<SmartHash>)null);
            result.IsOk = true;
            result["Jackpots"] = jackpotValues;
            return result;
        }

        public virtual HashResult HandleGetFastBalance(HashParams auxPars)
        {
            HashResult result = new HashResult();
            result.IsOk = true;
            result["balance"] = _getFastSessionBalancesByTicket(
                auxPars.getTypedValue("ticket", string.Empty, false),
                auxPars.getTypedValue<HashParams>("sessionInfos", null, false)).amountTotal;
            return result;
        }

        private HashResult HandleRefreshBalance(HashParams auxPars)
        {
            HashResult result = new HashResult();
            int fstMatchId = auxPars.getTypedValue("fstMatchId", -1, false);
            EndUser local_eu = auxPars.getTypedValue<EndUser>("localEu", null, false);
            EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), useNoLockWhePossible = true };
            using (new GGConnMgr.CentralCtxWrap("CASINO"))
            {
                centEu.getByUserId();
            }

            bool funSession = false;
            bool freeRoundSession = false;
            string ticket = string.Empty;
            string gameKey = string.Empty;
            using (new GGConnMgr.CentralCtxWrap("CASINO"))
            {
                CasinoSubscription fstSub = new CasinoSubscription { MATCH_Id = fstMatchId, useNoLockWhenPossible = true };
                fstSub.GetById();
                if (fstSub.flagRecordFound)
                {
                    CasinoSession session = fstSub.referenceSession;
                    funSession = session.CSES_PLAYBONUS_TABLE > 0;
                    freeRoundSession = funSession && fstSub.MATCH_FR_START > 0;
                    ticket = fstSub.MATCH_PGA_TICKET;
                    gameKey = session.ReferenceCasinoTableDef.getReferenceGame(true).GAME_KEY;
                }
            }

            if (fstMatchId >= 0)
            {
                SessionBalancesHolder counters = _getFastSessionBalancesByTicket(
                    ticket,
                    new HashParams(
                        "EU_UID", local_eu.EU_UID,
                        "MATCH_Id", fstMatchId,
                        "centEU_Id", centEu.EU_ID,
                        "funSession", funSession,
                        "freeRoundSession", freeRoundSession,
                        "GAME_KEY", gameKey
                    ),
                    new HashParams(
                        "reload", true,
                        "skipCacheWrite", true
                    )
                );
                result.IsOk = true;
                result["amount"] = funSession ? counters.funBonus : counters.amountTotal;
            }
            return result;
        }

        public virtual HashResult HandleSetFastBalance(HashParams auxPars)
        {
            HashResult result = new HashResult();
            SessionBalancesHolder balHolder = _getFastSessionBalancesByTicket(
                auxPars.getTypedValue("ticket", string.Empty, false),
                auxPars.getTypedValue<HashParams>("sessionInfos", null, false));

            balHolder.amountTotal = auxPars.getTypedValue("newBalance", -1L, false);
            if (auxPars.getTypedValue("funBonusSession", false))
                balHolder.funBonus = auxPars.getTypedValue("newBalance", -1, false);

            _setFastSessionBalancesByTicket(
                auxPars.getTypedValue("ticket", string.Empty, false),
                balHolder,
                auxPars.getTypedValue<HashParams>("sessionInfos", null, false));
            result.IsOk = true;
            return result;
        }

        public virtual HashResult HandleGetSessionInfos(HashParams auxPars)
        {
            HashResult result = new HashResult();
            result["sessionInfos"] = _getSessionInfosByTicket(
                auxPars.getTypedValue("ticket", string.Empty, false),
                auxPars.getTypedValue<EndUser>("localEu", null, false),
                auxPars.getTypedValue("idTransazione", "notran"),
                _MethNames.Cam_AuxInfos,
                auxPars
            );
            result.IsOk = true;
            return result;
        }

        #endregion

        #region Core Business Logic

        #region Authentication

        public virtual Cam_AuthResponse _CasinoAM_Auth(int euId, HashParams auxPars)
        {
            var dbg = Log.getLogger("GiovanniDebug");

            try
            {
                if (auxPars == null)
                    throw new ArgumentNullException("auxPars");

                string portalCode = auxPars.getTypedValue("portalCode", "", false);
                string playerId = auxPars.getTypedValue("playerId", "", false);
                string sessionId = auxPars.getTypedValue("sessionId", "", false);
                string gameId = auxPars.getTypedValue("gameId", "", false);

                string defenceCode = auxPars.getTypedValue("defenceCode", string.Empty);
                string authenticationToken = auxPars.getTypedValue("authenticationToken", string.Empty);

                //dbg.BetaDebug($"[AM_AUTH] IN euId={euId} portalCode={portalCode} playerId={playerId} sessionId={sessionId} gameId={gameId} " +
                              //$"hasDefenceCode={!string.IsNullOrEmpty(defenceCode)} hasAuthToken={!string.IsNullOrEmpty(authenticationToken)}");

                if (!string.IsNullOrEmpty(defenceCode))
                {
                    //dbg.BetaDebug("[AM_AUTH] Branch=DEFENCE_CODE (initial launch)");
                    return _CasinoAM_AuthByDefenceCode(euId, auxPars);
                }

                if (!string.IsNullOrEmpty(authenticationToken))
                {
                    //dbg.BetaDebug("[AM_AUTH] Branch=AUTH_TOKEN (relaunch -> close+open always)");
                    return _CasinoAM_AuthByAuthenticationToken(euId, auxPars);
                }

                //dbg.BetaDebug("[AM_AUTH] Missing both defenceCode and authenticationToken -> 401");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized: missing defenceCode/authenticationToken"
                };
            }
            catch (Exception ex)
            {
                Log.getLogger("CasinoAM").Warn("CasinoAM_Auth error: {0}", ex);
                //dbg.BetaDebug("[AM_AUTH] EXCEPTION -> 401 (see CasinoAM log)");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized"
                };
            }
        }

        private Cam_AuthResponse _CasinoAM_AuthByDefenceCode(int euId, HashParams auxPars)
        {
            var dbg = Log.getLogger("GiovanniDebug");

            string defenceCode = auxPars.getTypedValue("defenceCode", string.Empty);
            string playerId = auxPars.getTypedValue("playerId", "", false);

            //dbg.BetaDebug($"[AM_AUTH_DEF] IN euId={euId} playerId={playerId} defenceCodeLen={(defenceCode ?? "").Length}");

            if (string.IsNullOrEmpty(defenceCode) || string.IsNullOrEmpty(playerId))
            {
                //dbg.BetaDebug("[AM_AUTH_DEF] Missing defenceCode/playerId -> 401");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized: missing defenceCode/playerId"
                };
            }

            string cachekey = defenceCode + "X" + playerId;

            string token = string.Empty;
            try
            {
                int rem = defenceCodeCache.remove(cachekey);
                //dbg.BetaDebug($"[AM_AUTH_DEF] defenceCodeCache.remove(cachekey)={rem} (0=OK/consumed)");

                token = rem == 0
                    ? StringMgr.def.DecryptString(defenceCode).Split('|')[0]
                    : string.Empty;

                //dbg.BetaDebug($"[AM_AUTH_DEF] tokenDecoded={(string.IsNullOrEmpty(token) ? "NO" : "YES")} tokenLen={(token ?? "").Length}");
            }
            catch (Exception ex)
            {
                Log.getLogger("CasinoAM").Warn("CasinoAM_AuthByDefenceCode decrypt error: {0}", ex);
                //dbg.BetaDebug("[AM_AUTH_DEF] EXCEPTION while decoding defenceCode -> token empty");
                token = string.Empty;
            }

            if (string.IsNullOrEmpty(token))
            {
                //dbg.BetaDebug("[AM_AUTH_DEF] Empty token -> 401");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized"
                };
            }

            auxPars["user_token"] = token;
            //dbg.BetaDebug("[AM_AUTH_DEF] Calling _CasinoAM_CheckToken()");
            return _CasinoAM_CheckToken(euId, auxPars);
        }

        private Cam_AuthResponse _CasinoAM_AuthByAuthenticationToken(int euId, HashParams auxPars)
        {
            var dbg = Log.getLogger("GiovanniDebug");

            string authTok = auxPars.getTypedValue("authenticationToken", string.Empty);
            //dbg.BetaDebug($"[AM_AUTH_TOK] IN euId={euId} authTokenLen={(authTok ?? "").Length}");

            if (string.IsNullOrEmpty(authTok))
            {
                //dbg.BetaDebug("[AM_AUTH_TOK] Missing authenticationToken -> 401");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized: missing authenticationToken"
                };
            }

            string token;
            try
            {
                token = StringMgr.def.DecryptString(authTok);
                //dbg.BetaDebug($"[AM_AUTH_TOK] tokenDecoded={(string.IsNullOrEmpty(token) ? "NO" : "YES")} tokenLen={(token ?? "").Length}");
            }
            catch (Exception ex)
            {
                Log.getLogger("CasinoAM").Warn("CasinoAM_AuthByAuthenticationToken decrypt error: {0}", ex);
                //dbg.BetaDebug("[AM_AUTH_TOK] EXCEPTION while decoding authenticationToken -> 401");
                token = string.Empty;
            }

            if (string.IsNullOrEmpty(token))
            {
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized"
                };
            }

            string[] tp = (token ?? "").Split('@');
            if (tp.Length < 4 || string.IsNullOrEmpty(tp[1]) || string.IsNullOrEmpty(tp[3]))
            {
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(), errorMessage = "Unauthorized" };
            }

            string tokUid = tp[1];
            int tokMatchId;
            if (!int.TryParse(tp[3], out tokMatchId))
            {
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(), errorMessage = "Unauthorized" };
            }

            EndUser local_eu = new EndUser { EU_UID = tokUid, unl = true };
            local_eu.getByUserId();
            //checkEU vero e token coerente
            if (local_eu.EU_ID == -1 || local_eu.EU_ID != euId)
            {
                //dbg.BetaDebug("[AM_AUTH_TOK] Invalid token (EU mismatch) -> 403");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(),
                    errorMessage = "Invalid token"
                };
            }

            HashParams sessionInfos = _getCurrentSessionInfo(local_eu.EU_ID, null);
            //check eu attivo
            if (sessionInfos == null || sessionInfos.Count <= 0)
            {
                //dbg.BetaDebug("[AM_AUTH_TOK] Expired token / no current session -> 403");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(),
                    errorMessage = "Expired token"
                };
            }

            CasinoSubscription currSub = (CasinoSubscription)sessionInfos["sub"];
            //check token coerente con match corrente
            if(currSub == null || currSub.MATCH_Id != tokMatchId)
            {
                //dbg.BetaDebug("[AM_AUTH_TOK] Expired token / no current session (no sub) -> 403");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(),
                    errorMessage = "Expired token"
                };
            }

            auxPars["user_token"] = token;

            //dbg.BetaDebug("[AM_AUTH_TOK] Calling _CasinoAM_Reauth_CloseAndOpen() (close+open always)");
            return _CasinoAM_Reauth_CloseAndOpen(euId, auxPars);
        }

        public virtual Cam_AuthResponse _CasinoAM_CheckToken(int euId, HashParams auxPars)
        {
            var dbg = Log.getLogger("GiovanniDebug");

            string token = auxPars.getTypedValue("user_token", "", false);
            string playerId = auxPars.getTypedValue("playerId", "", false);
            string sessionId = auxPars.getTypedValue("sessionId", "", false);

            //dbg.BetaDebug($"[AM_CHECK] IN euId={euId} playerId={playerId} sessionId={sessionId} tokenPresent={!string.IsNullOrEmpty(token)} tokenLen={(token ?? "").Length}");

            string[] p = (playerId ?? "").Split('@');
            if (p.Length < 2 || string.IsNullOrEmpty(p[1]))
            {
                //dbg.BetaDebug("[AM_CHECK] Invalid playerId format -> 401");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized: invalid playerId"
                };
            }
            string euUid = p[1];

            if (string.IsNullOrEmpty(token))
            {
                //dbg.BetaDebug("[AM_CHECK] Empty token -> 403");
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(), errorMessage = "Empty token" };
            }

            EndUser local_eu = new EndUser { EU_UID = euUid, unl = true };
            local_eu.getByUserId();

            //dbg.BetaDebug($"[AM_CHECK] local_eu.EU_ID={local_eu.EU_ID} expectedEuId={euId}");

            if (local_eu.EU_ID == -1 || local_eu.EU_ID != euId)
            {
                //dbg.BetaDebug("[AM_CHECK] Invalid token (EU mismatch) -> 403");
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(), errorMessage = "Invalid token" };
            }

            HashParams currSessInfos = _getCurrentSessionInfo(local_eu.EU_ID, null);
            //dbg.BetaDebug($"[AM_CHECK] _getCurrentSessionInfo count={(currSessInfos == null ? -1 : currSessInfos.Count)}");

            if (currSessInfos == null || currSessInfos.Count <= 0)
            {
                //dbg.BetaDebug("[AM_CHECK] Expired token / no current session -> 403");
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(), errorMessage = "Expired token" };
            }

            CasinoSubscription currentMatch = currSessInfos.getTypedValue<CasinoSubscription>("sub", null);
            Games game = currSessInfos.getTypedValue<Games>("game", null);
            Games locGame = _getActualGameByGameEiKey(local_eu, game.GAME_EI_KEY);

            if (currentMatch == null || game == null)
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(), errorMessage = "Expired token" };

            using (new GGConnMgr.AlternateCtxWrap("CENTRAL"))
            {
                if (currentMatch.referenceSession == null)
                    currentMatch.referenceSession = currentMatch.referenceSession; // se lazy-load serve solo forzare contesto; altrimenti check basta
            }
            if (currentMatch.referenceSession == null)
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(), errorMessage = "Expired token" };


            bool funSession = currentMatch.referenceSession.CSES_PLAYBONUS_TABLE == 1;

            //dbg.BetaDebug($"[AM_CHECK] matchId={currentMatch.MATCH_Id} gameEiKey={game.GAME_EI_KEY} locGameKey={locGame.GAME_KEY} funSession={funSession}");

            HashParams sessionInfos = new HashParams
            {
                { "MATCH_Id", currentMatch.MATCH_Id },
                { "centEU_Id", local_eu.EU_ID },
                { "GAME_KEY", locGame.GAME_KEY },
                { "funSession", funSession },
                { "freeRoundSession", false }
            };

            SessionBalancesHolder sessBalHolder = _getFastSessionBalancesByTicket(currentMatch.MATCH_PGA_TICKET, sessionInfos);
            long saldoCents = sessBalHolder.amountTotal;

            //dbg.BetaDebug($"[AM_CHECK] saldoCents={saldoCents} ticket={currentMatch.MATCH_PGA_TICKET}");

            SmartHash tokenPars = SmartHash.byPP("playType", funSession ? "FUN" : "CASH", "matchId", currentMatch.MATCH_Id);
            SmartHash tokens = _Encode_ExtToken(local_eu, tokenPars);
            string newToken = tokens.get<string>("TOKEN", "", false);
            string cleanToken = tokens.get<string>("CLEAN_TOKEN", "", false);

            HashResult upRes = _CasinoAM_UpdateUserTokenOnDb(local_eu, cleanToken, tokenPars);
            if (!upRes.IsOk) return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString(), errorMessage = "internal server error" }; ;

            var result = new Cam_AuthResponse
            {
                balance = saldoCents,
                authenticationToken = newToken,
                errorMessage = "",
                responseCodeReason = ((int)Cam_ResponseStatus.RS_200_Success).ToString()
            };

            if (!string.IsNullOrEmpty(sessionId))
            {
                using (new GGConnMgr.AlternateCtxWrap("CENTRAL"))
                {
                    currentMatch.IsUpdatable = true;
                    currentMatch.MATCH_EXTCASINO_SESS2 = sessionId;
                    currentMatch.Update();
                }
                //dbg.BetaDebug($"[AM_CHECK] Saved MATCH_EXTCASINO_SESS2={sessionId} on matchId={currentMatch.MATCH_Id}");
            }

            //dbg.BetaDebug("[AM_CHECK] OUT 200");
            return result;
        }

        private Cam_AuthResponse _CasinoAM_Reauth_CloseAndOpen(int euId, HashParams auxPars)
        {
            var dbg = Log.getLogger("GiovanniDebug");

            string token = auxPars.getTypedValue("user_token", "", false);
            string playerId = auxPars.getTypedValue("playerId", "", false);
            string gameId = auxPars.getTypedValue("gameId", "", false);
            string sessionId = auxPars.getTypedValue("sessionId", "", false);

            //dbg.BetaDebug($"[AM_REAUTH] IN euId={euId} playerId={playerId} gameId={gameId} sessionId={sessionId} tokenPresent={!string.IsNullOrEmpty(token)} tokenLen={(token ?? "").Length}");

            if (string.IsNullOrEmpty(token))
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(), errorMessage = "Unauthorized: empty token" };

            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(sessionId))
            {
                //dbg.BetaDebug("[AM_REAUTH] Missing playerId/gameId/sessionId -> 401");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized: missing playerId/gameId/sessionId"
                };
            }

            string[] p = (playerId ?? "").Split('@');
            if (p.Length < 2 || string.IsNullOrEmpty(p[1]))
            {
                //dbg.BetaDebug("[AM_REAUTH] Invalid playerId format -> 401");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_401_UnauthorizedAccess).ToString(),
                    errorMessage = "Unauthorized: invalid playerId"
                };
            }
            string euUid = p[1];

            EndUser local_eu = new EndUser { EU_UID = euUid, unl = true };
            local_eu.getByUserId();

            //dbg.BetaDebug($"[AM_REAUTH] local_eu.EU_ID={local_eu.EU_ID} expectedEuId={euId}");

            if (local_eu.EU_ID == -1 || local_eu.EU_ID != euId)
            {
                //dbg.BetaDebug("[AM_REAUTH] Invalid token (EU mismatch) -> 403");
                return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(), errorMessage = "Invalid token" };
            }

            Games requestedGame;
            try
            {
                requestedGame = _getActualGameByGameEiKey(local_eu, gameId);
                //dbg.BetaDebug($"[AM_REAUTH] requestedGameKey={requestedGame.GAME_KEY} (from gameId={gameId})");
            }
            catch (Exception ex)
            {
                Log.getLogger("CasinoAM").Warn("CasinoAM_Reauth invalid gameId={0}: {1}", gameId, ex);
                //dbg.BetaDebug($"[AM_REAUTH] Invalid gameId={gameId} -> 403");
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_403_Forbidden).ToString(),
                    errorMessage = "Invalid game"
                };
            }

            HashParams currSessInfos = _getCurrentSessionInfo(local_eu.EU_ID, null);
            //dbg.BetaDebug($"[AM_REAUTH] _getCurrentSessionInfo count={(currSessInfos == null ? -1 : currSessInfos.Count)}");

            long saldoCents = 0;
            bool funSession = false;

            CasinoSession referenceSession = null;
            CasinoSubscription currentMatch = null;
            Games currentGame = null;

            if (currSessInfos != null && currSessInfos.Count > 0)
            {
                currentMatch = currSessInfos.getTypedValue<CasinoSubscription>("sub", null);
                Games game = currSessInfos.getTypedValue<Games>("game", null);
                using (new GGConnMgr.AlternateCtxWrap("CENTRAL"))
                {
                    referenceSession = currentMatch.referenceSession;
                }

                currentGame = _getActualGameByGameEiKey(local_eu, game.GAME_EI_KEY);
                funSession = referenceSession.CSES_PLAYBONUS_TABLE == 1;

                HashParams sessionInfos = new HashParams
                {
                    { "MATCH_Id", currentMatch.MATCH_Id },
                    { "centEU_Id", local_eu.EU_ID },
                    { "GAME_KEY", currentGame.GAME_KEY },
                    { "funSession", funSession },
                    { "freeRoundSession", false }
                };

                SessionBalancesHolder sessBalHolder = _getFastSessionBalancesByTicket(currentMatch.MATCH_PGA_TICKET, sessionInfos);
                saldoCents = sessBalHolder.amountTotal;

                //dbg.BetaDebug($"[AM_REAUTH] CurrentSession matchId={currentMatch.MATCH_Id} currentGameKey={currentGame.GAME_KEY} funSession={funSession} saldoCents={saldoCents}");
            }
            else
            {
                //dbg.BetaDebug("[AM_REAUTH] No current session found -> will OPEN anyway (no CLOSE)");
            }

            EndUser localEuById = new EndUser { EU_ID = euId, unl = true };
            localEuById.GetById();

            // 1) Close SEMPRE se ho una sessione corrente
            if (currentMatch != null && currentGame != null)
            {
                string closeTxId = "AUTHCLOSE_" + currentMatch.MATCH_Id + "_" + sessionId;
                //dbg.BetaDebug($"[AM_REAUTH] CLOSE start txId={closeTxId} matchId={currentMatch.MATCH_Id} currentGameKey={currentGame.GAME_KEY}");

                SmartHash closeRes = _CASINOAM_CloseSession(localEuById, new HashParams(
                    "locGame", currentGame,
                    "fstMatchId", currentMatch.MATCH_Id,
                    "transactionId", closeTxId
                ));

                //dbg.BetaDebug($"[AM_REAUTH] CLOSE result ok={closeRes.IsOk} err={(closeRes.IsOk ? "" : closeRes.ErrorMessage)}");

                if (!closeRes.IsOk)
                {
                    return new Cam_AuthResponse
                    {
                        responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString(),
                        errorMessage = "Unable to close previous session: " + closeRes.ErrorMessage
                    };
                }
            }

            // 2) Open SEMPRE sul gioco richiesto
            bool reqFun = (playerId ?? "").EndsWith("@FUN", StringComparison.OrdinalIgnoreCase);
            bool reqCash = (playerId ?? "").EndsWith("@CASH", StringComparison.OrdinalIgnoreCase);
            bool requestedFunSession = reqFun; // fallback

            // Se vuoi essere rigidissimo:
            if (!reqFun && !reqCash)
                throw new Exception("Unauthorized: invalid playType");

            long chosenBalLimit = CcConfig.AppSettingInt("CASINOAM_AUTHENTICATE_LIVELOBBY_FULLBALANCE_LIMIT", 100000);
            if (saldoCents > chosenBalLimit)
                saldoCents = chosenBalLimit;

            decimal sessionBalance = saldoCents / 100.0m; // saldoCents è in centesimi
            string openTxId = "AUTHOPEN_" + sessionId;

            //dbg.BetaDebug($"[AM_REAUTH] OPEN start txId={openTxId} requestedGameKey={requestedGame.GAME_KEY} sessionBalance={sessionBalance} (from saldoCents={saldoCents}) extSessionId={sessionId}");

            SmartHash openRes = _CASINOAM_OpenSession(localEuById, new HashParams(
                "locGame", requestedGame,
                "transactionId", openTxId,
                "isFunBonus", funSession,
                "sessionBalance", sessionBalance,
                "CasinoSessionId", sessionId,
                "rawRequest", auxPars.getTypedValue("rawRequest", "", false)
            ));

            //dbg.BetaDebug($"[AM_REAUTH] OPEN result ok={openRes.IsOk} err={(openRes.IsOk ? "" : openRes.ErrorMessage)}");

            if (!openRes.IsOk)
            {
                return new Cam_AuthResponse
                {
                    responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString(),
                    errorMessage = "Unable to open new session: " + openRes.ErrorMessage
                };
            }
            SmartHash tokenPars = new SmartHash();
            // 3) Refresh + update MATCH_EXTCASINO_SESS2 sul match post-open
            try
            {
                HashParams newSessInfos = _getCurrentSessionInfo(local_eu.EU_ID, null);
                CasinoSubscription newSub = (newSessInfos == null) ? null : newSessInfos.getTypedValue<CasinoSubscription>("sub", null);
                if (newSub == null)
                {
                    return new Cam_AuthResponse
                    {
                        responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString(),
                        errorMessage = "Unable to refresh session after open"
                    };
                }

                tokenPars = SmartHash.byPP("playType", requestedFunSession ? "FUN" : "CASH", "matchId", newSub.MATCH_Id);
                //dbg.BetaDebug($"[AM_REAUTH] Refresh _getCurrentSessionInfo count={(newSessInfos == null ? -1 : newSessInfos.Count)}");

                if (newSessInfos != null && newSessInfos.Count > 0)
                {
                    CasinoSubscription newMatch = newSessInfos.getTypedValue<CasinoSubscription>("sub", null);
                    if (newMatch != null)
                    {
                        using (new GGConnMgr.AlternateCtxWrap("CENTRAL"))
                        {
                            newMatch.IsUpdatable = true;
                            newMatch.MATCH_EXTCASINO_SESS2 = sessionId;
                            newMatch.Update();
                        }
                        //dbg.BetaDebug($"[AM_REAUTH] Saved MATCH_EXTCASINO_SESS2={sessionId} on NEW matchId={newMatch.MATCH_Id}");
                    }
                }
                

            }
            catch (Exception ex)
            {
                Log.getLogger("CasinoAM").Warn("CasinoAM_Reauth unable to update MATCH_EXTCASINO_SESS2: {0}", ex);
                //dbg.BetaDebug("[AM_REAUTH] WARN: unable to update MATCH_EXTCASINO_SESS2 (see CasinoAM log)");
            }

            SmartHash tokens = _Encode_ExtToken(local_eu, tokenPars);
            string newToken = tokens.get<string>("TOKEN", "", false);
            string cleanToken = tokens.get<string>("CLEAN_TOKEN", "", false);

            HashResult upRes = _CasinoAM_UpdateUserTokenOnDb(local_eu, cleanToken, tokenPars);
            if (!upRes.IsOk) return new Cam_AuthResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString(), errorMessage = "internal server error" }; ;

            //dbg.BetaDebug("[AM_REAUTH] OUT 200");
            return new Cam_AuthResponse
            {
                balance = saldoCents,
                authenticationToken = newToken,
                errorMessage = "",
                responseCodeReason = ((int)Cam_ResponseStatus.RS_200_Success).ToString()
            };
        }

        #endregion Authentication

        #region withdraw
        public virtual Cam_WithdrawResponse _CasinoAM_Withdraw(int euId, HashParams auxPars)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            const _MethNames methName = _MethNames.Cam_Withdraw;
            DateTime utcNow = DateTime.UtcNow;

            // === RESPONSE MODELS =====================================================
            Cam_WithdrawResponse okResp = new Cam_WithdrawResponse
            {
                balance = 0,
                casinoTransferId = "",
                responseCodeReason = ((int)Cam_ResponseStatus.RS_200_Success).ToString()
            };
            Cam_WithdrawResponse response = new Cam_WithdrawResponse
            {
                responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString()
            };

            bool _useDenormThreads = CcConfig.AppSettingBool("CASINOAM_USE_DENORM_THREADS", true);

            // === CONTEXT BASE ========================================================
            EndUser local_eu = new EndUser { EU_ID = euId, useNoLockWhePossible = true };
            local_eu.GetById();
            auxPars["_local_eu"] = local_eu;

            string rawRequest = auxPars.getTypedValue<string>("rawRequest", null, false);
            string author = auxPars.getTypedValue("author", typeof(CasinoExtIntAMSWCoreTest).Name);

            // Stato "esterno" (Amusnet) per retry/mapping
            Cam_ResponseStatus targetStatus = Cam_ResponseStatus.RS_5xx_InternalServerError;
            // Stati "interni" per il buffer
            int targetState = (int)CasinoMovimentiBuffer.States.Deleted;
            int targetStateFinal = (int)CasinoMovimentiBuffer.States.Deleted;

            CasinoMovimentiBuffer.Type cmbType = CasinoMovimentiBuffer.Type.Loose;
            CasinoMovimentiBuffer retryOp = null;

            long cmbId = -1;
            int partition = 0;
            int MATCH_Id = -1;
            DateTime opDateUtc = DateTime.MinValue;
            bool skipMainFlow = false;

            // === PRECHECK REQUEST VALIDATION =========================================
            #region PRECHECK REQUEST VALIDATION
            Cam_WithdrawRequest rqst = new Cam_WithdrawRequest
            {
                playerId = auxPars.getTypedValue("playerId", ""),
                transferId = auxPars.getTypedValue("transferId", ""),
                amount = auxPars.getTypedValue("amount", 0L),
                currency = auxPars.getTypedValue("currency", ""),
                gameId = auxPars.getTypedValue("gameId", ""),
                gameNumber = auxPars.getTypedValue("gameNumber", ""),
                sessionId = auxPars.getTypedValue("sessionId", ""),
                baseBet = auxPars.getTypedValue("baseBet", 0L),
                portalCode = auxPars.getTypedValue("portalCode", ""),
                platformType = auxPars.getTypedValue("platformType", "")
            };

            string transactionId = rqst.transferId;
            string roundRef = rqst.gameNumber ?? "";
            string extCode = rqst.gameId + " - " + rqst.sessionId ?? "";
            bool hasRoundRef = roundRef.Length > 0;
            bool stakeInvalid = false;

            HashParams currSession = _getCurrentSessionInfo(euId, rqst.gameId, auxPars);
            string ticket = currSession.getTypedValue("ticket", string.Empty, false);
            HashParams sessionInfos = _getSessionInfosByTicket(ticket, local_eu, transactionId, methName);
            bool funBonusSession = sessionInfos.getTypedValue("CSES_PLAYBONUS_TABLE", 0, false) > 0;
            bool freeRoundSession = sessionInfos.getTypedValue("freeRoundSession", false, false);

            // Amusnet non espone 400 -> per parametri mancanti/invalidi uso 409 (Conflict)
            if (rqst.playerId.Length == 0 || transactionId.Length == 0 || extCode.Length == 0 || rqst.sessionId.Length == 0 || rqst.amount <= 0)
            {
                skipMainFlow = true;
                targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                response = new Cam_WithdrawResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "BAD_REQUEST" };
            }
            // playerId mismatch -> 403
            if (!skipMainFlow && !rqst.playerId.Equals(CcConfig.AppSettingStr("CASINOEXTINTAM_ENVIRONMENT", "AD") + "@" + local_eu.EU_UID + (funBonusSession ? "@FUN" : "@CASH"), StringComparison.Ordinal))
            {
                skipMainFlow = true;
                targetStatus = Cam_ResponseStatus.RS_403_Forbidden;
                response = new Cam_WithdrawResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "Player ID mismatch" };
            }
            #endregion

            // === PRECHECK RETRY / IDEMPOTENCY ========================================
            #region PRECHECK RETRYOP

            try
            {
                EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), unl = true };
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    centEu.getByUserId();
                    retryOp = CasinoSessionMgr.def.BUFFER_GetAll(
                        new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_FK_EU_Id", centEu.EU_ID,
                            "CMB_STATEs", new[] {
                        (int)CasinoMovimentiBuffer.States.Dumped,
                        (int)CasinoMovimentiBuffer.States.PreCommitted,
                        (int)CasinoMovimentiBuffer.States.Committed,
                        (int)CasinoMovimentiBuffer.States.Deleted,
                        (int)CasinoMovimentiBuffer.States.Completed
                            },
                            "CMB_EXTTXID", transactionId
                        )
                    ).FirstOrDefault();
                }
            }
            catch (Exception logContinue) { Log.exc(logContinue); }
            
            #endregion

            // === PRECHECK CURRENT SESSION & SITTING-OUT ==============================
            #region PRECHECK CURRENT SESSION

            if (currSession != null && currSession.Count > 0)
            {
                CasinoSubscription fstSub = currSession.getTypedValue<CasinoSubscription>("sub", null);
                if (fstSub != null)
                {
                    object sittingOut = CasinoAccountingMgr.sittingOutCache[fstSub.MATCH_Id];
                    if (sittingOut != null)
                    {
                        currSession = null;
                        stakeInvalid = true;
                    }
                }
            }

            if ((currSession == null || currSession.Count == 0) && retryOp == null)
            {
                skipMainFlow = true;
                targetStatus = Cam_ResponseStatus.RS_401_UnauthorizedAccess; // sessione non valida
                response = new Cam_WithdrawResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "INVALID_SESSION" };

                // registrazione "Deleted" out-of-session (come HS)
                try
                {
                    EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), unl = true };
                    using (new GGConnMgr.CentralCtxWrap("CENTRAL")) centEu.getByUserId();

                    CasinoMovimentiBuffer newMov = new CasinoMovimentiBuffer
                    {
                        CMB_SERVICE = (int)FinMovExtern.Servizi.CasinoAM,
                        CMB_FK_FST_MATCH_Id = -1,
                        CMB_FK_CURR_MATCH_Id = -1,
                        CMB_FK_EU_Id = centEu.EU_ID,
                        CMB_TYPE = (int)cmbType,
                        CMB_STATE = (int)CasinoMovimentiBuffer.States.Deleted,
                        CMB_CLOSED = (int)CasinoMovimentiBuffer.Closed.Opened,
                        CMB_AMOUNT_PLAYBONUS = funBonusSession ? Convert.ToInt32(rqst.amount) : 0,
                        CMB_EXTTXID = transactionId,
                        CMB_OPDATE_UTC = utcNow,
                        CMB_CREATEDAT = utcNow.ToLocalTime(),
                        CMB_RESULT = ((int)targetStatus).ToString(),
                        CMB_RECVTEXT = rawRequest,
                        CMB_SENTTEXT = JsonConvert.SerializeObject(response),
                        CMB_EXTROUNDREF = roundRef,
                        CMB_FK_USER_ID = OrgUserMgr.def.GetFirstMaster().GetCentralUser().USER_ID,
                        CMB_EXTCODE = extCode
                    };
                    if (longSupportStage <= 2)
                    {
                        newMov.CMB_AMOUNT_TOT = Convert.ToInt32(rqst.amount);
                        newMov.CMB_AMOUNT_BONUS = funBonusSession ? Convert.ToInt32(rqst.amount) : 0;
                        newMov.CMB_AMOUNT_WITH = Convert.ToInt32(rqst.amount);
                    }
                    if (longSupportStage >= 1)
                    {
                        newMov.CMB_AMOUNT_TOT_LONG = rqst.amount;
                        newMov.CMB_AMOUNT_BONUS_LONG = funBonusSession ? rqst.amount : 0;
                        newMov.CMB_AMOUNT_WITH_LONG = rqst.amount;
                    }
                    using (new GGConnMgr.CentralCtxWrap("CASINO")) newMov.Create(author);
                }
                catch (Exception logContinue)
                {
                    Log.exc(logContinue);
                }

                try
                {
                    if (CcConfig.AppSettingBool("CASINOAM_DEBUG_INVALID_SESSIONS", true))
                        Log.getLogger("CasinoAMInvalids").Debug("InvalidSession::Request:{0} IsInvalidStake:{1} RAW:{2}", transactionId, stakeInvalid, rawRequest);
                }
                catch (Exception logContinue) { Log.exc(logContinue); }
            }
            
            #endregion

            // === MAIN FLOW ===========================================================
            #region MAIN FLOW
            if (!skipMainFlow)
            {
                HashParams monitor = null;
                try
                {
                    if (retryOp == null)
                    {

                        MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
                        int centEuId = sessionInfos.getTypedValue("centEU_Id", -1, false);
                        int MATCH_FR_VALUE = sessionInfos.getTypedValue("MATCH_FR_VALUE", 0);
                        partition = sessionInfos.getTypedValue("PARTITION", 0);

                        long recvMoneyInt, recvTotInt, recvMoneyAbsInt, recvTotAbsInt;
                        {
                            long recvMoney = rqst.amount;
                            if (recvMoney < 0) throw new Exception("MANAGED::96xINVALID_MONEY_FOR_TX");
                            if (recvMoney > int.MaxValue && longSupportStage <= 2) throw new Exception("MANAGED::97xMONEY_TOO_HIGH");

                            recvMoneyInt = -recvMoney; // debito
                            recvMoneyAbsInt = recvMoney;
                            recvTotInt = recvMoneyInt;
                            recvTotAbsInt = recvMoneyAbsInt;
                        }
                        
                        if (recvTotAbsInt == 0 && CcConfig.AppSettingBool("CASINOAM_BLOCK_ZERO_BET", SystemInfos2.def.Environment.Beta))
                            throw new Exception("MANAGED::96xINVALID_MONEY_FOR_TX");
                        
                        monitor = _acquireMonitor(ticket);
                        bool multibetLiveFlow = CcConfig.AppSettingBool("CASINOAM_ENABLE_MULTIBET_LIVE_FLOW", true) && (sessionInfos.getTypedValue("GAME_ISLIVE", 0) == 1 || CcConfig.AppSettingStr("CASINOAM_MULTIBET_RNG_GAMES", "#bjma#bjmb#").Contains("#" + rqst.gameId + "#"));
                        bool isSeamlessExternal = useSeamlessExternalIntegration("bet", sessionInfos);
                        CasinoMovimentiBuffer checkOp = _getSingleOperation(sessionInfos, transactionId);

                        if (checkOp == null)
                        {
                            //TOCHECK SE POSSO MANTENERE IL CONTROLLO SUL BALANCE INTERNO,
                            //SE è POSSIBILE RICARICARE IL CONTO CON UNA SESSIONE APERTA SU ADMIRAL SAREBBE MEGLIO VERIFICARE SEMPRE SUL WALLET ESTERNO LA PRESENZA DI FONDI
                            //SE NO SI RISCHIA DI BLOCCARE ERRONEAMENTE DELLE BET
                            bool needCheckSaldo = !isSeamlessExternal || !CcConfig.AppSettingBool("CASINOAM_DO_NOT_CHECKSALDO", false);

                            SessionBalancesHolder sessionBalanceCents = _getFastSessionBalancesByTicket(ticket, sessionInfos);
                            CasinoMovimentiBuffer relatedOp = null;
                            int remFr = sessionBalanceCents.lastRemFr;

                            bool checkSaldo = !needCheckSaldo || (!freeRoundSession ? sessionBalanceCents.amountTotal >= recvTotAbsInt : remFr > 0);

                            long theoricalBalanceTot = sessionBalanceCents.amountTotal;
                            List<CasinoMovimentiBuffer> openDebits;

                            if (checkSaldo)
                            {
                                theoricalBalanceTot = sessionBalanceCents.amountTotal + recvTotInt; // recvTotInt negativo
                                targetStateFinal = _useDenormThreads ? (int)CasinoMovimentiBuffer.States.Dumped : (int)CasinoMovimentiBuffer.States.Committed;
                                targetState = isSeamlessExternal ? (int)CasinoMovimentiBuffer.States.PreDumped : targetStateFinal;
                                targetStatus = Cam_ResponseStatus.RS_200_Success;
                                okResp.balance = (long)theoricalBalanceTot;
                                response = okResp;

                                bool blocksBetsOnPending = CcConfig.AppSettingBool("CASINOAM_BLOCK_BETS_ON_PENDING", SystemInfos2.def.Environment.Beta);

                                SmartHash openLoadAux = new SmartHash();
                                openDebits = _getOpenDebits("bet", sessionInfos, openLoadAux);
                                
                                bool pendingRetry = openLoadAux.get("pendingRetry", false);

                                bool goOnPendingCheck = !blocksBetsOnPending || !pendingRetry;
                                //Log.getLogger("GiovanniDebug").BetaDebug("goOnPendingCheck = " + goOnPendingCheck);
                                //TOCHECK SE REASON = ROUND_BEGIN DEVO CHIUDERE EVENTUALI ROUND PRECEDENTI PENDENTI
                                if (goOnPendingCheck)
                                {
                                    //Log.getLogger("GiovanniDebug").BetaDebug("block bets on pending");
                                    for (int i = 0; i < openDebits.Count && relatedOp == null; i++)
                                        if (openDebits[i].CMB_RELATED_FK_CMB_ID <= 0)
                                            relatedOp = openDebits[i];

                                    if (CcConfig.AppSettingBool("CASINOAM_BLOCK_MULIPLE_ROUND_BETS", true) && !multibetLiveFlow)
                                    {
                                        bool allOk = true;
                                        for (int i = 0; i < openDebits.Count && allOk; i++)
                                        {
                                            if (CcConfig.AppSettingBool("CASINOAM_BLOCK_MULIPLE_ROUND_BETS_LAZY", true))
                                            {
                                                //Log.getLogger("GiovanniDebug").BetaDebug("block multiple lazy");
                                                allOk = openDebits[i].CMB_EXTROUNDREF == roundRef;
                                            }
                                            else
                                            {
                                                allOk = false;
                                            }
                                        }
                                        if (!allOk)
                                        {
                                            targetStateFinal = targetState = (int)CasinoMovimentiBuffer.States.Deleted;
                                            targetStatus = Cam_ResponseStatus.RS_409_Conflict; // round aperto
                                            response = new Cam_WithdrawResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "Open Round" };
                                        }
                                    }
                                }
                                else
                                {
                                    targetStateFinal = targetState = (int)CasinoMovimentiBuffer.States.Deleted;
                                    targetStatus = Cam_ResponseStatus.RS_409_Conflict; // pending update
                                    response = new Cam_WithdrawResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "Pending update" };
                                }
                            }
                            else
                            {
                                targetStateFinal = targetState = (int)CasinoMovimentiBuffer.States.Deleted;
                                targetStatus = Cam_ResponseStatus.RS_409_Conflict; // saldo insufficiente
                                response = new Cam_WithdrawResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "Insufficient balance" };
                                openDebits = new List<CasinoMovimentiBuffer>();
                            }

                            // NB: va creato comunque
                            CasinoMovimentiBuffer newMov = new CasinoMovimentiBuffer
                            {
                                CMB_SERVICE = (int)FinMovExtern.Servizi.CasinoAM,
                                CMB_FK_FST_MATCH_Id = MATCH_Id,
                                CMB_FK_CURR_MATCH_Id = MATCH_Id,
                                CMB_FK_EU_Id = centEuId,
                                CMB_TYPE = (int)cmbType,
                                CMB_STATE = targetState,
                                CMB_CLOSED = (int)CasinoMovimentiBuffer.Closed.Opened,
                                CMB_AMOUNT_PLAYBONUS = funBonusSession ? Convert.ToInt32(recvTotInt) : 0,
                                CMB_EXTTXID = transactionId,
                                CMB_EXTTXREFID = null,
                                CMB_OPDATE_UTC = utcNow,
                                CMB_CREATEDAT = utcNow.ToLocalTime(),
                                CMB_RESULT = ((int)targetStatus).ToString(),
                                CMB_RECVTEXT = rawRequest,
                                CMB_SENTTEXT = JsonConvert.SerializeObject(response),
                                CMB_EXTROUNDREF = roundRef,
                                CMB_PROGR = sessionBalanceCents.lastProgr + 1,
                                CMB_SW_EXTTOKEN = sessionInfos.getTypedValue("EXTTOKEN", string.Empty, false),
                                CMB_FK_USER_ID = OrgUserMgr.def.GetFirstMaster().GetCentralUser().USER_ID,
                                CMB_EXTCODE = extCode
                            };
                            if (longSupportStage <= 2)
                            {
                                newMov.CMB_AMOUNT_TOT = Convert.ToInt32(recvTotInt);
                                newMov.CMB_AMOUNT_BONUS = funBonusSession ? Convert.ToInt32(recvTotInt) : 0;
                                newMov.CMB_AMOUNT_WITH = Convert.ToInt32(recvTotInt);
                            }
                            if (longSupportStage >= 1)
                            {
                                newMov.CMB_AMOUNT_TOT_LONG = recvTotInt;
                                newMov.CMB_AMOUNT_BONUS_LONG = funBonusSession ? recvTotInt : 0;
                                newMov.CMB_AMOUNT_WITH_LONG = recvTotInt;
                                ;
                            }

                            long relatedGrdId = -1;
                            long firstRelatedGrdId = -1;
                            if (relatedOp != null)
                            {
                                // #REF1# keeping current roundId
                                bool allowOptMultiBetGeneral = CcConfig.AppSettingBool("CASINOAM_OPTIM_MULIPLE_ROUND_BETS", true);
                                bool allowOptMultiBetOnlyLive = CcConfig.AppSettingBool("CASINOAM_OPTIM_MULIPLE_ROUND_BETS_ONLIVE", true);
                                bool allowOptMultiBet = multibetLiveFlow && allowOptMultiBetOnlyLive || allowOptMultiBetGeneral;
                                bool groupRelatedBets = CcConfig.AppSettingBool("CASINOAM_MULIPLE_ROUND_BETS_GROUP", true);
                                if (!allowOptMultiBet)
                                    newMov.CMB_EXTROUNDREF = relatedOp.CMB_EXTROUNDREF;
                                relatedGrdId = relatedOp.CMB_ID;
                                firstRelatedGrdId = relatedOp.CMB_RELATED_FK_CMB_ID > 0 ? relatedOp.CMB_RELATED_FK_CMB_ID : relatedGrdId;
                                newMov.CMB_RELATED_FK_CMB_ID = allowOptMultiBet && groupRelatedBets ? firstRelatedGrdId : relatedGrdId;
                            }

                            int createRes;
                            using (new GGConnMgr.CentralCtxWrap("CASINO"))
                            {
                                createRes = newMov.Create(author);
                                cmbId = newMov.CMB_ID;
                            }
                            if (createRes > 0)
                            {
                                if (targetStatus == Cam_ResponseStatus.RS_200_Success)
                                {
                                    bool allOk;
                                    CasinoMovimentiBuffer newMovUpd = new CasinoMovimentiBuffer { CMB_ID = newMov.CMB_ID };
                                    bool needUpdate = true; 
                                    okResp.casinoTransferId = CcConfig.AppSettingStr("CASINOAM_TRANSACTION_REFERENCE_ENV", "BG") + (Convert.ToString(newMov.CMB_ID, 16).PadLeft(10, '0'));

                                    if (isSeamlessExternal)
                                    {
                                        int roundRefExt = newMov.CMB_PROGR;
                                        bool roundClosed = newMov.CMB_CLOSED > 0;
                                        int dumpedStateFinal = targetStateFinal;

                                        CasGameState casGameState = new CasGameState(
                                            ExtIntMgr.PlatformKeys.CASINOAM, ticket, newMov, funBonusSession, sessionInfos);

                                        HashResult extRes = CBSLWalletMgr.def.makeExternalTransactions(
                                            newMov.CMB_ID, euId, casGameState,
                                            new HashParams(
                                                "fstMatchId", MATCH_Id,
                                                "opType", "" + cmbType,
                                                "progr", roundRefExt,
                                                "extToken", newMov.CMB_SW_EXTTOKEN,
                                                "CasinoSessionId", sessionInfos.getTypedValue("SWTOKEN", string.Empty, false),
                                                "GameMode", funBonusSession ? "FUN" : "CASH",
                                                "GameId", sessionInfos.getTypedValue("GAME_KEY", extCode, false),
                                                "roundRef", roundRef,
                                                "freeRoundSession", false,
                                                "freeRounds", 0,
                                                "freeRoundVal", 0,
                                                "roundClosed", roundClosed,
                                                "spinType", (int)cmbType,
                                                "relatedGrdId", relatedGrdId,
                                                "firstRelatedGrdId", firstRelatedGrdId,
                                                "grdState", newMov.CMB_STATE,
                                                "dumpedState", dumpedStateFinal,
                                                "opDateUtc", newMov.CMB_OPDATE_UTC,
                                                "opId", newMov.CMB_ID,
                                                "GameName", sessionInfos.getTypedValue("GAME_NAME", string.Empty),
                                                "checkBalance", CcConfig.AppSettingBool("CASINOAM_EXTBALANACE_UPDATE", true),
                                                "autoClose",  CcConfig.AppSettingBool("CASINOAM_SEAMLESS_WALLET_AUTOCLOSE", false),
                                                //"isJackpotWin", jackpotAmount > 0,
                                                //"jackpotWin", jackpotAmount
                                                "service", (int)FinMovExtern.Servizi.CasinoAM,
                                                "partition", partition
                                            ));

                                        bool cgrStateCommitted = extRes.ContainsKey("cgrStateCommitted") && (bool)extRes["cgrStateCommitted"];
                                        bool cgrIsRetrying = extRes.ContainsKey("cgrIsRetrying") && (bool)extRes["cgrIsRetrying"];
                                        if (extRes.IsOk)
                                        {
                                            if (extRes.ContainsKey("newBalance"))
                                            {
                                                okResp.balance = sessionBalanceCents.amountTotal = (long)extRes["newBalance"];
                                                if (CcConfig.AppSettingBool("CASINOAM_SW_SETNEWBALANCE", true))
                                                    _setFastSessionBalancesByTicket(ticket, sessionBalanceCents, sessionInfos);
                                            }
                                            else
                                            {
                                                bool skipSetOkResp = CcConfig.AppSettingBool("TEMP_FIX_CASAM_CASHRESP", true) || (multibetLiveFlow && CcConfig.AppSettingBool("TEMP_FIX_CASAM_CASHRESP_ONLYLIVEFLOW", true));
                                                if (!skipSetOkResp)
                                                    okResp.balance = (sessionBalanceCents.amountTotal = casGameState.getTotalBalance());
                                            }

                                            response = okResp;
                                            if (!cgrStateCommitted)
                                            {
                                                newMovUpd.CMB_STATE = targetStateFinal;
                                            }
                                            newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);

                                            allOk = true;
                                            if (hasRoundRef) _setStakeByRefInCache(sessionInfos, roundRef, newMov);
                                            auxPars["op"] = newMov;
                                        }
                                        else
                                        {
                                            allOk = false;
                                            bool shouldBeTried = extRes.ErrorMessage == "EXEC_UNKNOWN";
                                            string low = ("" + extRes["txResErrorMessage"]).ToLowerInvariant(); 
                                            _LogDebug(20, string.Format("ATT {0} ERROR ON EXTWALLET {1} | Ext Wallet Message: {2}", transactionId, extRes.ErrorMessage, low), new EndUser { EU_ID = euId }, transactionId, methName, true);

                                            targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                                            response = new Cam_WithdrawResponse
                                            {
                                                responseCodeReason = targetStatus.ToString(),
                                                errorMessage = "Error in external wallet"
                                            };

                                            if (!cgrStateCommitted)
                                                newMov._CMB_STATE = newMovUpd.CMB_STATE = (int)CasinoMovimentiBuffer.States.Deleted;
                                            newMov._CMB_RESULT = ((int)targetStatus).ToString();
                                            newMov._CMB_SENTTEXT = newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                        }
                                    }
                                    else
                                    {
                                        if (hasRoundRef) _setStakeByRefInCache(sessionInfos, roundRef, newMov);
                                        auxPars["op"] = newMov;
                                        /*
                                         * lucab 20191004
                                         * responseJson should be saved so needUpdate always true
                                         */
                                        //bool needUpdate;
                                        SessionBalancesHolder finalBalancesCents = _getFastSessionBalancesByTicket(ticket, sessionInfos, new HashParams("reload", true));
                                        bool isFreeRoundSession = sessionInfos.getTypedValue("freeRoundSession", false);
                                        if (finalBalancesCents.amountTotal >= 0)
                                        {
                                            allOk = true;
                                            okResp.casinoTransferId = CcConfig.AppSettingStr("CASINOAM_TRANSACTION_REFERENCE_ENV", "BG") +  (Convert.ToString(newMov.CMB_ID, 16).PadLeft(10, '0'));
                                            if (isFreeRoundSession)
                                                okResp.balance = (long)finalBalancesCents.amountTotalWin;
                                            else
                                                okResp.balance = (long)finalBalancesCents.amountTotal;
                                            response = okResp;
                                            newMov._CMB_SENTTEXT = newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                            if (finalBalancesCents.amountTotal != theoricalBalanceTot)
                                                _LogDebug(10, string.Format("ATT {0} WRONG BALANCE THEOR<>REAL {1}<>{2}", transactionId, finalBalancesCents.amountTotal, theoricalBalanceTot), new EndUser { EU_ID = euId }, transactionId, methName, true);
                                        }
                                        else
                                        {
                                            allOk = false;
                                            _LogDebug(20, string.Format("ATT {0} FINAL<0 {1}", transactionId, finalBalancesCents.amountTotal), new EndUser { EU_ID = euId }, transactionId, methName, true);
                                            targetStatus = Cam_ResponseStatus.RS_403_Forbidden;

                                            response = new Cam_WithdrawResponse
                                            {
                                                responseCodeReason = ((int)targetStatus).ToString(),
                                            };
                                            newMov._CMB_STATE = newMovUpd.CMB_STATE = (int)CasinoMovimentiBuffer.States.Deleted;
                                            newMov._CMB_SENTTEXT = newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                            newMov._CMB_RESULT = newMovUpd.CMB_RESULT = "" + (int)targetStatus;
                                        }
                                    }

                                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                    {
                                        if (needUpdate) PatternLib.def.RetryAction(5, 2000, () => newMovUpd.Update(author), new HashParams("logContinue", true));
                                        _pingTableSession_Central(sessionInfos, author);
                                    }

                                    _setSingleOperationInCache(sessionInfos, transactionId, newMov);

                                    if (allOk)
                                    {
                                        openDebits.Add(newMov);
                                        _setOpenDebits("bet", sessionInfos, openDebits);
                                    }
                                    else
                                    {
                                        if (CcConfig.AppSettingBool("TEMP_Csp_FIX_RESETOPENDEBS", true))
                                            _setOpenDebits("bet", sessionInfos, null);
                                    }
                                }
                                // else: “errore” già gestito sopra
                            }
                            else throw new Exception("MANAGED::99xINTERNAL_ERROR_RETRY");
                        }
                        else
                        {
                            retryOp = checkOp;
                        }
                    }

                    #region RESEND
                    if (retryOp != null)
                    {
                        response = JsonConvert.DeserializeObject<Cam_WithdrawResponse>(retryOp.CMB_SENTTEXT);
                        if (currSession != null && !string.IsNullOrEmpty(currSession.getTypedValue("ticket", string.Empty, true)))
                        {
                            if (response != null && CcConfig.AppSettingBool("CASINOAM_ACTUAL_BALANCE_ON_RESEND", true))
                            {
                                //string ticket = currSession.getTypedValue("ticket", string.Empty, false);
                                //HashParams sessionInfos = _getSessionInfosByTicket(ticket, local_eu, transactionId, methName);
                                SessionBalancesHolder sessionBalanceCents = _getFastSessionBalancesByTicket(ticket, sessionInfos);
                                response.balance = sessionBalanceCents.amountTotal;
                            }
                        }

                        try
                        {
                            targetStatus = (Cam_ResponseStatus)int.Parse("" + retryOp.CMB_RESULT);
                        }
                        catch (Exception logContinue)
                        {
                            Log.exc(logContinue);
                            targetStatus = Cam_ResponseStatus.RS_5xx_InternalServerError;
                        }

                        _LogDebug(30, "Resending", new EndUser { EU_ID = euId }, transactionId, methName);
                        auxPars["_isRetransmission"] = true;
                    }
                    #endregion

                    auxPars["_cmbId"] = cmbId;
                    auxPars["_partition"] = partition;
                    auxPars["_matchId"] = MATCH_Id;
                    auxPars["_opDateUtc"] = opDateUtc.ToString("yyyyMMddHHmmss");
                    auxPars["_targetStatus"] = targetStatus;
                }
                catch (Exception exc)
                {
                    if (exc.Message.StartsWith("MANAGED::")) throw;
                    Log.exc(exc);
                    throw new Exception("MANAGED::99xINTERNAL_ERROR_RETRY");
                }
                finally
                {
                    if (monitor != null)
                        _releaseMonitor(monitor);
                }
            }
            #endregion

            return response;
        }

        #endregion withdraw

        #region deposit
        public virtual Cam_DepositResponse _CasinoAM_Deposit(int euId, HashParams auxPars)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            const _MethNames methName = _MethNames.Cam_Deposit;
            DateTime utcNow = DateTime.UtcNow;

            // === RESPONSE MODELS =====================================================
            Cam_DepositResponse okResp = new Cam_DepositResponse
            {
                balance = 0,
                casinoTransferId = "",
                responseCodeReason = ((int)Cam_ResponseStatus.RS_200_Success).ToString()
            };
            Cam_DepositResponse response = new Cam_DepositResponse
            {
                responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString(),
                errorMessage = "UNKNOWN_ERROR"
            };

            bool _useDenormThreads = CcConfig.AppSettingBool("CASINOAM_USE_DENORM_THREADS", true);

            // === CONTEXT BASE ========================================================
            EndUser local_eu = new EndUser { EU_ID = euId, useNoLockWhePossible = true };
            local_eu.GetById();
            auxPars["_local_eu"] = local_eu;

            string rawRequest = auxPars.getTypedValue<string>("rawRequest", null, false);
            string author = auxPars.getTypedValue("author", typeof(CasinoExtIntAMSWCoreTest).Name);

            // Stato esterno (Amusnet) e stato interno buffer
            Cam_ResponseStatus targetStatus = Cam_ResponseStatus.RS_5xx_InternalServerError;
            int targetState = (int)CasinoMovimentiBuffer.States.Deleted;
            int targetStateFinal = (int)CasinoMovimentiBuffer.States.Deleted;

            CasinoMovimentiBuffer.Type cmbType = CasinoMovimentiBuffer.Type.Win; // credito
            CasinoMovimentiBuffer retryOp = null;

            long cmbId = -1;
            int partition = 0;
            int MATCH_Id = -1;
            DateTime opDateUtc = DateTime.MinValue;

            bool skipMainFlow = false;
            // === PRECHECK REQUEST VALIDATION =========================================
            #region PRECHECK REQUEST VALIDATION
            Cam_DepositRequest rqst = new Cam_DepositRequest
            {
                playerId = auxPars.getTypedValue("playerId", ""),
                transferId = auxPars.getTypedValue("transferId", ""),
                amount = auxPars.getTypedValue("amount", 0L),
                currency = auxPars.getTypedValue("currency", ""),
                gameId = auxPars.getTypedValue("gameId", ""),
                gameNumber = auxPars.getTypedValue("gameNumber", ""),
                sessionId = auxPars.getTypedValue("sessionId", ""),
                portalCode = auxPars.getTypedValue("portalCode", ""),
                platformType = auxPars.getTypedValue("platformType", "")
            };

            string transactionId = rqst.transferId;
            string roundRef = rqst.gameNumber ?? "";
            string extCode = rqst.gameId + " - " + rqst.sessionId ?? "";
            bool hasRoundRef = roundRef.Length > 0;
            bool winInvalid = false;

            HashParams currSession = _getCurrentSessionInfo(euId, rqst.gameId, auxPars);
            string ticket = currSession.getTypedValue("ticket", string.Empty, false);
            HashParams sessionInfos = _getSessionInfosByTicket(ticket, local_eu, transactionId, methName);
            bool funBonusSession = sessionInfos.getTypedValue("CSES_PLAYBONUS_TABLE", 0, false) > 0;

            // Amusnet non espone 400 -> parametri mancanti/invalidi => 409 (Conflict)
            if (rqst.playerId.Length == 0 || transactionId.Length == 0 || extCode.Length == 0 || rqst.sessionId.Length == 0 || rqst.amount < 0)
            {
                skipMainFlow = true;
                targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                response = new Cam_DepositResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "BAD_REQUEST" };
            }
            // playerId mismatch -> 403
            if (!skipMainFlow && !rqst.playerId.Equals(CcConfig.AppSettingStr("CASINOEXTINTAM_ENVIRONMENT", "AD") + "@" + local_eu.EU_UID + (funBonusSession ? "@FUN" : "@CASH"), StringComparison.Ordinal))
            {
                skipMainFlow = true;
                targetStatus = Cam_ResponseStatus.RS_403_Forbidden;
                response = new Cam_DepositResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "Player ID mismatch" };
            }
            #endregion

            // === PRECHECK RETRY / IDEMPOTENCY ========================================
            #region PRECHECK RETRYOP

            try
            {
                EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), unl = true };
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    centEu.getByUserId();
                    retryOp = CasinoSessionMgr.def.BUFFER_GetAll(
                        new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_FK_EU_Id", centEu.EU_ID,
                            "CMB_STATEs", new[] {
                        (int)CasinoMovimentiBuffer.States.Dumped,
                        (int)CasinoMovimentiBuffer.States.PreCommitted,
                        (int)CasinoMovimentiBuffer.States.Committed,
                        (int)CasinoMovimentiBuffer.States.Deleted,
                        (int)CasinoMovimentiBuffer.States.Completed
                            },
                            "CMB_EXTTXID", transactionId
                        )
                    ).FirstOrDefault();
                }
            }
            catch (Exception logContinue) { Log.exc(logContinue); }

            #endregion

            // === PRECHECK CURRENT SESSION & SITTING-OUT ==============================
            #region PRECHECK CURRENT SESSION

            if (currSession != null && currSession.Count > 0)
            {
                CasinoSubscription fstSub = currSession.getTypedValue<CasinoSubscription>("sub", null);
                if (fstSub != null)
                {
                    object sittingOut = CasinoAccountingMgr.sittingOutCache[fstSub.MATCH_Id];
                    if (sittingOut != null)
                    {
                        currSession = null;
                        winInvalid = true;
                    }
                }
            }

            if ((currSession == null || currSession.Count == 0) && retryOp == null)
            {
                skipMainFlow = true;
                targetStatus = Cam_ResponseStatus.RS_401_UnauthorizedAccess; // sessione non valida
                response = new Cam_DepositResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "INVALID_SESSION" };

                // registrazione "Deleted" out-of-session
                try
                {
                    EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), unl = true };
                    using (new GGConnMgr.CentralCtxWrap("CENTRAL")) centEu.getByUserId();

                    CasinoMovimentiBuffer newMov = new CasinoMovimentiBuffer
                    {
                        CMB_SERVICE = (int)FinMovExtern.Servizi.CasinoAM,
                        CMB_FK_FST_MATCH_Id = -1,
                        CMB_FK_CURR_MATCH_Id = -1,
                        CMB_FK_EU_Id = centEu.EU_ID,
                        CMB_TYPE = (int)cmbType,
                        CMB_STATE = (int)CasinoMovimentiBuffer.States.Deleted,
                        CMB_CLOSED = (int)CasinoMovimentiBuffer.Closed.Opened,
                        CMB_AMOUNT_PLAYBONUS = funBonusSession ? Convert.ToInt32(rqst.amount) : 0,
                        CMB_EXTTXID = transactionId,
                        CMB_OPDATE_UTC = utcNow,
                        CMB_CREATEDAT = utcNow.ToLocalTime(),
                        CMB_RESULT = ((int)targetStatus).ToString(),
                        CMB_RECVTEXT = rawRequest,
                        CMB_SENTTEXT = JsonConvert.SerializeObject(response),
                        CMB_EXTROUNDREF = roundRef,
                        CMB_FK_USER_ID = OrgUserMgr.def.GetFirstMaster().GetCentralUser().USER_ID,
                        CMB_EXTCODE = extCode
                    };
                    if (longSupportStage <= 2)
                    {
                        newMov.CMB_AMOUNT_TOT = Convert.ToInt32(rqst.amount); // win => positivo
                        newMov.CMB_AMOUNT_BONUS = funBonusSession ? Convert.ToInt32(rqst.amount) : 0;
                        newMov.CMB_AMOUNT_WITH = 0;
                    }
                    if (longSupportStage >= 1)
                    {
                        newMov.CMB_AMOUNT_TOT_LONG = rqst.amount;
                        newMov.CMB_AMOUNT_BONUS_LONG = funBonusSession ? rqst.amount : 0;
                        newMov.CMB_AMOUNT_WITH_LONG = 0;
                    }
                    using (new GGConnMgr.CentralCtxWrap("CASINO")) newMov.Create(author);
                }
                catch (Exception logContinue) { Log.exc(logContinue); }

                try
                {
                    if (CcConfig.AppSettingBool("CASINOAM_DEBUG_INVALID_SESSIONS", true))
                        Log.getLogger("CasinoAMInvalids").Debug("InvalidSession::Deposit:{0} IsInvalidWin:{1} RAW:{2}", transactionId, winInvalid, rawRequest);
                }
                catch (Exception logContinue) { Log.exc(logContinue); }
            }

            #endregion

            // === MAIN FLOW ===========================================================
            #region MAIN FLOW
            if (!skipMainFlow)
            {
                HashParams monitor = null;
                try
                {
                    if (retryOp == null)
                    {
                        targetStatus = Cam_ResponseStatus.RS_200_Success;
                        
                        MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
                        int centEuId = sessionInfos.getTypedValue("centEU_Id", -1, false);
                        partition = sessionInfos.getTypedValue("PARTITION", 0);

                        // importi (DEPOSIT = credito => positivi)
                        long recvMoneyInt, recvTotInt, recvMoneyAbsInt, recvTotAbsInt;
                        {
                            long recvMoney = rqst.amount;
                            if (recvMoney < 0) throw new Exception("MANAGED::96xINVALID_MONEY_FOR_TX");
                            if (recvMoney > int.MaxValue && longSupportStage <= 2) throw new Exception("MANAGED::97xMONEY_TOO_HIGH");

                            recvMoneyInt = recvMoney;       // credito (+)
                            recvMoneyAbsInt = recvMoney;
                            recvTotInt = recvMoneyInt;
                            recvTotAbsInt = recvMoneyAbsInt;
                        }


                        monitor = _acquireMonitor(ticket);

                        //ZeroWin round close
                        bool forceRoundClose = auxPars.getTypedValue("forceRoundClose", false);
                        bool isSeamlessExternal = useSeamlessExternalIntegration("win", sessionInfos);
                        bool multibetLiveFlow = CcConfig.AppSettingBool("CASINOAM_ENABLE_MULTIBET_LIVE_FLOW", true) && (sessionInfos.getTypedValue("GAME_ISLIVE", 0) == 1 || CcConfig.AppSettingStr("CASINOAM_MULTIBET_RNG_GAMES", "#bjma#bjmb#").Contains("#" + rqst.gameId + "#"));
                        CasinoMovimentiBuffer checkOp = _getSingleOperation(sessionInfos, transactionId);
                        if (checkOp == null)
                        {
                            // saldo teorico
                            SessionBalancesHolder sessionBalanceCents = _getFastSessionBalancesByTicket(ticket, sessionInfos);
                            long theoricalBalanceTot = sessionBalanceCents.amountTotal + recvTotInt;
                            int remFr = sessionBalanceCents.lastRemFr;

                            // target state
                            targetStateFinal = _useDenormThreads ? (int)CasinoMovimentiBuffer.States.Dumped : (int)CasinoMovimentiBuffer.States.Committed;
                            targetState = isSeamlessExternal ? (int)CasinoMovimentiBuffer.States.PreDumped : targetStateFinal;

                            if (CcConfig.AppSettingBool("TEMP_FIX_CASAM_RESULTOKRESP_PRE01", true))
                                okResp.balance = theoricalBalanceTot;

                            // relazione con stake (se presente) per roundId
                            CasinoMovimentiBuffer relatedOp = null; 
                            List<CasinoMovimentiBuffer> openDebits = new List<CasinoMovimentiBuffer>();
                            List<long> openDebitsRelated = new List<long>();
                            int openDebitsNotRelated = 0;

                            if (hasRoundRef)
                                relatedOp = _getStakeByRef(sessionInfos, roundRef);

                            if (relatedOp != null)
                            {
                                SmartHash openLoadAux = new SmartHash();
                                openDebits = _getOpenDebits("credit", sessionInfos, openLoadAux);
                                for (int i = 0; i < openDebits.Count; i++)
                                    if ((openDebits[i].CMB_RELATED_FK_CMB_ID == relatedOp.CMB_ID && openDebits[i].CMB_EXTROUNDREF == relatedOp.CMB_EXTROUNDREF) || openDebits[i].CMB_ID == relatedOp.CMB_ID)
                                        openDebitsRelated.Add(openDebits[i].CMB_ID);
                                openDebitsNotRelated = openDebits.Count - openDebitsRelated.Count;
                            }
                            else
                            {
                                //20200925 lucab for the moment mandatory then we will see
                                if (CcConfig.AppSettingBool("CASINOAM_STAKE_MANDATORY_FORWIN", true))
                                {
                                    targetState = (int)CasinoMovimentiBuffer.States.Deleted;
                                    targetStatus = Cam_ResponseStatus.RS_403_Forbidden;
                                    response = new Cam_DepositResponse
                                    {
                                        responseCodeReason = ((int)targetStatus).ToString()
                                    };
                                }

                                if (CcConfig.AppSettingBool("CASINOAM_WIN_WITHOUT_STAKE_ALERT", true))
                                {
                                    try
                                    {
                                        AlertMgr.def.sendAlertV2(
                                            AlertMgr.Target.CASINO_EngineError, new[]
                                            {
                                                "CASINOAM WIN Without Stake",
                                                "Received winning witout stake:</br></br>" +
                                                "User: " + local_eu.EU_UID + "<br/>" +
                                                "MATCH_Id: " + MATCH_Id + "<br/>" +
                                                "TXId: " + transactionId + "<br/>" +
                                                "AMOUNT: " + rqst.amount / 10 + "<br/>" +
                                                "DATE: " + utcNow + "<br/>"
                                            });
                                    }
                                    catch (Exception logContinue)
                                    {
                                        Log.exc(logContinue);
                                    }
                                }
                            }

                            bool roundClosed = (openDebitsNotRelated == 0 && !multibetLiveFlow) || forceRoundClose; 
                            int roundClosedOnCmb = (int)CasinoMovimentiBuffer.Closed.Opened; 

                            if (CcConfig.AppSettingBool("TEMP_CAM_FIX_ROUNDCLOSE_RESULT", false) && roundClosed)
                                roundClosedOnCmb = (int)CasinoMovimentiBuffer.Closed.Closed;

                            // Crea sempre il movimento
                            CasinoMovimentiBuffer newMov = new CasinoMovimentiBuffer
                            {
                                CMB_SERVICE = (int)FinMovExtern.Servizi.CasinoAM,
                                CMB_FK_FST_MATCH_Id = MATCH_Id,
                                CMB_FK_CURR_MATCH_Id = MATCH_Id,
                                CMB_FK_EU_Id = centEuId,
                                CMB_TYPE = (int)cmbType,
                                CMB_STATE = targetState,
                                CMB_CLOSED = (int)CasinoMovimentiBuffer.Closed.Opened,
                                CMB_AMOUNT_PLAYBONUS = funBonusSession ? Convert.ToInt32(recvTotInt) : 0,
                                CMB_EXTTXID = transactionId,
                                CMB_EXTTXREFID = null,
                                CMB_OPDATE_UTC = utcNow,
                                CMB_CREATEDAT = utcNow.ToLocalTime(),
                                CMB_RESULT = ((int)targetStatus).ToString(),
                                CMB_RECVTEXT = rawRequest,
                                CMB_SENTTEXT = JsonConvert.SerializeObject(response),
                                CMB_EXTROUNDREF = roundRef,
                                CMB_PROGR = sessionBalanceCents.lastProgr + 1,
                                CMB_SW_EXTTOKEN = sessionInfos.getTypedValue("EXTTOKEN", string.Empty, false),
                                CMB_FK_USER_ID = OrgUserMgr.def.GetFirstMaster().GetCentralUser().USER_ID,
                                CMB_EXTCODE = extCode
                            };
                            if (longSupportStage <= 2)
                            {
                                newMov.CMB_AMOUNT_TOT = Convert.ToInt32(recvTotInt);
                                newMov.CMB_AMOUNT_BONUS = funBonusSession ? Convert.ToInt32(recvTotInt) : 0;
                                newMov.CMB_AMOUNT_WITH = 0;
                            }
                            if (longSupportStage >= 1)
                            {
                                newMov.CMB_AMOUNT_TOT_LONG = recvTotInt;
                                newMov.CMB_AMOUNT_BONUS_LONG = funBonusSession ? recvTotInt : 0;
                                newMov.CMB_AMOUNT_WITH_LONG = 0;
                            }

                            long relatedGrdId = -1;
                            long firstRelatedGrdId = -1;
                            if (relatedOp != null)
                            {
                                bool groupRelatedBets = CcConfig.AppSettingBool("CASINOAM_MULIPLE_ROUND_BETS_GROUP", true);
                                //newMov.CMB_EXTROUNDREF = relatedOp.CMB_EXTROUNDREF;  ---> inutile, la ricerca del related avviene per roundref quindi hanno perforza lo stesso
                                newMov.CMB_EXTTXREFID = relatedOp.CMB_EXTTXID;
                                relatedGrdId = relatedOp.CMB_ID;
                                firstRelatedGrdId = relatedOp.CMB_RELATED_FK_CMB_ID > 0 ? relatedOp.CMB_RELATED_FK_CMB_ID : relatedGrdId;
                                newMov.CMB_RELATED_FK_CMB_ID = groupRelatedBets ? firstRelatedGrdId : relatedGrdId;
                            }

                            int createRes;
                            using (new GGConnMgr.CentralCtxWrap("CASINO"))
                            {
                                createRes = newMov.Create(author);
                                cmbId = newMov.CMB_ID;
                            }
                            if (createRes > 0)
                            {
                                okResp.casinoTransferId = CcConfig.AppSettingStr("CASINOAM_TRANSACTION_REFERENCE_ENV", "BG") + (Convert.ToString(newMov.CMB_ID, 16).PadLeft(10, '0'));

                                if (targetStatus == Cam_ResponseStatus.RS_200_Success)
                                {
                                    bool allOk;
                                    bool needUpdate = true;
                                    bool relatedClosed = false;
                                    CasinoMovimentiBuffer newMovUpd = new CasinoMovimentiBuffer { CMB_ID = newMov.CMB_ID };

                                    if (isSeamlessExternal)
                                    {
                                        int roundRefExt = newMov.CMB_PROGR;
                                        int dumpedStateFinal = targetStateFinal;

                                        bool makeCreditOnZeroRoundClose = CcConfig.AppSettingBool("CASINOAM_MAKE_ZEROCREDIT", true);

                                        CasGameState casGameState = new CasGameState(
                                            ExtIntMgr.PlatformKeys.CASINOAM, ticket, newMov, funBonusSession, sessionInfos);

                                        //Log.getLogger("GiovanniDebug").BetaDebug("relatedop = " + JsonConvert.SerializeObject(relatedOp));

                                        //Log.getLogger("GiovanniDebug").BetaDebug("relatedopGrdId = " + relatedGrdId + " firstRelatedGrdId = " + firstRelatedGrdId);

                                        HashResult extRes = CBSLWalletMgr.def.makeExternalTransactions(
                                            newMov.CMB_ID, euId, casGameState,
                                            new HashParams(
                                                "fstMatchId", MATCH_Id,
                                                "opType", "" + cmbType,
                                                "progr", roundRefExt,
                                                "extToken", newMov.CMB_SW_EXTTOKEN,
                                                "CasinoSessionId", sessionInfos.getTypedValue("SWTOKEN", string.Empty, false),
                                                "GameMode", funBonusSession ? "FUN" : "CASH",
                                                "GameId", sessionInfos.getTypedValue("GAME_KEY", extCode, false),
                                                "roundRef", roundRef,
                                                "freeRoundSession", false,
                                                "freeRounds", 0,
                                                "freeRoundVal", 0,
                                                "roundClosed", roundClosed,
                                                "spinType", (int)cmbType,
                                                "relatedGrdId", firstRelatedGrdId,
                                                "firstRelatedGrdId", firstRelatedGrdId,
                                                "grdState", newMov.CMB_STATE,
                                                "dumpedState", dumpedStateFinal,
                                                "opDateUtc", newMov.CMB_OPDATE_UTC,
                                                "opId", newMov.CMB_ID,
                                                "GameName", sessionInfos.getTypedValue("GAME_NAME", string.Empty),
                                                "checkBalance", CcConfig.AppSettingBool("CASINOAM_EXTBALANACE_UPDATE", true),
                                                "makeCreditOnZeroRoundClose", makeCreditOnZeroRoundClose,
                                                "makeDebitOnZeroRoundClose", false,
                                                "autoClose", CcConfig.AppSettingBool("CASINOAM_SEAMLESS_WALLET_AUTOCLOSE", false),
                                                "service", (int)FinMovExtern.Servizi.CasinoAM,
                                                "partition", partition
                                            ));

                                        bool cgrStateCommitted = extRes.ContainsKey("cgrStateCommitted") && (bool)extRes["cgrStateCommitted"];
                                        bool cgrIsRetrying = extRes.ContainsKey("cgrIsRetrying") && (bool)extRes["cgrIsRetrying"];
                                        if (extRes.IsOk)
                                        {
                                            long newBalance = theoricalBalanceTot;
                                            if (extRes.ContainsKey("newBalance"))
                                            {
                                                newBalance = (long)extRes["newBalance"];
                                                sessionBalanceCents.amountTotal = newBalance;
                                                if (CcConfig.AppSettingBool("CASINOAM_SW_SETNEWBALANCE", true))
                                                    _setFastSessionBalancesByTicket(ticket, sessionBalanceCents, sessionInfos);
                                            }

                                            okResp.balance = newBalance;
                                            response = okResp;

                                            if (!cgrStateCommitted)
                                            {
                                                newMovUpd.CMB_STATE = targetStateFinal;
                                            }
                                            newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);

                                            allOk = true;
                                            auxPars["op"] = newMov;
                                        }
                                        else
                                        {
                                            allOk = false;

                                            string low = ("" + extRes["txResErrorMessage"]).ToLowerInvariant();
                                            bool shouldBeTried = extRes.ErrorMessage.Contains("EXEC_UNKNOWN");
                                            if (shouldBeTried)
                                            {
                                                //allways retried internally
                                                if (cgrIsRetrying)
                                                {
                                                    //mattiab okResp status is already Cpn_ResponseStatus.RS_000_Success
                                                    response = okResp;
                                                    //mattiab response.cash should already be set to theoricalBalanceTot, but overwrite it to be sure and make it explicit
                                                    response.balance = theoricalBalanceTot;
                                                }
                                                else
                                                {
                                                    //should not happen
                                                    targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                                                    response = new Cam_DepositResponse
                                                    {
                                                        //AMUSNET RICHIEDE SEMPRE IL BALANCE ANCHE SE LA TRANSAZIONE è FALLITA
                                                        balance = (long)sessionBalanceCents.amountTotal,
                                                        errorMessage = string.IsNullOrEmpty(low) ? "External wallet timeout" : low,
                                                        responseCodeReason = ((int)targetStatus).ToString(),
                                                    };

                                                    if (CcConfig.AppSettingBool("CASINOAM_RETRYFLOW_ERROR_ALERT", true))
                                                        AlertMgr.def.sendAlertV2(
                                                            AlertMgr.Target.CASINO_EngineError, new[]
                                                            {
                                                            "CasinoAM Internal Wallet Error ** ALERT NOT RETRYING **",
                                                            "Received msg but internal wallet is not retrying:</br></br>" +
                                                            "User: " + local_eu.EU_UID + "<br/>" +
                                                            "TXId: " + transactionId + "<br/>" +
                                                            "AMOUNT: " + rqst.amount + "<br/>" +
                                                            "DATE: " + utcNow + "<br/>"
                                                            });
                                                }
                                            }
                                            else
                                            {
                                                targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                                                response = new Cam_DepositResponse
                                                {
                                                    //AMUSNET RICHIEDE SEMPRE IL BALANCE ANCHE SE LA TRANSAZIONE è FALLITA
                                                    balance = (long)sessionBalanceCents.amountTotal,
                                                    errorMessage = string.IsNullOrEmpty(low) ? "External wallet timeout" : low,
                                                    responseCodeReason = ((int)targetStatus).ToString(),
                                                };
                                            }

                                            if (!cgrStateCommitted)
                                                newMov._CMB_STATE = newMovUpd.CMB_STATE = (int)CasinoMovimentiBuffer.States.Deleted;
                                            newMov._CMB_RESULT = ((int)targetStatus).ToString();
                                            newMov._CMB_SENTTEXT = newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                        }
                                    }
                                    else
                                    {
                                        auxPars["op"] = newMov;

                                        SessionBalancesHolder finalBalancesCents = _getFastSessionBalancesByTicket(ticket, sessionInfos, new HashParams("reload", true));
                                        if (finalBalancesCents.amountTotal >= 0)
                                        {
                                            allOk = true;
                                            okResp.casinoTransferId = CcConfig.AppSettingStr("CASINOAM_TRANSACTION_REFERENCE_ENV", "AM") +
                                                                      Convert.ToString(newMov.CMB_ID, 16).PadLeft(10, '0');
                                            okResp.balance = (long)finalBalancesCents.amountTotal;
                                            response = okResp;
                                            newMov._CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                        }
                                        else
                                        {
                                            allOk = false;
                                            targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                                            response = new Cam_DepositResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "Final balance < 0" };
                                            newMov._CMB_STATE = (int)CasinoMovimentiBuffer.States.Deleted;
                                            newMov._CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                            newMov._CMB_RESULT = ((int)targetStatus).ToString();
                                        }
                                    }

                                    bool shouldCloseRelated = allOk && (!multibetLiveFlow || forceRoundClose);
                                    bool shouldUpdateMov = needUpdate;
                                    bool shouldCloseRound = allOk && _useDenormThreads && roundClosed && CcConfig.AppSettingBool("CASINOAM_CLOSE_ROUND_ONWIN", true);

                                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                    {
                                        if (shouldUpdateMov)
                                        {
                                            newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                            PatternLib.def.RetryAction(5, 2000, () => newMovUpd.Update(author), new HashParams("logContinue", true));
                                        }
                                        _pingTableSession_Central(sessionInfos, author);
                                    }

                                    _setSingleOperationInCache(sessionInfos, transactionId, newMov);

                                    if (shouldCloseRound)
                                    {
                                        HashResult closeRes = _closeRound(relatedOp);
                                        if (!closeRes.IsOk)
                                        {
                                            response = new Cam_DepositResponse { responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString() };
                                        }
                                        else
                                        {
                                            relatedClosed = true;
                                        }
                                    }

                                    if (relatedClosed && openDebitsRelated.Count > 0)
                                    {
                                        for (int i = openDebits.Count - 1; i >= 0; i--)
                                        {
                                            if (openDebitsRelated.Contains(openDebits[i].CMB_ID))
                                                openDebits.RemoveAt(i);
                                        }
                                        _setOpenDebits("credit", sessionInfos, openDebits);
                                    }
                                }
                            }
                            else throw new Exception("MANAGED::99xINTERNAL_ERROR_RETRY");
                        }
                        else
                        {
                            retryOp = checkOp;
                        }
                    }

                    #region RESEND
                    if (retryOp != null)
                    {
                        response = JsonConvert.DeserializeObject<Cam_DepositResponse>(retryOp.CMB_SENTTEXT);
                        if (currSession != null && !String.IsNullOrEmpty(currSession.getTypedValue("ticket", string.Empty, true)))
                        {
                            if (response != null && CcConfig.AppSettingBool("CASINOAM_ACTUAL_BALANCE_ON_RESEND", true))
                            {
                                //string ticket = currSession.getTypedValue("ticket", string.Empty, false);
                                //HashParams sessionInfos = _getSessionInfosByTicket(ticket, local_eu, transactionId, methName);
                                SessionBalancesHolder sessionBalanceCents = _getFastSessionBalancesByTicket(ticket, sessionInfos);
                                response.balance = sessionBalanceCents.amountTotal;
                            }
                        }

                        try
                        {
                            targetStatus = (Cam_ResponseStatus)int.Parse("" + retryOp.CMB_RESULT);
                        }
                        catch (Exception logContinue)
                        {
                            Log.exc(logContinue);
                            targetStatus = Cam_ResponseStatus.RS_5xx_InternalServerError;
                        }

                        _LogDebug(30, "Resending", new EndUser { EU_ID = euId }, transactionId, methName);
                        auxPars["_isRetransmission"] = true;
                    }
                    #endregion

                    auxPars["_cmbId"] = cmbId;
                    auxPars["_partition"] = partition;
                    auxPars["_matchId"] = MATCH_Id;
                    auxPars["_opDateUtc"] = opDateUtc.ToString("yyyyMMddHHmmss");
                    auxPars["_targetStatus"] = targetStatus;
                }
                catch (Exception exc)
                {
                    if (exc.Message.StartsWith("MANAGED::")) throw;
                    Log.exc(exc);
                    throw new Exception("MANAGED::99xINTERNAL_ERROR_RETRY");
                }
                finally
                {
                    if (monitor != null)
                        _releaseMonitor(monitor);
                }
            }
            #endregion

            // === RETURN UNICO ========================================================
            return response;
        }

        #endregion deposit 

        #region rollBack

        public virtual Cam_DepositResponse _CasinoAM_RollBack(int euId, HashParams auxPars)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            const _MethNames methName = _MethNames.Cam_RollBack;
            DateTime utcNow = DateTime.UtcNow;

            #region HEADER & INPUT

            // === REQUEST ============================================================
            Cam_DepositRequest rqst = new Cam_DepositRequest
            {
                playerId = auxPars.getTypedValue("playerId", ""),
                transferId = auxPars.getTypedValue("transferId", ""),
                gameNumber = auxPars.getTypedValue("gameNumber", ""),
                gameId = auxPars.getTypedValue("gameId", ""),
                sessionId = auxPars.getTypedValue("sessionId", ""),
                portalCode = auxPars.getTypedValue("portalCode", ""),
                platformType = auxPars.getTypedValue("platformType", "")
            };

            // === RESPONSES ==========================================================
            Cam_DepositResponse okResp = new Cam_DepositResponse
            {
                balance = 0,
                casinoTransferId = "",
                responseCodeReason = ((int)Cam_ResponseStatus.RS_200_Success).ToString()
            };
            Cam_DepositResponse response = new Cam_DepositResponse
            {
                responseCodeReason = ((int)Cam_ResponseStatus.RS_5xx_InternalServerError).ToString(),
                errorMessage = "UNKNOWN_ERROR"
            };

            // === CONTEXT ============================================================
            EndUser local_eu = new EndUser { EU_ID = euId, useNoLockWhePossible = true };
            local_eu.GetById();
            auxPars["_local_eu"] = local_eu;

            string transactionId = rqst.transferId;
            string rawRequest = auxPars.getTypedValue<string>("rawRequest", null, false);
            string extCode = (rqst.gameId ?? "") + " - " + (rqst.sessionId ?? "");
            string roundRef = rqst.gameNumber;
            string author = auxPars.getTypedValue("author", typeof(CasinoExtIntAMSWCoreTest).Name);

            // Multisessione: prova a ricavare il game dalla related
            string restoredGameEiKey = string.Empty;
            if (CcConfig.AppSettingBool("CASINOAM_MULTISESSION_ENABLE", false))
            {
                try
                {
                    EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), unl = true };
                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    {
                        centEu.getByUserId();
                        CasinoMovimentiBuffer refBetOp = CasinoSessionMgr.def.BUFFER_GetAll(
                            new HashParams(
                                "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                                "CMB_FK_EU_Id", centEu.EU_ID,
                                "CMB_STATEs", new[] {
                            (int)CasinoMovimentiBuffer.States.Dumped,
                            (int)CasinoMovimentiBuffer.States.PreCommitted,
                            (int)CasinoMovimentiBuffer.States.Committed,
                            (int)CasinoMovimentiBuffer.States.Deleted,
                            (int)CasinoMovimentiBuffer.States.Completed
                                },
                                "CMB_EXTROUNDREF", roundRef
                            )
                        ).FirstOrDefault();

                        if (refBetOp != null && refBetOp.CMB_ID > 0)
                        {
                            CasinoSubscription fstSub = new CasinoSubscription() { MATCH_Id = refBetOp.CMB_FK_FST_MATCH_Id, unl = true };
                            fstSub.GetById();
                            if (fstSub.flagRecordFound)
                            {
                                CasinoSession casinoSession = fstSub.getReferenceSession(true);
                                if (casinoSession.flagRecordFound)
                                {
                                    CasinoTableDef tDef = casinoSession.getReferenceCasinoTableDef(true);
                                    if (tDef.flagRecordFound)
                                    {
                                        Games refGame = tDef.getReferenceGame(true);
                                        if (refGame.flagRecordFound)
                                            restoredGameEiKey = refGame.GAME_EI_KEY;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception logContinue) { Log.exc(logContinue); }
            }

            // Stato obiettivo
            Cam_ResponseStatus targetStatus = Cam_ResponseStatus.RS_5xx_InternalServerError;
            CasinoMovimentiBuffer.Type cmbType = CasinoMovimentiBuffer.Type.LooseCancel;
            CasinoMovimentiBuffer.Type relatedCmbType = CasinoMovimentiBuffer.Type.Loose;

            bool skipMainFlow = false;

            #endregion HEADER & INPUT

            #region PRECHECK REQUEST VALIDATION

            HashParams currSession = _getCurrentSessionInfo(euId, restoredGameEiKey.Length > 0 ? restoredGameEiKey : rqst.gameId, auxPars);
            string ticket = currSession.getTypedValue("ticket", string.Empty, false);

            HashParams sessionInfos = _getSessionInfosByTicket(ticket, local_eu, transactionId, methName);

            int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
            int centEuId = sessionInfos.getTypedValue("centEU_Id", -1, false);
            bool funBonusSession = sessionInfos.getTypedValue("CSES_PLAYBONUS_TABLE", 0, false) > 0;

            // Parametri minimi e coerenza playerId
            if (rqst.playerId.Length == 0 || transactionId.Length == 0 || roundRef.Length == 0 || rqst.sessionId.Length == 0)
            {
                skipMainFlow = true;
                targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                response = new Cam_DepositResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "BAD_REQUEST" };
            }
            if (!skipMainFlow && !rqst.playerId.Equals(CcConfig.AppSettingStr("CASINOEXTINTAM_ENVIRONMENT", "AD") + "@" + local_eu.EU_UID + (funBonusSession ? "@FUN" : "@CASH"), StringComparison.Ordinal))
            {
                skipMainFlow = true;
                targetStatus = Cam_ResponseStatus.RS_403_Forbidden;
                response = new Cam_DepositResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "Player ID mismatch" };
            }

            #endregion PRECHECK REQUEST VALIDATION

            #region PRECHECK RETRYOP

            CasinoMovimentiBuffer retryOp = null;
            try
            {
                EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), unl = true };
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    centEu.getByUserId();
                    retryOp = CasinoSessionMgr.def.BUFFER_GetAll(
                        new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_FK_EU_Id", centEu.EU_ID,
                            "CMB_STATEs", new[] {
                        (int)CasinoMovimentiBuffer.States.Dumped,
                        (int)CasinoMovimentiBuffer.States.PreCommitted,
                        (int)CasinoMovimentiBuffer.States.Committed,
                        (int)CasinoMovimentiBuffer.States.Deleted,
                        (int)CasinoMovimentiBuffer.States.Completed
                            },
                            "CMB_EXTTXID", transactionId
                        )
                    ).FirstOrDefault();
                }
            }
            catch (Exception logContinue) { Log.exc(logContinue); }

            #endregion PRECHECK RETRYOP

            #region PRECHECK CURRENT SESSION

            
            if (currSession == null || currSession.Count == 0)
            {
                if (retryOp == null)
                {
                    // come HS: soft-ok per idempotenza lato chiamante
                    targetStatus = Cam_ResponseStatus.RS_200_Success;
                    response = new Cam_DepositResponse
                    {
                        responseCodeReason = ((int)targetStatus).ToString(),
                        balance = _getFastSessionBalancesByTicket(auxPars.getTypedValue("ticket", "", false), new HashParams()).amountTotal,
                        errorMessage = "INVALID_SESSION"
                    };
                    return response;
                }
            }

            #endregion PRECHECK CURRENT SESSION

            #region MAIN FLOW

            if (!skipMainFlow)
            {
                HashParams monitor = null;
                try
                {
                    if (retryOp == null)
                    {
                        bool multibetLiveFlow = CcConfig.AppSettingBool("CASINOAM_ENABLE_MULTIBET_LIVE_FLOW", true) &&
                                               (sessionInfos.getTypedValue("GAME_ISLIVE", 0) == 1 ||
                                                CcConfig.AppSettingStr("CASINOAM_MULTIBET_RNG_GAMES", "#").Contains("#" + sessionInfos.getTypedValue("GAME_EI_KEY", "XXXX") + "#"));

                        bool isSeamlessExternal = useSeamlessExternalIntegration("refund", sessionInfos);

                        monitor = _acquireMonitor(ticket);

                        CasinoMovimentiBuffer checkOp = _getSingleOperation(sessionInfos, transactionId);
                        if (checkOp == null)
                        {
                            SessionBalancesHolder sessionBalanceCents = _getFastSessionBalancesByTicket(ticket, sessionInfos);

                            int targetState;
                            int targetStateRelated = -1;
                            int targetStateFinal = (int)CasinoMovimentiBuffer.States.Deleted;

                            targetStatus = Cam_ResponseStatus.RS_200_Success;
                            response = okResp;

                            long relatedOpAmount = 0;
                            long relatedOpPlayBonusAmount = 0;
                            string relatedOpExtRoundRef = string.Empty;

                            CasinoMovimentiBuffer relatedOp = _getStakeByRef(sessionInfos, roundRef);
                            long relatedRes = relatedOp != null ? (long)relatedOp.CMB_ID : -1;

                            List<CasinoMovimentiBuffer> openDebits = new List<CasinoMovimentiBuffer>();
                            List<long> openDebitsRelated = new List<long>();
                            int openDebitsNotRelated = 0;
                            bool _useDenormThreads = CcConfig.AppSettingBool("CASINOAM_USE_DENORM_THREADS", true);

                            bool createFutureBet = false;
                            if (relatedOp == null)
                            {
                                // related mancante: tengo CANCEL “pronto” e creo placeholder stake se serve
                                targetState = targetStateFinal = (int)CasinoMovimentiBuffer.States.Deleted;
                                relatedOpAmount = 0;                  // importo lo deduco dalla related quando arriverà
                                relatedOpExtRoundRef = roundRef;      // (vuoto qui)
                                createFutureBet = true;
                            }
                            else
                            {
                                bool looseCancelCheckFailed = relatedOp.CMB_STATE == (int)CasinoMovimentiBuffer.States.Committed;
                                if (looseCancelCheckFailed)
                                {
                                    targetState = (int)CasinoMovimentiBuffer.States.Deleted;
                                    targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                                    response = new Cam_DepositResponse
                                    {
                                        responseCodeReason = ((int)targetStatus).ToString(),
                                        errorMessage = "Related bet not found/invalid",
                                        balance = sessionBalanceCents.amountTotal
                                    };
                                }
                                else
                                {
                                    int deleteState = (int)CasinoMovimentiBuffer.States.Deleted;
                                    targetStateRelated = targetStateFinal = deleteState;
                                    targetState = isSeamlessExternal
                                        ? (int)CasinoMovimentiBuffer.States.PreDumpDeleted
                                        : targetStateFinal;

                                    relatedOpAmount = longSupportStage <= 1 ? relatedOp.CMB_AMOUNT_TOT : relatedOp.CMB_AMOUNT_TOT_LONG;
                                    relatedOpPlayBonusAmount = relatedOp.CMB_AMOUNT_PLAYBONUS;
                                    relatedOpExtRoundRef = relatedOp.CMB_EXTROUNDREF;

                                    openDebits = _getOpenDebits("refund", sessionInfos, new SmartHash());
                                    for (int i = 0; i < openDebits.Count; i++)
                                        if ((openDebits[i].CMB_RELATED_FK_CMB_ID == relatedOp.CMB_ID &&
                                             openDebits[i].CMB_EXTROUNDREF == relatedOp.CMB_EXTROUNDREF) ||
                                            openDebits[i].CMB_ID == relatedOp.CMB_ID)
                                            openDebitsRelated.Add(openDebits[i].CMB_ID);
                                    openDebitsNotRelated = openDebits.Count - openDebitsRelated.Count;
                                }
                            }

                            int remFr = sessionBalanceCents.lastRemFr;
                            bool roundClosed = openDebitsNotRelated == 0 && !multibetLiveFlow;
                            int roundClosedOnCmb = (int)CasinoMovimentiBuffer.Closed.Opened;
                            if (CcConfig.AppSettingBool("CASINOAM_REFUND_CLOSED_FLAG", false) && roundClosed)
                                roundClosedOnCmb = (int)CasinoMovimentiBuffer.Closed.Closed;

                            // Se la related non esiste ancora e vuoi tracciare lo stake “futuro”
                            if (createFutureBet)
                            {
                                relatedOp = new CasinoMovimentiBuffer
                                {
                                    CMB_SERVICE = (int)FinMovExtern.Servizi.CasinoAM,
                                    CMB_FK_FST_MATCH_Id = MATCH_Id,
                                    CMB_FK_CURR_MATCH_Id = MATCH_Id,
                                    CMB_FK_EU_Id = centEuId,
                                    CMB_TYPE = (int)relatedCmbType,
                                    CMB_STATE = targetState,
                                    CMB_CLOSED = (short)roundClosedOnCmb,
                                    CMB_AMOUNT_PLAYBONUS = funBonusSession ? Convert.ToInt32(relatedOpPlayBonusAmount) : 0,
                                    CMB_EXTTXID = transactionId + "_fake",
                                    CMB_EXTTXREFID = null,
                                    CMB_OPDATE_UTC = utcNow,
                                    CMB_CREATEDAT = utcNow.ToLocalTime(),
                                    CMB_RESULT = ((int)targetStatus).ToString(),
                                    CMB_RECVTEXT = rawRequest,
                                    CMB_SENTTEXT = JsonConvert.SerializeObject(okResp),
                                    CMB_EXTROUNDREF = relatedOpExtRoundRef,
                                    CMB_PROGR = sessionBalanceCents.lastProgr + 1,
                                    CMB_SW_EXTTOKEN = sessionInfos.getTypedValue("EXTTOKEN", string.Empty, false),
                                    CMB_FK_USER_ID = OrgUserMgr.def.GetFirstMaster().GetCentralUser().USER_ID,
                                    CMB_EXTCODE = extCode,
                                    CMB_REM_FR = remFr == 0 ? remFr : remFr - 1
                                };
                                if (longSupportStage <= 2)
                                {
                                    relatedOp.CMB_AMOUNT_TOT = Convert.ToInt32(relatedOpAmount);
                                    relatedOp.CMB_AMOUNT_BONUS = funBonusSession ? Convert.ToInt32(relatedOpPlayBonusAmount) : 0;
                                    relatedOp.CMB_AMOUNT_WITH = 0;
                                }
                                if (longSupportStage >= 1)
                                {
                                    relatedOp.CMB_AMOUNT_TOT_LONG = relatedOpAmount;
                                    relatedOp.CMB_AMOUNT_BONUS_LONG = funBonusSession ? relatedOpPlayBonusAmount : 0;
                                    relatedOp.CMB_AMOUNT_WITH_LONG = 0;
                                }

                                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                    relatedRes = relatedOp.Create(author);
                            }

                            // Crea SEMPRE il movimento CANCEL
                            CasinoMovimentiBuffer newMov = new CasinoMovimentiBuffer
                            {
                                CMB_SERVICE = (int)FinMovExtern.Servizi.CasinoAM,
                                CMB_FK_FST_MATCH_Id = MATCH_Id,
                                CMB_FK_CURR_MATCH_Id = MATCH_Id,
                                CMB_FK_EU_Id = centEuId,
                                CMB_TYPE = (int)cmbType,
                                CMB_STATE = targetState,
                                CMB_CLOSED = (short)roundClosedOnCmb,
                                CMB_AMOUNT_PLAYBONUS = funBonusSession ? Convert.ToInt32(relatedOpPlayBonusAmount) : 0,
                                CMB_EXTTXID = transactionId,
                                CMB_EXTTXREFID = relatedOp != null ? relatedOp.CMB_EXTTXID : "impossibile",
                                CMB_OPDATE_UTC = utcNow,
                                CMB_CREATEDAT = utcNow.ToLocalTime(),
                                CMB_RESULT = ((int)targetStatus).ToString(),
                                CMB_RECVTEXT = rawRequest,
                                CMB_SENTTEXT = JsonConvert.SerializeObject(response),
                                CMB_EXTROUNDREF = relatedOpExtRoundRef,
                                CMB_PROGR = sessionBalanceCents.lastProgr + 1,
                                CMB_SW_EXTTOKEN = sessionInfos.getTypedValue("EXTTOKEN", string.Empty, false),
                                CMB_FK_USER_ID = OrgUserMgr.def.GetFirstMaster().GetCentralUser().USER_ID,
                                CMB_EXTCODE = extCode,
                                CMB_REM_FR = remFr
                            };
                            if (longSupportStage <= 2)
                            {
                                newMov.CMB_AMOUNT_TOT = Convert.ToInt32(relatedOpAmount);
                                newMov.CMB_AMOUNT_BONUS = funBonusSession ? Convert.ToInt32(relatedOpPlayBonusAmount) : 0;
                                newMov.CMB_AMOUNT_WITH = 0;
                            }
                            if (longSupportStage >= 1)
                            {
                                newMov.CMB_AMOUNT_TOT_LONG = relatedOpAmount;
                                newMov.CMB_AMOUNT_BONUS_LONG = funBonusSession ? relatedOpPlayBonusAmount : 0;
                                newMov.CMB_AMOUNT_WITH_LONG = 0;
                            }

                            if (relatedOp != null && relatedRes > 0)
                            {
                                newMov.CMB_RELATED_FK_CMB_ID = relatedOp.CMB_ID;
                                newMov.CMB_EXTROUNDREF = relatedOp.CMB_EXTROUNDREF;
                            }

                            int createRes;
                            using (new GGConnMgr.CentralCtxWrap("CASINO"))
                            {
                                createRes = newMov.Create(author);
                                if (createRes > 0 && targetStateRelated > 0 && relatedRes > 0)
                                {
                                    // pre-delete della related (stake) subito
                                    CasinoMovimentiBuffer cmbUpd = new CasinoMovimentiBuffer { CMB_ID = relatedOp.CMB_ID };
                                    cmbUpd.CMB_STATE = targetStateRelated;
                                    cmbUpd.Update();
                                    targetStateRelated = -1;
                                }
                            }

                            if (createRes > 0)
                            {
                                okResp.casinoTransferId = CcConfig.AppSettingStr("CASINOAM_TRANSACTION_REFERENCE_ENV", "BG") +
                                                          Convert.ToString(newMov.CMB_ID, 16).PadLeft(10, '0');
                                response = okResp;
                                newMov.CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                    PatternLib.def.RetryAction(10, 2000, () => newMov.Update(author), new HashParams());

                                bool allOk;
                                bool needUpdate = true;
                                bool relatedClosed = false;
                                CasinoMovimentiBuffer newMovUpd = new CasinoMovimentiBuffer { CMB_ID = newMov.CMB_ID };

                                if (isSeamlessExternal)
                                {
                                    int roundRefExt = newMov.CMB_PROGR;
                                    if (CcConfig.AppSettingBool("CASINOAM_SEAMLESS_WALLET_ROUNDREF_LOGICAL", false) && relatedOp != null)
                                        roundRefExt = relatedOp.CMB_PROGR;

                                    int dumpedStateFinal = targetStateFinal;
                                    HashResult extRes = new HashResult();
                                    if (!createFutureBet)
                                    {
                                        extRes = CBSLWalletMgr.def.makeExternalTransactions(
                                            newMov.CMB_ID, euId,
                                            new CasGameState(ExtIntMgr.PlatformKeys.CASINOAM, ticket, newMov, funBonusSession, sessionInfos),
                                            new HashParams(
                                                "fstMatchId", MATCH_Id,
                                                "opType", "" + cmbType,
                                                "progr", roundRefExt,
                                                "extToken", newMov.CMB_SW_EXTTOKEN,
                                                "CasinoSessionId", sessionInfos.getTypedValue("SWTOKEN", string.Empty, false),
                                                "GameMode", funBonusSession ? "FUN" : "CASH",
                                                "GameId", sessionInfos.getTypedValue("GAME_KEY", string.Empty, false),
                                                "roundRef", relatedOpExtRoundRef,
                                                "freeRoundSession", false,
                                                "freeRounds", 0,
                                                "freeRoundVal", 0,
                                                "roundClosed", roundClosed,
                                                "spinType", (int)cmbType,
                                                "relatedGrdId", newMov.CMB_RELATED_FK_CMB_ID,
                                                "grdState", newMov.CMB_STATE,
                                                "dumpedState", dumpedStateFinal,
                                                "opDateUtc", newMov.CMB_OPDATE_UTC,
                                                "opId", newMov.CMB_ID,
                                                "GameName", sessionInfos.getTypedValue("GAME_NAME", string.Empty),
                                                "checkBalance", CcConfig.AppSettingBool("CASINOAM_EXTBALANACE_UPDATE", true),
                                                "autoClose", CcConfig.AppSettingBool("CASINOAM_SEAMLESS_WALLET_AUTOCLOSE", false),
                                                "service", (int)FinMovExtern.Servizi.CasinoAM
                                            ));
                                    }

                                    bool cgrStateCommitted = extRes.ContainsKey("cgrStateCommitted") && (bool)extRes["cgrStateCommitted"];
                                    bool cgrIsRetrying = extRes.ContainsKey("cgrIsRetrying") && (bool)extRes["cgrIsRetrying"];

                                    if (extRes.IsOk || createFutureBet)
                                    {
                                        long newBal = sessionBalanceCents.amountTotal;
                                        if (extRes.ContainsKey("newBalance"))
                                        {
                                            newBal = (long)extRes["newBalance"];
                                            sessionBalanceCents.amountTotal = newBal;
                                            if (CcConfig.AppSettingBool("CASINOAM_SW_SETNEWBALANCE", true))
                                                _setFastSessionBalancesByTicket(ticket, sessionBalanceCents, sessionInfos);
                                        }
                                        okResp.balance = newBal;
                                        response = okResp;

                                        if (!cgrStateCommitted)
                                        {
                                            newMovUpd.CMB_STATE = targetStateFinal;
                                            newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                            newMovUpd.CMB_CLOSED = 1;
                                            using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                                PatternLib.def.RetryAction(10, 2000, () => newMovUpd.Update(author), new HashParams());
                                        }
                                        allOk = true;
                                        needUpdate = false;
                                    }
                                    else
                                    {
                                        allOk = false;
                                        int targetState1 = (int)CasinoMovimentiBuffer.States.Deleted;
                                        bool shouldBeTried = extRes.ErrorMessage.Contains("EXEC_UNKNOWN");
                                        if (!shouldBeTried || !cgrIsRetrying)
                                        {
                                            targetState1 = (int)CasinoMovimentiBuffer.States.Invalid;
                                        }

                                        if (!CcConfig.AppSettingBool("TEMP_FIX_CASINOAM_BETFAIL_UPDATE2", true) || !cgrStateCommitted)
                                            newMov._CMB_STATE = newMovUpd.CMB_STATE = targetState1;

                                        targetStatus = Cam_ResponseStatus.RS_5xx_InternalServerError;
                                        response = new Cam_DepositResponse
                                        {
                                            responseCodeReason = ((int)targetStatus).ToString(),
                                            errorMessage = "Error on external wallet",
                                            balance = sessionBalanceCents.amountTotal
                                        };
                                        newMov._CMB_RESULT = newMovUpd.CMB_RESULT = ((int)targetStatus).ToString();
                                        newMov._CMB_SENTTEXT = newMovUpd.CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                    }
                                }
                                else
                                {
                                    // Wallet interno
                                    auxPars["op"] = newMov;
                                    SessionBalancesHolder finalBalancesCents = _getFastSessionBalancesByTicket(ticket, sessionInfos, new HashParams("reload", true));
                                    if (finalBalancesCents.amountTotal >= 0)
                                    {
                                        okResp.casinoTransferId = CcConfig.AppSettingStr("CASINOAM_TRANSACTION_REFERENCE_ENV", "AM") +
                                                                  Convert.ToString(newMov.CMB_ID, 16).PadLeft(10, '0');
                                        okResp.balance = finalBalancesCents.amountTotal;
                                        response = okResp;
                                        newMov._CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                        allOk = true;
                                    }
                                    else
                                    {
                                        allOk = false;
                                        targetStatus = Cam_ResponseStatus.RS_409_Conflict;
                                        response = new Cam_DepositResponse { responseCodeReason = ((int)targetStatus).ToString(), errorMessage = "Final balance < 0" };
                                        newMov._CMB_STATE = (int)CasinoMovimentiBuffer.States.Deleted;
                                        newMov._CMB_SENTTEXT = JsonConvert.SerializeObject(response);
                                        newMov._CMB_RESULT = ((int)targetStatus).ToString();
                                    }

                                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                        PatternLib.def.RetryAction(5, 2000, () => new CasinoMovimentiBuffer { CMB_ID = newMov.CMB_ID, CMB_SENTTEXT = JsonConvert.SerializeObject(response) }.Update(author), new HashParams("logContinue", true));
                                }

                                // Post-step comuni
                                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                {
                                    if (allOk && relatedOp != null && !multibetLiveFlow)
                                    {
                                        CasinoMovimentiBuffer cmbUpd = new CasinoMovimentiBuffer { CMB_ID = relatedOp.CMB_ID };
                                        if (targetStateRelated > 0)
                                            cmbUpd.CMB_STATE = targetStateRelated;
                                        cmbUpd.CMB_CLOSED = (int)CasinoMovimentiBuffer.Closed.Closed;
                                        PatternLib.def.RetryAction(5, 2000, () => cmbUpd.Update(author), new HashParams("logContinue", true));
                                        relatedClosed = true;
                                    }
                                }

                                _setSingleOperationInCache(sessionInfos, transactionId, newMov);
                                //_setFastJackpotAccumulator(local_eu, sessionInfos, null, auxPars);

                                if (CcConfig.AppSettingBool("TEMP_CAM_FIX_OPENDEBS_003", true))
                                {
                                    _setOpenDebits("refund", sessionInfos, null);
                                }
                                else
                                {
                                    if (relatedOp != null && openDebits.Count > 0)
                                    {
                                        for (int i = openDebits.Count - 1; i >= 0; i--)
                                        {
                                            if (openDebitsRelated.Contains(openDebits[i].CMB_ID))
                                                openDebits.RemoveAt(i);
                                        }
                                        _setOpenDebits("refund", sessionInfos, openDebits);
                                    }
                                }

                                if (_useDenormThreads && roundClosed && relatedOp != null && CcConfig.AppSettingBool("CASINOAM_CLOSE_ROUND_ONCANCEL", true))
                                    _closeRound(relatedOp);
                            }
                            else throw new Exception("MANAGED::99xINTERNAL_ERROR_RETRY");
                        }
                        else
                        {
                            // Idempotenza → RESEND
                            retryOp = checkOp;
                        }
                    }

                    #region RESEND
                    if (retryOp != null)
                    {
                        response = JsonConvert.DeserializeObject<Cam_DepositResponse>(retryOp.CMB_SENTTEXT);
                        if (currSession != null && CcConfig.AppSettingBool("CASINOAM_ACTUAL_BALANCE_ON_RESEND", true))
                        {
                            SessionBalancesHolder sessionBalanceCents = _getFastSessionBalancesByTicket(ticket, sessionInfos);
                            response.balance = sessionBalanceCents.amountTotal;
                        }

                        try { targetStatus = (Cam_ResponseStatus)int.Parse("" + retryOp.CMB_RESULT); }
                        catch (Exception logContinue) { Log.exc(logContinue); }

                        _LogDebug(30, "Resending", new EndUser { EU_ID = euId }, transactionId, methName);
                        auxPars["_isRetransmission"] = true;
                    }
                    #endregion RESEND

                    auxPars["_targetStatus"] = targetStatus;
                }
                catch (Exception exc)
                {
                    if (exc.Message.StartsWith("MANAGED::")) throw;
                    Log.exc(exc);
                    throw new Exception("MANAGED::99xINTERNAL_ERROR_RETRY");
                }
                finally
                {
                    if (monitor != null)
                        _releaseMonitor(monitor);
                }
            }

            #endregion MAIN FLOW

            return response;
        }

        #endregion rollBack

        #region closeSession
        public virtual SmartHash _CASINOAM_CloseSession(EndUser local_eu, HashParams auxPars)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            const _MethNames methName = _MethNames.CAM_CloseSession;
            SmartHash response = new SmartHash();

            string author = auxPars.getTypedValue("author", GetType().Name);
            Games locGame = auxPars.getTypedValue<Games>("locGame", null, false);
            string idTransazione = auxPars.getTypedValue("transactionId", string.Empty, false);
            bool skipTransactionCheck = CcConfig.AppSettingBool("CASINOAM_CLOSESESS_NOCHECK_RETRIES", true);
            FinMovExtern checkTran = new FinMovExtern { MOVEXT_EXTTXID = idTransazione, MOVEXT_SERVICE = (int)FinMovExtern.Servizi.CasinoAM };
            checkTran.getByTransactionIdAndService();
            if (skipTransactionCheck && checkTran.flagRecordFound)
            {
                int num = 0;
                string idTransazioneCand = string.Empty;
                while (checkTran.flagRecordFound && num++ < 100)
                {
                    idTransazioneCand = idTransazione + "#" + num;
                    checkTran = new FinMovExtern { MOVEXT_EXTTXID = idTransazioneCand, MOVEXT_SERVICE = (int)FinMovExtern.Servizi.CasinoAM };
                    checkTran.getByTransactionIdAndService();
                }
                if (num == 100)
                    throw new Exception("INTERNAL INCONSISTENCE: MAX NUM APPEND TO TX" + num);
                idTransazione = idTransazioneCand;
            }
            if (!checkTran.flagRecordFound)
            {
                FinMovExtern tran = new FinMovExtern();
                try
                {
                    tran.MOVEXT_EXTTXID = idTransazione;
                    tran.MOVEXT_TIMESTAMP = DateTime.Now;

                    tran.MOVEXT_FK_EU_ID = local_eu.EU_ID;
                    tran.MOVEXT_STATE = (int)FinMovExtern.States.Creata;
                    tran.MOVEXT_TYPE = (int)FinMovExtern.Types.UscitaCasinoDaEsterno;
                    tran.MOVEXT_SERVICE = (int)FinMovExtern.Servizi.CasinoAM;
                    tran.MOVEXT_RAW_RECV_TXT = auxPars.getTypedValue("rawRequest", string.Empty);
                    tran.MOVEXT_RAW_RECV_UTC = DateTime.UtcNow;
                    tran.MOVEXT_OP_DATE = DateTime.Now;
                    tran.Create(author);

                    FinMovExtern tranUpd = new FinMovExtern { MOVEXT_ID = tran.MOVEXT_ID };

                    EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), unl = true };
                    using (new GGConnMgr.CentralCtxWrap("CENTRAL"))
                    {
                        centEu.getByUserId();
                    }

                    bool funBonusSession = false;
                    int fstMatchId = auxPars.getTypedValue("fstMatchId", -1, false);
                    CasinoSession sess = null;
                    CasinoSubscription sub;
                    bool freeRoundSession = false;
                    string ticket = string.Empty;
                    string gameKey = string.Empty;
                    long sitoutCents = -1;
                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    {
                        sub = new CasinoSubscription { MATCH_Id = fstMatchId };
                        sub.GetById();
                        if (sub.flagRecordFound)
                        {
                            sess = sub.referenceSession;
                            funBonusSession = sess.CSES_PLAYBONUS_TABLE > 0;
                            ticket = sub.MATCH_PGA_TICKET;
                            freeRoundSession = funBonusSession && sub.MATCH_FR_START > 0;
                            gameKey = sess.ReferenceCasinoTableDef.getReferenceGame(true).GAME_KEY;
                        }
                    }

                    if (sub.flagRecordFound)
                    {
                        if (CcConfig.AppSettingBool("CASINOAM_SITOUT_CLOSEPENDINGS", false))
                        {
                            completePendingSessions(local_eu, locGame, new HashParams(
                                "fstMatchId", fstMatchId,
                                "centEu", centEu
                            ));
                        }

                        bool continueToWait = true;

                        if (ticket.Length > 0)
                        {
                            bool forceSessionBalance = fstMatchId >= 0 &&
                                                       CcConfig.AppSettingBool(
                                                           "CASINOAM_AUTHENTICATE_USE_LOCALSESSION_BALANCE", false);
                            SessionBalancesHolder counters = _getFastSessionBalancesByTicket(ticket, new HashParams(
                                    "EU_UID", local_eu.EU_UID,
                                    "MATCH_Id", fstMatchId,
                                    "centEU_Id", centEu.EU_ID,
                                    "funSession", funBonusSession,
                                    "freeRoundSession", freeRoundSession,
                                    "GAME_KEY", gameKey
                                ),
                                new HashParams(
                                    "_getLocalBalances_", forceSessionBalance
                                ));
                            if (freeRoundSession)
                                sitoutCents = counters.amountTotalWin;
                            else
                                sitoutCents = counters.amountTotal;
                        }

                        bool firstIter = CcConfig.AppSettingBool("CASINOAM_SYNC_SITOUT_MAKEGAMESITOUT", true);
                        DateTime utcStart = DateTime.UtcNow;
                        while (continueToWait && (DateTime.UtcNow - utcStart).TotalMilliseconds <
                               CcConfig.AppSettingInt("CASINOAM_MAX_SITOUT_WAIT_MSECS", 30000))
                        {
                            HashResult result = CasinoAccountingMgr.def.SignedAssemblyImplementation
                                .Peripheral_Casino_SitOut_Flow2(
                                    CasinoAMSwDispatchTest.AMSWFACEKEY,
                                    local_eu,
                                    centEu,
                                    sess,
                                    BOSessionMgr.def.getCurrentMachine(),
                                    locGame,
                                    author,
                                    new HashParams(
                                        "makeGameSitout", firstIter,
                                        "makeGameSitoutAt",
                                        DateTime.Now.AddSeconds(
                                            CcConfig.AppSettingInt("CASINOAM_MAKE_GAME_AUTO_SITOUT_DELAY_SECS",
                                                300)),
                                        "makeCasinoSitout", firstIter,
                                        "sitoutTotal", sitoutCents,
                                        "casinoPlatform", ExtIntMgr.PlatformKeys.CASINOAM
                                    )
                                );
                            if (result.IsOk)
                            {
                                if (CcConfig.AppSettingBool("CASINOSW_SEAMLESS_WALLET_SEND_ENDSESSION", false))
                                {
                                    string GameMode = "CASH";
                                    string extToken = string.Empty;
                                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                    {
                                        if (sub.flagRecordFound)
                                        {
                                            extToken = "" + CBSLWalletMgr.def
                                                .getActualExtToken("" + sub.MATCH_EXTCASINO_REFS, "").dbToken[0];
                                            if (sub.getReferenceSession(true).CSES_PLAYBONUS_TABLE > 0)
                                                GameMode = "FUN";
                                        }
                                    }

                                    HashParams auxSitoutPars = new HashParams();
                                    auxSitoutPars["casinoPlatform"] = ExtIntMgr.PlatformKeys.CASINOAM;
                                    if (extToken.Length > 0)
                                        auxSitoutPars["extToken"] = extToken;
                                    auxSitoutPars["GameId"] = locGame.GAME_KEY;
                                    auxSitoutPars["GameName"] = locGame.GAME_NAME;
                                    auxSitoutPars["GameExtKey"] = locGame.GAME_EI_KEY;
                                    auxSitoutPars["GameMode"] = GameMode;
                                    auxSitoutPars["fstMatchId"] = fstMatchId;
                                    auxSitoutPars["CasinoSessionId"] =
                                        CasinoBigExtWsAccountMgr.def.Encrypt_CasinoSessionId(
                                            fstMatchId,
                                            GameMode, new HashParams("generateSessionId_fixedVariant", "s"));
                                    auxSitoutPars["extService"] = (int)FinMovExtern.Servizi.CasinoAM;
                                    auxSitoutPars["forceAsyncCall"] = auxPars.getTypedValue("forceAsyncCall", false);
                                    CBSLWalletMgr.def.SessionEndNotification(local_eu.EU_ID, auxSitoutPars);
                                }
                            }

                            continueToWait = !result.IsOk;
                            if (continueToWait)
                            {
                                continueToWait = false;
                                string[] errorForWait = CcConfig
                                    .AppSettingStr("CASINOAM_MAKE_SYNC_SITOUT_ERROR_FOR_WAIT",
                                        "BetsInconsistency,Records to denorm").Split(',');
                                for (int i = 0; i < errorForWait.Length && !continueToWait; i++)
                                    continueToWait = result.ErrorMessage.Contains(errorForWait[i]);
                            }

                            firstIter = false;
                            if (continueToWait)
                                Thread.Sleep(CcConfig.AppSettingInt("CASINOAM_ITERATIONWAIT_ON_SITOUT_MSESC", 200));
                        }
                    }

                    response.IsOk = true;
                    response["status"] = "OK";
                    long saldoContoGioco = 0;
                    long saldoBonus = 0;
                    try
                    {
                        saldoContoGioco = _getSaldo(local_eu, locGame, funBonusSession);
                        saldoBonus = funBonusSession
                            ? 0
                            : (saldoContoGioco -
                               _getSaldo(local_eu, locGame, false, new HashParams("useAccKey", false)));
                    }
                    catch (Exception logCont)
                    {
                        Log.exc(logCont);
                    }

                    tranUpd.MOVEXT_RAW_SENT_TXT = JsonConvert.SerializeObject(response);
                    tranUpd.MOVEXT_RAW_SENT_UTC = DateTime.UtcNow;
                    tranUpd.MOVEXT_RESULT = 0;
                    tranUpd.MOVEXT_RESULTCODE = response.get("status", "");
                    if (longSupportStage <= 2)
                    {
                        tranUpd.MOVEXT_AUX_INT_01 = Convert.ToInt32(saldoContoGioco);
                        tranUpd.MOVEXT_AUX_INT_02 = Convert.ToInt32(saldoBonus);
                    }
                    if (longSupportStage >= 1)
                    {
                        tranUpd.MOVEXT_BIGAMOUNT9 = saldoContoGioco;
                        tranUpd.MOVEXT_BIGAMOUNT8 = saldoBonus;
                    }

                    tranUpd.MOVEXT_STATE = (int)FinMovExtern.States.Ricevuta;
                    PatternLib.def.RetryAction(3, 1000, () => tranUpd.Update(author));
                }
                catch (Exception exc)
                {
                    Log.exc(exc);
                    response = SmartHash.byPP("status", "UNKNOWN_ERROR");
                    _LogDebug(10, string.Format("Non è possibile creare il movimento, EXC:{0}", exc.Message), local_eu, idTransazione, methName);
                    if (tran.MOVEXT_ID > 0)
                    {
                        try
                        {
                            string errDesc = exc.Message;
                            if (errDesc.Length > 50)
                                errDesc = errDesc.Substring(0, 50);
                            FinMovExtern tranUpd2 = new FinMovExtern { MOVEXT_ID = tran.MOVEXT_ID };
                            tranUpd2.MOVEXT_STATE = (int)FinMovExtern.States.InErrore;
                            tranUpd2.MOVEXT_RESULTCODE = errDesc;
                            tranUpd2.MOVEXT_RAW_SENT_TXT = JsonConvert.SerializeObject(response);
                            tranUpd2.MOVEXT_RAW_SENT_UTC = DateTime.UtcNow;
                            tranUpd2.Update(author);
                        }
                        catch (Exception logCont)
                        {
                            Log.exc(logCont);
                        }
                    }
                }
            }
            else
            {
                _LogDebug(200, "Tx Presente -> reinivio", local_eu, idTransazione, methName);
                response = JsonConvert.DeserializeObject<SmartHash>(checkTran.MOVEXT_RAW_SENT_TXT);
                if (response != null)
                    response["retransmission"] = true;
            }
            return response;
        }

        #endregion closeSession 

        #region openSession

        public virtual SmartHash _CASINOAM_OpenSession(EndUser local_eu, HashParams auxPars)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            const _MethNames methName = _MethNames.CAM_OpenSession;
            SmartHash response = new SmartHash();
            bool responseCommittedWithError = false;
            string author = auxPars.getTypedValue("author", GetType().Name);
            bool useIdempotency = auxPars.getTypedValue("useIdempotency", true);

            Games locGame = auxPars.getTypedValue<Games>("locGame", null, false);
            string idTransazione = auxPars.getTypedValue("transactionId", string.Empty, false);
            FinMovExtern checkTran = new FinMovExtern
            {
                MOVEXT_EXTTXID = idTransazione,
                MOVEXT_SERVICE = (int)FinMovExtern.Servizi.CasinoAM
            };
            if (useIdempotency)
                checkTran.getByTransactionIdAndService();

            if (!useIdempotency || !checkTran.flagRecordFound)
            {
                if (!ParamMgr.def.isSubSystemEnable(GameKeys.CASINOAM))
                    throw new Exception("CASINOAM IS DISABLED");

                if (local_eu.IsAutoescluso(EndUser.CategoriaAutoesclusione.Casino))
                {
                    _LogDebug(200, "EU autoescluso", local_eu, idTransazione, methName);
                    response["status"] = "ACCOUNT_LOCKED";
                    responseCommittedWithError = true;
                }

                if (!responseCommittedWithError && !EndUserMgr.def.EndUser_CheckProductEnabled(local_eu, locGame).IsOk)
                {
                    _LogDebug(200, string.Format("EU disabilitato per gioco:{0}", locGame.GAME_KEY), local_eu, idTransazione, methName);
                    response["status"] = "ACCOUNT_LOCKED";
                    responseCommittedWithError = true;
                }

                FinMovExtern tran = new FinMovExtern();
                int IdMov = -9999;
                EndUser centEu = null;
                EndUserExtInt euExt;
                CasinoTable table = null;
                string GameMode = "CASH";
                bool funBonusSession = auxPars.getTypedValue("isFunBonus", false);
                int prefetchedOrRestoredMatchId;
                CasinoSubscription prefetchedOrRestoredSub = null;
                Games newSubGame = null;
                bool isPrefetched = false;
                string prefetchedCasinoSessionId = string.Empty;
                bool sessionStartSent = false;
                string prefetchedExternalTicket = string.Empty;
                string prefetchedExternalSession = string.Empty;
                bool depositOk = false;
                bool sessionRestoreOk = false;

                string _clientType_ = "desktop";
                string csesIdStr = string.Empty;
                string extToken = string.Empty;

                try
                {
                    EndUserLoginNew lastLogin = null;
                    long sitinCents = (long)(auxPars.getTypedValue("sessionBalance", 0.0m, false) * 100.0m);

                    tran.MOVEXT_EXTTXID = idTransazione;
                    tran.MOVEXT_AUX_STR_03 = locGame.GAME_KEY;
                    tran.MOVEXT_AMOUNT = sitinCents;
                    tran.MOVEXT_TIMESTAMP = DateTime.Now;
                    tran.MOVEXT_FK_EU_ID = local_eu.EU_ID;
                    tran.MOVEXT_STATE = (int)FinMovExtern.States.Creata;
                    tran.MOVEXT_TYPE = (int)FinMovExtern.Types.IngressoCasinoDaEsterno;
                    tran.MOVEXT_SERVICE = (int)FinMovExtern.Servizi.CasinoAM;
                    tran.MOVEXT_RAW_RECV_TXT = auxPars.getTypedValue("rawRequest", string.Empty);
                    tran.MOVEXT_RAW_RECV_UTC = DateTime.UtcNow;
                    tran.MOVEXT_OP_DATE = DateTime.Now;
                    tran.MOVEXT_AUX_INT_07 = funBonusSession ? 1 : 0;
                    if (useIdempotency)
                        tran.Create(author);

                    euExt = getUserExtension(local_eu, locGame, new HashParams("playType", GameMode, "skipCache", true));
                    EndUserExtInt euExtRfr = new EndUserExtInt
                    {
                        EUEI_ID = euExt.EUEI_ID,
                        unl = true,
                        useBOReadCache = false
                    };
                    euExtRfr.GetById();

                    FinMovExtern tranUpd = new FinMovExtern { MOVEXT_ID = tran.MOVEXT_ID };

                    #region caricamento dati

                    if (CcConfig.AppSettingBool("CASINOAM_SEAMLESS_WALLET", false))
                    {
                        GameMode = funBonusSession ? "FUN" : "CASH";
                        
                        if (CcConfig.AppSettingBool("TEMP_CAMOPENSESS_FIX_RECOVER_TOKEN", true))
                        {
                            string[] extTokenParts = (euExtRfr.EUEI_AUX_DATA + "#").Split('#');
                            extToken = extTokenParts[1];
                            if (extToken[0] == 'M')
                                _clientType_ = "mobile";
                        }
                        else
                        {
                            extToken = "" + euExtRfr.EUEI_AUX_DATA;
                            if (extToken.Length > 2 && extToken[1] == '#')
                            {
                                if (extToken[0] == 'M')
                                    _clientType_ = "mobile";
                                extToken = extToken.Substring(2);
                            }
                        }

                        if (CcConfig.AppSettingBool("CASINOSW_EXTTOKEN_DIRECT_CALL", false))
                        {
                            auxPars["_clientType_"] = _clientType_;
                            auxPars["GameId"] = locGame.GAME_KEY;
                            auxPars["extToken"] = extToken;
                            auxPars["newSessionBalance"] = sitinCents / 100.0m;

                            bool renewExtTokenDebugTimes = CcConfig.AppSettingBool("CASINOAM_RENEW_TOKEN_DEBUG_TIMES", false);
                            DateTime startCallUtc = DateTime.MinValue;
                            if (renewExtTokenDebugTimes)
                                startCallUtc = DateTime.UtcNow;

                            CBSLWalletBase.CallHandler callRes =
                                CBSLWalletMgr.def.WalletLink.AuxCall("GetNewExtToken", local_eu.EU_UID, auxPars);

                            if (renewExtTokenDebugTimes)
                                Log.getLogger("CasinoAMAuthDebug")
                                   .Debug("RenewExtToken::{0}::::{1}::{2}",
                                       (int)(DateTime.UtcNow - startCallUtc).TotalMilliseconds,
                                       local_eu.EU_UID,
                                       locGame.GAME_KEY);

                            if (callRes.IsOk && callRes.auxValues.ContainsKey("extToken"))
                                extToken = callRes.auxValues["extToken"];
                            else
                            {
                                _LogDebug(100,
                                    string.Format("Error retrieving new token for EU:{0} Error:{1}", local_eu.EU_UID,
                                        callRes.Error));
                                response["status"] = "UNKNOWN_ERROR";
                                responseCommittedWithError = true;
                            }
                        }

                        _LogDebug(5000, string.Format("OpenSession eu={0} sitin={1}", local_eu.EU_ID, sitinCents));

                        if (!responseCommittedWithError)
                        {
                            centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), unl = true };
                            using (new GGConnMgr.CentralCtxWrap("CENTRAL"))
                            {
                                centEu.getByUserId();
                            }

                            HashParams tokenResult = SWCasinoApiMgrStub.def.getToken_Core(
                                ExtIntMgr.PlatformKeys.CASINOAM,
                                local_eu.EU_ID,
                                centEu.EU_ID,
                                local_eu.EU_UID,
                                locGame.GAME_KEY,
                                GameMode,
                                extToken,
                                SmartHash.byPP(
                                    "_clientType_", _clientType_,
                                    "prefetchedBalance", sitinCents,
                                    "allowSessionRestore",
                                    CcConfig.AppSettingBool("CASINOAM_ALLOW_SESSIONRESTORE_ON_AUTHENTICATE", true)
                                ));

                            string resultError = tokenResult.getTypedValue("error", "");

                            _LogDebug(5000,
                                string.Format("OpenSession getToken eu={0} sitin={1} res={2}", local_eu.EU_ID, sitinCents,
                                    resultError));

                            if (resultError.Length == 0)
                            {
                                string token1 = tokenResult.getTypedValue("token", "", false);
                                HashResult infoByToken = CasinoBigExtWsAccountMgr.def.Decrypt_Token(token1);

                                prefetchedOrRestoredMatchId = (int)infoByToken["matchId"];

                                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                {
                                    prefetchedOrRestoredSub = new CasinoSubscription
                                    {
                                        MATCH_Id = prefetchedOrRestoredMatchId,
                                        unl = true
                                    };
                                    prefetchedOrRestoredSub.GetById();
                                    isPrefetched = prefetchedOrRestoredSub.MATCH_State ==
                                                   (int)CasinoSubscription.States.PreFetched;
                                    CasinoSession sess = prefetchedOrRestoredSub.getReferenceSession(true);
                                    table = sess.getReferenceTable(true);
                                    funBonusSession = sess.CSES_PLAYBONUS_TABLE > 0;
                                    newSubGame = sess.getReferenceCasinoTableDef(true).getReferenceGame(true);
                                    csesIdStr = "" + prefetchedOrRestoredSub.MATCH_FK_CSES_Id;
                                }

                                prefetchedCasinoSessionId =
                                    CasinoBigExtWsAccountMgr.def.Encrypt_CasinoSessionId(
                                        prefetchedOrRestoredMatchId,
                                        GameMode,
                                        new HashParams("generateSessionId_fixedVariant", "s"));

                                if (tokenResult.ContainsKey("prefetchedExternalTicket"))
                                    prefetchedExternalTicket =
                                        tokenResult.getTypedValue("prefetchedExternalTicket", "");
                                if (tokenResult.ContainsKey("prefetchedExternalSession"))
                                    prefetchedExternalSession =
                                        tokenResult.getTypedValue("prefetchedExternalSession", "");
                                if (tokenResult.ContainsKey("sessionStartSent"))
                                    sessionStartSent = tokenResult.getTypedValue("sessionStartSent", false);

                                depositOk = tokenResult.getTypedValue("depositOk", false);
                                sessionRestoreOk = tokenResult.getTypedValue("sessionRestoreOk", false);

                                if (sessionRestoreOk)
                                {
                                    string[] prefetchedCodes =
                                        (prefetchedOrRestoredSub.MATCH_PGA_EXTSESS + "#").Split('#');
                                    prefetchedExternalTicket = prefetchedCodes[1];
                                    prefetchedExternalSession = prefetchedCodes[0];
                                }
                            }
                            else
                            {
                                response["status"] = "UNKNOWN_ERROR";
                                responseCommittedWithError = true;
                            }
                        }
                    }
                    else
                    {
                        Hashtable openSession = new Hashtable();

                        if (!responseCommittedWithError)
                        {
                            #region sessioni

                            Hashtable allSessionsByKey = (Hashtable)casinoGameCache["ALL"];
                            if (allSessionsByKey == null)
                            {
                                HashResult getAllGameResult =
                                    CasinoCommonMgr.def.CASINO_COMMON_getAvailableGamesByKey(
                                        ExtIntMgr.PlatformKeys.CASINOAM,
                                        new HashParams("tipoConto", funBonusSession ? "fun" : "cash"));

                                if (!getAllGameResult.IsOk)
                                    getAllGameResult["MAP"] = new Hashtable();

                                casinoGameCache["ALL"] = allSessionsByKey =
                                    (Hashtable)getAllGameResult["MAP"];
                            }

                            if (allSessionsByKey.ContainsKey(locGame.GAME_KEY))
                            {
                                Hashtable item = (Hashtable)allSessionsByKey[locGame.GAME_KEY];
                                Hashtable gameItem = (Hashtable)item["game"];
                                foreach (object key in gameItem.Keys)
                                    openSession[key] = gameItem[key];

                                string sessionsKey = funBonusSession
                                    ? "openSessions_FunBonus"
                                    : "openSessions_Cash";

                                ArrayList sessions = new ArrayList(((Hashtable)item[sessionsKey]).Values);
                                if (sessions.Count > 0)
                                {
                                    Hashtable sessionItem =
                                        (Hashtable)sessions[_random.Next(0, sessions.Count)];
                                    foreach (object key in sessionItem.Keys)
                                        openSession[key] = sessionItem[key];

                                    Hashtable casGameItem = (Hashtable)item["casGameItem"];
                                    foreach (object key in casGameItem.Keys)
                                        openSession[key] = casGameItem[key];
                                }
                            }

                            if (openSession.Count == 0)
                            {
                                response["status"] = "SESSION_DOES_NOT_EXIST";
                                responseCommittedWithError = true;
                            }

                            #endregion sessioni

                            #region altri

                            if (!responseCommittedWithError)
                            {
                                centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName() };
                                using (new GGConnMgr.CentralCtxWrap("CENTRAL"))
                                {
                                    centEu.getByUserId();
                                }

                                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                {
                                    CasinoSession sess = new CasinoSession
                                    {
                                        CSES_Id = (int)openSession["CSES_Id"],
                                        unl = true
                                    };
                                    sess.GetById();
                                    newSubGame = sess.getReferenceCasinoTableDef(true).getReferenceGame(true);
                                    funBonusSession = sess.CSES_PLAYBONUS_TABLE > 0;
                                    table = sess.getReferenceTable(true);
                                }

                                lastLogin = EndUserMgr.def.EndUserLogin_getLastAccessNew(1, local_eu.EU_ID)
                                    .FirstOrDefault();
                                GameMode = funBonusSession ? "FUN" : "CASH";
                            }

                            #endregion altri
                        }

                        csesIdStr = "" + openSession["CSES_Id"];
                    }

                    #endregion caricamento dati

                    tranUpd.MOVEXT_AUX_STR_01 = csesIdStr;

                    if (!responseCommittedWithError)
                    {
                        if (sitinCents < 0 ||
                            sitinCents > CcConfig.AppSettingInt("CASINOAM_MAX_SITIN_CENTS", 100000))
                        {
                            _LogDebug(200, string.Format("valore di sitin non valido:{0}", sitinCents), local_eu,
                                idTransazione, methName);
                            response["status"] = "INVALID_PARAMETER";
                            responseCommittedWithError = true;
                        }

                        if (!responseCommittedWithError &&
                            !CcConfig.AppSettingBool("CASINOAM_OPEN_SESSION_SKIP_PREDEPO_BALANCECHECK", false))
                        {
                            bool extAccountMangeFunBonus =
                                CcConfig.AppSettingBool("EXTACCOUNT_MANAGE_FUNBONUS", false);
                            bool local_eu_ExtAccount = local_eu.EU_IS_EXTERNAL > 0 &&
                                                       (!funBonusSession || extAccountMangeFunBonus);
                            long saldoTotPreAcquisto = local_eu_ExtAccount
                                ? 0
                                : _getSaldo(local_eu, locGame, funBonusSession);
                            bool saldoTotPreAcquistoCheck =
                                local_eu_ExtAccount || saldoTotPreAcquisto >= sitinCents;

                            if (!saldoTotPreAcquistoCheck)
                            {
                                _LogDebug(200,
                                    string.Format("EU non ha i soldi {0}>{1}", sitinCents, saldoTotPreAcquisto),
                                    local_eu, idTransazione, methName);
                                response["status"] = "INSUFFICIENT_FUNDS";
                                responseCommittedWithError = true;
                            }
                        }
                    }

                    if (!responseCommittedWithError)
                    {
                        bool sitIntFlowResultIsOk;
                        bool subResIsNull = false;
                        int subResLocal_op_id = -1;
                        CasinoSubscription subResSubscription = null;

                        if (CcConfig.AppSettingBool("CASINOAM_SEAMLESS_WALLET", false))
                        {
                            if (prefetchedOrRestoredSub == null ||
                                !(isPrefetched || depositOk || sessionRestoreOk))
                                throw new Exception("Invalid Prefetched data");

                            if (!sessionRestoreOk && !sessionStartSent)
                            {
                                #region Session Start

                                bool sendSessionStart =
                                    CcConfig.AppSettingBool("CASINOSW_SEAMLESS_WALLET_SEND_STARTSESSION", false);
                                HashParams sendSessionStartAuxPars = new HashParams();
                                bool sendSessionStartBeforeDeposit =
                                    CcConfig.AppSettingBool(
                                        "CASINOSW_SEAMLESS_WALLET_SEND_STARTSESSION_BEFORE_DEPO", false);
                                bool sendSessionStartTicketCapture =
                                    CcConfig.AppSettingBool(
                                        "CASINOSW_SEAMLESS_WALLET_SEND_STARTSESSION_TICKET_CAPTURE", false);
                                string gameKey = string.Empty;

                                if (sendSessionStart)
                                {
                                    try
                                    {
                                        sendSessionStartAuxPars = new HashParams(
                                            "GameMode", GameMode,
                                            "CasinoSessionId", prefetchedCasinoSessionId,
                                            "StartDateUtc", prefetchedOrRestoredSub.MATCH_StartDateUTC,
                                            "GameId", gameKey = newSubGame.GAME_KEY,
                                            "GameName", newSubGame.GAME_NAME,
                                            "GameExtKey", newSubGame.GAME_EI_KEY,
                                            "_clientType_", _clientType_,
                                            "extToken", extToken,
                                            "localEu", local_eu,
                                            "_ticket_capture_", sendSessionStartTicketCapture,
                                            "extService", (int)FinMovExtern.Servizi.CasinoAM
                                        );
                                    }
                                    catch (Exception logContinue)
                                    {
                                        Log.exc(logContinue);
                                    }
                                }

                                if (sendSessionStart && sendSessionStartBeforeDeposit)
                                {
                                    HashResult sessionStartResult = new HashResult();
                                    try
                                    {
                                        sessionStartResult =
                                            CBSLWalletMgr.def.SessionStartNotification(local_eu.EU_ID,
                                                sendSessionStartAuxPars);
                                    }
                                    catch (Exception logContinue)
                                    {
                                        Log.exc(logContinue);
                                        sessionStartResult.IsOk = false;
                                        sessionStartResult.ErrorMessage = logContinue.Message;
                                    }

                                    if (sessionStartResult.IsOk)
                                    {
                                        if (sendSessionStartTicketCapture)
                                        {
                                            try
                                            {
                                                prefetchedExternalTicket =
                                                    "" + sessionStartResult["prefetchedExternalTicket"];
                                                prefetchedExternalSession =
                                                    "" + sessionStartResult["prefetchedExternalSession"];
                                            }
                                            catch (Exception exc)
                                            {
                                                Log.exc(exc);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        string customStartExcHandlingLow =
                                            CcConfig.AppSettingStr(
                                                    "CASINOSW_SEAMLESS_WALLET_SEND_STARTSESSION_PRE_EXC_FIX", "")
                                                .Trim()
                                                .ToLower();
                                        if (customStartExcHandlingLow.Length > 0 &&
                                            sessionStartResult.ErrorMessage.ToLower()
                                                .Contains(customStartExcHandlingLow))
                                        {
                                            new Action<string, int, int, SmartHash>(_manageOldSessionEnds)
                                                .BeginInvoke(
                                                    ExtIntMgr.PlatformKeys.CASINOAM,
                                                    local_eu.EU_ID,
                                                    centEu.EU_ID,
                                                    SmartHash.byPP("GAME_KEY", gameKey),
                                                    null,
                                                    null);
                                        }
                                    }

                                    if (!sessionStartResult.IsOk)
                                        throw new Exception(
                                            "EXC_STARTING_SESSION::" + sessionStartResult.ErrorMessage);
                                }

                                _LogDebug(5000,
                                    string.Format("OpenSession sessionstartend eu={0} sitin={1}", local_eu.EU_ID,
                                        sitinCents));

                                #endregion Session Start

                                #region TICKET CAPTURE controls

                                if (prefetchedExternalTicket.Length > 0)
                                {
                                    if (CcConfig.AppSettingBool(
                                            "CASINOSW_SEAMLESS_WALLET_TICKET_CAPTURE_CHECK_EXISTENCE", true))
                                    {
                                        SmartHash checkTicketRes = new SmartHash();
                                        using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                        {
                                            CasinoSubscription runSub = new CasinoSubscription
                                            {
                                                MATCH_PGA_TICKET = prefetchedExternalTicket,
                                                unl = true
                                            };
                                            runSub.getByTicketAams();
                                            if (runSub.flagRecordFound)
                                                checkTicketRes.IsOk = true;
                                            else
                                                checkTicketRes.ErrorMessage =
                                                    string.Format("Ticket {0} not found", prefetchedExternalTicket);
                                        }

                                        if (checkTicketRes.IsOk)
                                            prefetchedExternalTicket = "###" + prefetchedExternalTicket;
                                    }
                                }

                                #endregion TICKET CAPTURE controls
                            }

                            sitIntFlowResultIsOk = depositOk || sessionRestoreOk;
                            subResSubscription = prefetchedOrRestoredSub;
                            subResLocal_op_id = prefetchedOrRestoredSub.MATCH_FK_RESERVE_MOV_Id;
                        }
                        else
                        {
                            int machId = Machines.getDefaultMachine().MACH_ID;
                            int progr = CasinoCommonMgr.def.CASINO_COMMON_GetLastHandProgr(
                                ExtIntMgr.PlatformKeys.CASINOAM, table.TAB_Id);
                            int currentProgr = progr + 1;

                            Hashtable fPars = new Hashtable();
                            fPars["HandProgr"] = currentProgr;

                            HashParams auxData = new HashParams(
                                "tipoConto", funBonusSession ? "fun" : "cash",
                                "PreFetchedFirstSub", prefetchedOrRestoredSub,
                                "prefetchedExternalTicket", prefetchedExternalTicket,
                                "prefetchedExternalSession", prefetchedExternalSession,
                                "casinoPlatform", ExtIntMgr.PlatformKeys.CASINOAM
                            );

                            HashResult sitIntFlowResult =
                                CasinoAccountingMgr.def.SignedAssemblyImplementation.Peripheral_Casino_SitIn_Flow(
                                    CasinoAMSwDispatchTest.AMSWFACEKEY,
                                    local_eu,
                                    centEu,
                                    table,
                                    locGame,
                                    machId,
                                    sitinCents,
                                    false,
                                    lastLogin == null ? "127.0.0.1" : lastLogin.EUL_IP,
                                    fPars,
                                    currentProgr,
                                    author,
                                    auxData);

                            CasinoAccountingMgr.CasinoSubscResult subRes = null;
                            if (sitIntFlowResult.ContainsKey("LOCAL_ACCNT_RESULT"))
                                subRes =
                                    (CasinoAccountingMgr.CasinoSubscResult)
                                    sitIntFlowResult["LOCAL_ACCNT_RESULT"];

                            sitIntFlowResultIsOk = sitIntFlowResult.IsOk;

                            if (sitIntFlowResultIsOk)
                            {
                                if (subRes == null)
                                    subResIsNull = true;
                                else
                                {
                                    subResLocal_op_id = subRes.local_op_id;
                                    subResSubscription = subRes.subscription;
                                }
                            }
                            else
                            {
                                _LogDebug(200,
                                    string.Format("SITIN ERROR {0}", sitIntFlowResult.ErrorMessage), local_eu,
                                    idTransazione, methName);
                                response["status"] = "SITIN_ERROR";

                                if (subRes != null &&
                                    subRes.result == CasinoAccountingMgr.CasinoSubscResType.NotEnoughMoney)
                                    response["status"] = "INSUFFICIENT_FUNDS";
                            }
                        }

                        long saldoContoGioco = 0;
                        long saldoBonus = 0;
                        long bonusAmount = 0;
                        int subId = -1;

                        if (sitIntFlowResultIsOk)
                        {
                            if (subResIsNull)
                                throw new Exception("Invalid subres content");

                            CasinoSubscription casinoSub = subResSubscription;

                            string aamsTicket = prefetchedExternalTicket;
                            string aamsSession = prefetchedExternalSession;

                            if (string.IsNullOrEmpty(prefetchedExternalTicket))
                                aamsTicket = casinoSub.MATCH_PGA_TICKET;

                            if (string.IsNullOrEmpty(prefetchedExternalSession))
                            {
                                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                                {
                                    aamsSession = casinoSub.referenceSession.CSES_PGA_AAMS_SESSID;
                                }
                            }
                            //euExtRfr.EUEI_NOTE = aamsTicket + "#" + aamsSession;
                            //euExtRfr.Update(author);

                            response.IsOk = true;
                            response["status"] = "OK";
                            response["aamsSession"] = aamsSession;
                            response["aamsTicket"] = aamsTicket;
                            IdMov = subResLocal_op_id;
                            response["MATCH_Id"] = response["matchId"] = subId = casinoSub.MATCH_Id;
                            bonusAmount = longSupportStage <= 1
                                ? subResSubscription.MATCH_OP_LOOSE_BONUS
                                : subResSubscription.MATCH_OP_LOOSE_BONUS_LONG;

                            try
                            {
                                saldoContoGioco = _getSaldo(local_eu, locGame, funBonusSession);
                                saldoBonus = funBonusSession
                                    ? 0
                                    : (saldoContoGioco -
                                       _getSaldo(local_eu, locGame, false,
                                           new HashParams("useAccKey", false)));
                            }
                            catch (Exception logCont)
                            {
                                Log.exc(logCont);
                            }
                        }

                        tranUpd.MOVEXT_RAW_SENT_TXT = JsonConvert.SerializeObject(response);
                        tranUpd.MOVEXT_RAW_SENT_UTC = DateTime.UtcNow;
                        tranUpd.MOVEXT_FK_MOV_ID = IdMov;
                        tranUpd.MOVEXT_RESULT = 0;
                        tranUpd.MOVEXT_RESULTCODE = response.get("status", "");

                        if (longSupportStage <= 2)
                        {
                            tranUpd.MOVEXT_AUX_INT_01 = Convert.ToInt32(saldoContoGioco);
                            tranUpd.MOVEXT_AUX_INT_02 = Convert.ToInt32(saldoBonus);
                        }

                        if (longSupportStage >= 1)
                        {
                            tranUpd.MOVEXT_BIGAMOUNT9 = saldoContoGioco;
                            tranUpd.MOVEXT_BIGAMOUNT8 = saldoBonus;
                        }

                        if (response.IsOk)
                        {
                            tranUpd.MOVEXT_STATE = (int)FinMovExtern.States.Ricevuta;
                            tranUpd.MOVEXT_AUX_STR_02 = response.get("aamsSession", "");
                            tranUpd.MOVEXT_CODESTERNO = response.get("aamsTicket", "");
                            tranUpd.MOVEXT_AUX_INT_04 = subId;
                        }
                        else
                        {
                            tranUpd.MOVEXT_STATE = (int)FinMovExtern.States.InErrore;
                        }

                        tranUpd.MOVEXT_BIGAMOUNT4 = sitinCents;
                        tranUpd.MOVEXT_BIGAMOUNT5 = bonusAmount;
                        tranUpd.MOVEXT_AUX_INT_05 = 1;

                        if (useIdempotency)
                            PatternLib.def.RetryAction(3, 1000, () => tranUpd.Update(author));
                    }
                    else
                    {
                        tranUpd.MOVEXT_RAW_SENT_TXT = JsonConvert.SerializeObject(response);
                        tranUpd.MOVEXT_RAW_SENT_UTC = DateTime.UtcNow;
                        tranUpd.MOVEXT_FK_MOV_ID = IdMov;
                        tranUpd.MOVEXT_RESULT = 0;
                        tranUpd.MOVEXT_RESULTCODE = response.get("status", "");

                        if (longSupportStage <= 2)
                        {
                            tranUpd.MOVEXT_AUX_INT_01 = 0;
                            tranUpd.MOVEXT_AUX_INT_02 = 0;
                        }

                        if (longSupportStage >= 1)
                        {
                            tranUpd.MOVEXT_BIGAMOUNT9 = 0;
                            tranUpd.MOVEXT_BIGAMOUNT8 = 0;
                        }

                        tranUpd.MOVEXT_STATE = (int)FinMovExtern.States.InErrore;
                        tranUpd.MOVEXT_BIGAMOUNT4 = sitinCents;
                        tranUpd.MOVEXT_AUX_INT_05 = 1;

                        if (useIdempotency)
                            PatternLib.def.RetryAction(3, 1000, () => tranUpd.Update(author));
                    }
                }
                catch (Exception exc)
                {
                    Log.exc(exc);
                    response = new SmartHash();

                    if (tran.MOVEXT_ID > 0)
                    {
                        try
                        {
                            string errDesc = exc.Message;
                            if (errDesc.Length > 50)
                                errDesc = errDesc.Substring(0, 50);

                            FinMovExtern tranUpd2 = new FinMovExtern { MOVEXT_ID = tran.MOVEXT_ID };
                            tranUpd2.MOVEXT_STATE = (int)FinMovExtern.States.InErrore;
                            tranUpd2.MOVEXT_RESULTCODE = errDesc;
                            tranUpd2.MOVEXT_FK_MOV_ID = IdMov;
                            tranUpd2.MOVEXT_RAW_SENT_TXT = JsonConvert.SerializeObject(response);
                            tranUpd2.MOVEXT_RAW_SENT_UTC = DateTime.UtcNow;

                            if (useIdempotency)
                                tranUpd2.Update(author);
                        }
                        catch (Exception logCont)
                        {
                            Log.exc(logCont);
                        }
                    }
                }
            }
            else
            {
                _LogDebug(200, "Tx Presente -> reinivio", local_eu, idTransazione, methName);
                response = JsonConvert.DeserializeObject<SmartHash>(checkTran.MOVEXT_RAW_SENT_TXT);
                if (response != null)
                    response["retransmission"] = true;
            }

            if (!response.IsOk && ("" + response.ErrorMessage).Length == 0 && response.ContainsKey("status"))
                response.ErrorMessage = "ERR:" + response["status"];

            return response;
        }

        #endregion openSession

        #endregion

        #region CasinoExtIntFace Implementation

        public override HashResult checkUserRegistered(EndUser eu, Games game, HashParams extPars)
        {
            HashResult result = new HashResult();
            EndUserExtInt euExt = getUserExtension(eu, game, extPars);
            result.IsOk = euExt != null && euExt.flagRecordFound;
            return result;
        }

        public override HashResult registerUser(EndUser eu, Games game, HashParams extPars, string author)
        {
            HashResult result = new HashResult();
            EndUserExtInt euExtInt = new EndUserExtInt();
            euExtInt.EUEI_CATEGORY = "CasinoAM";
            euExtInt.EUEI_EXTCODE = "-1";
            euExtInt.EUEI_FK_EI_ID = ExtIntMgr.def.getCasinoAM_EI().EI_ID;
            euExtInt.EUEI_FK_EU_ID = eu.EU_ID;
            euExtInt.EUEI_KEY = _getAMUserName(eu, game, extPars);
            euExtInt.EUEI_REGDATE = DateTime.Now;
            euExtInt.EUEI_STATE = (int)EndUserExtInt.States.Temporary;
            euExtInt.Create(author);
            result.IsOk = true;
            result["extEu"] = euExtInt;
            return result;
        }

        public override HashResult getAvailableGames(HashParams extPars)
        {
            HashResult result = new HashResult();
            result.IsOk = true;
            Dictionary<string, Hashtable> res = _cachedGames;
            DateTime utcNow = DateTime.UtcNow;
            try
            {
                if (res == null || _cachedGamesExpireUtc < utcNow)
                {
                    ExternalIntegration casinoAMEi = ExtIntMgr.def.getCasinoAM_EI();
                    Dictionary<string, Hashtable> newList = new Dictionary<string, Hashtable>();
                    ArrayList allAMGameTables = new Games().GetAll(-1, string.Empty, null, string.Format(" GAME_FK_EI_ID = {0} AND GAME_VISIBLE > 0 " + (CcConfig.AppSettingBool("TEMP_FIX_AM_GETGAMES", true) ? " AND ISNULL(GAME_MOBILE,0)=0 " : ""), casinoAMEi.EI_ID));

                    foreach (Games gameTable in allAMGameTables)
                    {
                        newList[gameTable.GAME_EI_KEY] = getGameInfos(gameTable);
                    }

                    _cachedGames = res = newList;
                    _cachedGamesExpireUtc = utcNow.AddSeconds(CcConfig.AppSettingInt("CASINOEXTINTAM_CACHE_GAME_EXPIRE_SECS", 10 * 60));
                }
            }
            catch (Exception exc)
            {
                Log.exc(exc);
                _cachedGamesExpireUtc = utcNow.AddSeconds(CcConfig.AppSettingInt("CASINOEXTINTAM_CACHE_GAME_EXPIRE_IFFAIL_SECS", 5));
            }
            if (res == null)
            {
                _cachedGames = res = new Dictionary<string, Hashtable>();
                _cachedGamesExpireUtc = utcNow.AddSeconds(CcConfig.AppSettingInt("CASINOEXTINTAM_CACHE_GAME_EXPIRE_IFFAIL_SECS", 5));
            }
            result["GAMEMAP"] = res;
            return result;
        }

        public virtual HashResult getAllAvailableGamesAll(HashParams extPars)
        {
            HashResult result = new HashResult();
            result.IsOk = true;
            Dictionary<string, Hashtable> res = _cachedGamesAll;
            DateTime utcNow = DateTime.UtcNow;
            try
            {
                if (res == null || _cachedGamesAllExpireUtc < utcNow)
                {
                    ExternalIntegration casinoAMEi = ExtIntMgr.def.getCasinoAM_EI();
                    Dictionary<string, Hashtable> newList = new Dictionary<string, Hashtable>();
                    ArrayList allPPGameTables = new Games().GetAll(-1, string.Empty, null, string.Format(" GAME_FK_EI_ID = {0} AND GAME_VISIBLE > 0 ", casinoAMEi.EI_ID));
                    foreach (Games gameTable in allPPGameTables)
                    {
                        newList[gameTable.GAME_EI_KEY + "x" + gameTable.GAME_MOBILE] = getGameInfos(gameTable);
                    }

                    _cachedGamesAll = res = newList;
                    _cachedGamesAllExpireUtc = utcNow.AddSeconds(CcConfig.AppSettingInt("CASINOEXTINTAM_CACHE_GAME_EXPIRE_SECS", 10 * 60));
                }
            }
            catch (Exception exc)
            {
                Log.exc(exc);
                _cachedGamesAllExpireUtc = utcNow.AddSeconds(CcConfig.AppSettingInt("CASINOEXTINTAM_CACHE_GAME_EXPIRE_IFFAIL_SECS", 5));
            }
            if (res == null)
            {
                _cachedGamesAll = res = new Dictionary<string, Hashtable>();
                _cachedGamesAllExpireUtc = utcNow.AddSeconds(CcConfig.AppSettingInt("CASINOEXTINTAM_CACHE_GAME_EXPIRE_IFFAIL_SECS", 5));
            }
            result["GAMEMAP"] = res;
            return result;
        }

        public override HashResult getCurrentBalances(EndUser eu, Games game, HashParams extPars)
        {
            const _MethSigns methSign = _MethSigns.GETBAL;
            HashResult result = new HashResult();
            try
            {
                int fstMatchId = extPars.getTypedValue("fstMatchId", -100, false);
                EndUser central_eu = extPars.getTypedValue<EndUser>("centEu", null);
                string ticket = extPars.getTypedValue("ticket", string.Empty);
                bool funSession = extPars.getTypedValue("funSession", false);
                bool freeRoundSession = extPars.getTypedValue("freeRoundSession", false);
                if (string.IsNullOrEmpty(ticket))
                {
                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    {
                        CasinoSubscription fstSub = new CasinoSubscription { MATCH_Id = fstMatchId, unl = true };
                        fstSub.GetById();
                        if (fstSub.flagRecordFound)
                        {
                            ticket = fstSub.MATCH_PGA_TICKET;
                            funSession = fstSub.MATCH_SITIN_PLAYBONUS > 0;
                            freeRoundSession = funSession && fstSub.MATCH_FR_START > 0;
                        }
                    }
                }

                if (central_eu == null)
                {
                    central_eu = new EndUser { EU_UID = eu.GetCentralEndUserName(), useNoLockWhePossible = true };
                    using (new GGConnMgr.CentralCtxWrap("CENTRAL"))
                    {
                        central_eu.getByUserId();
                    }
                }

                SessionBalancesHolder counters = _loadRealSessionBalancesBySessInfos(
                    ticket,
                    new HashParams(
                        "EU_UID", eu.EU_UID,
                        "MATCH_Id", fstMatchId,
                        "centEU_Id", central_eu.EU_ID,
                        "funSession", funSession,
                        "freeRoundSession", freeRoundSession,
                        "GAME_KEY", game.GAME_KEY
                    ),
                    SmartHash.byPP(
                        "_getLocalBalances_", extPars.getTypedValue("_getLocalBalances_", false),
                        "_getBalancesForSitout_", extPars.getTypedValue("_getBalancesForSitout_", false)
                    ));

                result.IsOk = true;
                result["CASH"] = counters.amountTotal - counters.bonus;
                result["BONUS"] = counters.bonus;
                result["FUNBONUS"] = counters.funBonus;
                result["FUNBONUS_FORCE"] = counters.funBonus;
            }
            catch (Exception exc)
            {
                Log.exc(exc);
                result.ErrorMessage = _ErrorRes(10, methSign, exc.Message);
            }
            return result;
        }

        public override HashResult sitIn(EndUser eu, Games game, long amount, TransferType transferType, HashParams extPars, string author)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            const _MethSigns methSign = _MethSigns.SITIN0;
            string error = string.Empty;
            string extMovTxPrefix = CcConfig.AppSettingStr("CASINOEXTINTAM_EXTTX_PREFIX", "AMADBT");

            HashResult result = new HashResult();
            DateTime utcNow = DateTime.UtcNow;

            int fstMatchId = extPars.getTypedValue("fstMatchId", -100, false);
            int matchId = extPars.getTypedValue("matchId", -100, false);
            EndUser centEu = extPars.getTypedValue<EndUser>("centEu", null, false);

            int chargeType = (int)FinMovExtern.Types.CaricoSuEsterno;
            if (transferType == TransferType.RealBonus || transferType == TransferType.FunBonus)
                chargeType = (int)FinMovExtern.Types.CaricoBonusSuEsterno;

            bool ok = true;
            long uniqueId;
            lock (_uniquerLock)
            {
                uniqueId = _uniquer++;
            }
            FinMovExtern movextCreate = new FinMovExtern();
            try
            {
                movextCreate.MOVEXT_EXTTXID = "#TMP#" + utcNow.ToString("yyMMddHHmmss", new CultureInfo("IT-it")) + "#" + uniqueId;
                movextCreate.MOVEXT_SERVICE = (int)FinMovExtern.Servizi.CasinoAM;
                movextCreate.MOVEXT_FK_EU_ID = eu.EU_ID;
                movextCreate.MOVEXT_FK_EI_ID = ExtIntMgr.def.getCasinoAM_EI().EI_ID;
                movextCreate.MOVEXT_TYPE = chargeType;
                movextCreate.MOVEXT_AMOUNT = amount;
                movextCreate.MOVEXT_OP_DATE = utcNow.ToLocalTime();
                movextCreate.MOVEXT_STATE = (int)FinMovExtern.States.Creata;
                movextCreate.MOVEXT_IN_PARS = "";
                movextCreate.MOVEXT_OUT_PARS = "";
                movextCreate.MOVEXT_AUX_INT_01 = fstMatchId;
                movextCreate.Create(author);

                movextCreate.MOVEXT_EXTTXID = extMovTxPrefix + ("" + movextCreate.MOVEXT_ID).PadLeft(10, '0');
                movextCreate.MOVEXT_RAW_SENT_UTC = utcNow;
                movextCreate.MOVEXT_STATE = (int)FinMovExtern.States.Inviata;
                movextCreate.Update(author);
            }
            catch (Exception exc)
            {
                Log.exc(exc);
                ok = false;
            }

            int setFreeRounds = -1;
            int betLevel = -1;
            int numLines;
            bool isFreeRoundEnabled = false;
            if (ok && CcConfig.AppSettingBool("CASINOAM_FREEROUNDS_ENABLE", false) && extPars.getTypedValue("playType", "CASH") == "FUN")
            {
                isFreeRoundEnabled = true;
                if (extPars.ContainsKey("fPars"))
                {
                    Hashtable fPars = extPars["fPars"] as Hashtable;
                    if (fPars != null)
                    {
                        if (fPars.ContainsKey("setFreeRounds"))
                            int.TryParse("" + fPars["setFreeRounds"], out setFreeRounds);
                        if (fPars.ContainsKey("freeRoundPlb"))
                            int.TryParse("" + fPars["freeRoundPlb"], out betLevel);
                        if (fPars.ContainsKey("freeRoundLines"))
                            int.TryParse("" + fPars["freeRoundLines"], out numLines);
                    }
                }
            }

            if (ok)
            {
                try
                {
                    int userId = OrgUserMgr.def.GetFirstMaster().GetCentralUser().USER_ID;
                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    {
                        CasinoMovimentiBuffer sitInBuff = new CasinoMovimentiBuffer();
                        sitInBuff.CMB_SERVICE = (int)FinMovExtern.Servizi.CasinoAM;
                        sitInBuff.CMB_FK_FST_MATCH_Id = fstMatchId;
                        sitInBuff.CMB_FK_CURR_MATCH_Id = matchId;
                        sitInBuff.CMB_FK_EU_Id = centEu.EU_ID;
                        sitInBuff.CMB_TYPE = (int)CasinoMovimentiBuffer.Type.SitIn;
                        sitInBuff.CMB_STATE = (int)CasinoMovimentiBuffer.States.Committed;
                        if (longSupportStage <= 2)
                        {
                            sitInBuff.CMB_AMOUNT_TOT = Convert.ToInt32(amount);
                            if (transferType == TransferType.RealBonus)
                                sitInBuff.CMB_AMOUNT_BONUS = Convert.ToInt32(amount);
                            else if (transferType == TransferType.FunBonus)
                                sitInBuff.CMB_AMOUNT_PLAYBONUS = Convert.ToInt32(amount);
                        }
                        if (longSupportStage >= 1)
                        {
                            sitInBuff.CMB_AMOUNT_TOT_LONG = amount;
                            if (transferType == TransferType.RealBonus)
                                sitInBuff.CMB_AMOUNT_BONUS_LONG = amount;
                            else if (transferType == TransferType.FunBonus)
                                sitInBuff.CMB_AMOUNT_PLAYBONUS = Convert.ToInt32(amount);
                        }

                        sitInBuff.CMB_CLOSED = (int)CasinoMovimentiBuffer.Closed.Closed;
                        sitInBuff.CMB_OPDATE_UTC = DateTime.UtcNow;
                        sitInBuff.CMB_CREATEDAT = sitInBuff.CMB_OPDATE_UTC.ToLocalTime();
                        sitInBuff.CMB_FK_USER_ID = userId;
                        if (setFreeRounds > 0)
                            sitInBuff.CMB_REM_FR = setFreeRounds;

                        PatternLib.def.RetryAction(3, 2000, () => sitInBuff.Create(author));
                    }

                    movextCreate.MOVEXT_RAW_RECV_UTC = DateTime.UtcNow;
                    movextCreate.MOVEXT_STATE = (int)FinMovExtern.States.Ricevuta;
                    movextCreate.Update(author);
                    result.IsOk = true;
                }
                catch (Exception exc)
                {
                    Log.exc(exc);
                }
            }
            else error = _ErrorRes(25, methSign, string.Format("connot create ext mov fro eu:{0}", eu.EU_UID));

            if (!string.IsNullOrEmpty(error)) result.ErrorMessage = error;

            return result;
        }

        public override HashResult sitOut(EndUser eu, Games game, long amount, TransferType transferType, HashParams extPars, string author)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            string error = string.Empty;
            HashResult result = new HashResult();
            DateTime utcNow = DateTime.UtcNow;
            string extMovTxPrefix = CcConfig.AppSettingStr("CASINOEXTINTAM_EXTTX_PREFIX", "AMADBT");

            int fstMatchId = extPars.getTypedValue("fstMatchId", -100, false);
            int matchId = extPars.getTypedValue("matchId", -100, false);
            EndUser centEu = extPars.getTypedValue<EndUser>("centEu", null, false);

            int unchargeType = (int)FinMovExtern.Types.ScaricoSuEsterno;
            if (transferType == TransferType.RealBonus || transferType == TransferType.FunBonus) unchargeType = (int)FinMovExtern.Types.ScaricoBonusSuEsterno;

            long uniqueId;
            lock (_uniquerLock)
            {
                uniqueId = _uniquer++;
            }

            FinMovExtern movextCreate = new FinMovExtern();
            movextCreate.MOVEXT_EXTTXID = "#TMP#" + utcNow.ToString("yyMMddHHmmss", new CultureInfo("IT-it")) + "#" + uniqueId;
            movextCreate.MOVEXT_SERVICE = (int)FinMovExtern.Servizi.CasinoAM;
            movextCreate.MOVEXT_FK_EU_ID = eu.EU_ID;
            movextCreate.MOVEXT_FK_EI_ID = ExtIntMgr.def.getCasinoAM_EI().EI_ID;
            movextCreate.MOVEXT_TYPE = unchargeType;
            movextCreate.MOVEXT_AMOUNT = amount;
            movextCreate.MOVEXT_OP_DATE = utcNow.ToLocalTime();
            movextCreate.MOVEXT_STATE = (int)FinMovExtern.States.Creata;
            movextCreate.MOVEXT_IN_PARS = "";
            movextCreate.MOVEXT_OUT_PARS = "";
            movextCreate.Create(author);

            movextCreate.MOVEXT_EXTTXID = extMovTxPrefix + ("" + movextCreate.MOVEXT_ID).PadLeft(10, '0');
            movextCreate.MOVEXT_RAW_SENT_UTC = utcNow;
            movextCreate.MOVEXT_STATE = (int)FinMovExtern.States.Inviata;
            movextCreate.MOVEXT_AUX_INT_01 = fstMatchId;
            movextCreate.Update(author);

            int userId = OrgUserMgr.def.GetFirstMaster().GetCentralUser().USER_ID;
            using (new GGConnMgr.CentralCtxWrap("CASINO"))
            {
                CasinoMovimentiBuffer sitOutBuff = new CasinoMovimentiBuffer();
                sitOutBuff.CMB_SERVICE = (int)FinMovExtern.Servizi.CasinoAM;
                sitOutBuff.CMB_FK_FST_MATCH_Id = fstMatchId;
                sitOutBuff.CMB_FK_CURR_MATCH_Id = matchId;
                sitOutBuff.CMB_FK_EU_Id = centEu.EU_ID;
                sitOutBuff.CMB_TYPE = (int)CasinoMovimentiBuffer.Type.SitOut;
                sitOutBuff.CMB_STATE = (int)CasinoMovimentiBuffer.States.Committed;

                if (longSupportStage <= 2)
                {
                    sitOutBuff.CMB_AMOUNT_TOT = Convert.ToInt32(-amount);
                    if (transferType == TransferType.RealBonus)
                        sitOutBuff.CMB_AMOUNT_BONUS = Convert.ToInt32(-amount);
                    else if (transferType == TransferType.FunBonus)
                        sitOutBuff.CMB_AMOUNT_PLAYBONUS = Convert.ToInt32(-amount);
                }
                if (longSupportStage >= 1)
                {
                    sitOutBuff.CMB_AMOUNT_TOT_LONG = -amount;
                    if (transferType == TransferType.RealBonus)
                        sitOutBuff.CMB_AMOUNT_BONUS_LONG = -amount;
                    else if (transferType == TransferType.FunBonus)
                        sitOutBuff.CMB_AMOUNT_PLAYBONUS = Convert.ToInt32(-amount);
                }

                sitOutBuff.CMB_CLOSED = (int)CasinoMovimentiBuffer.Closed.Closed;
                sitOutBuff.CMB_OPDATE_UTC = DateTime.UtcNow;
                sitOutBuff.CMB_CREATEDAT = sitOutBuff.CMB_OPDATE_UTC.ToLocalTime();
                sitOutBuff.CMB_FK_USER_ID = userId;

                PatternLib.def.RetryAction(3, 2000, () => sitOutBuff.Create(author));

                CasinoSubscription sub = new CasinoSubscription { MATCH_Id = fstMatchId, useNoLockWhenPossible = true };
                sub.GetById();
                sessionInfosByTicketCache.remove(sub.MATCH_PGA_TICKET);
            }

            movextCreate.MOVEXT_RAW_RECV_UTC = DateTime.UtcNow;
            movextCreate.MOVEXT_STATE = (int)FinMovExtern.States.Ricevuta;
            movextCreate.Update(author);
            result.IsOk = true;

            if (!string.IsNullOrEmpty(error)) result.ErrorMessage = error;
            return result;
        }

        public override HashResult getSessionBalances(EndUser eu, Games game, string specificSessionToken, HashParams extPars)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            HashResult result = new HashResult();
            EndUser cent_eu = extPars.getTypedValue<EndUser>("cent_eu", null);
            if (cent_eu == null)
            {
                cent_eu = new EndUser { EU_UID = eu.GetCentralEndUserName() };
                using (new GGConnMgr.CentralCtxWrap("CENTRAL"))
                {
                    cent_eu.getByUserId();
                }
            }
            bool isPlayBonusSub = extPars.getTypedValue("isPlayBonusSub", false);
            string minExtId = extPars.getTypedValue<string>("minExtId", null);
            long minExtIdL = -1;
            if (!string.IsNullOrEmpty(minExtId))
                long.TryParse(minExtId, out minExtIdL);
            int fstMatchId = int.Parse(specificSessionToken);

            bool getCount = extPars.getTypedValue("getCount", false);
            int[] filterStates = extPars.getTypedValue("filterStates", new[] { (int)CasinoMovimentiBuffer.States.Dumped, (int)CasinoMovimentiBuffer.States.PreCommitted, (int)CasinoMovimentiBuffer.States.Committed });
            List<CasinoMovimentiBuffer> casinoMovimentiBuffers;
            using (new GGConnMgr.CentralCtxWrap("CASINO"))
            {
                HashParams pars = new HashParams(
                    "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                    "CMB_FK_FST_MATCH_Id", fstMatchId,
                    "CMB_FK_EU_Id", cent_eu.EU_ID,
                    "CMB_STATEs", filterStates,
                    "CMB_TYPEs", new[] { (int)CasinoMovimentiBuffer.Type.Loose, (int)CasinoMovimentiBuffer.Type.Win },
                    "CMB_IDMinIncl", minExtIdL,
                    "orderBy", "CMB_ID"
                );
                if (getCount)
                {
                    pars.Remove("CMB_IDMinIncl");
                    pars.Remove("orderBy");
                    pars["getCount"] = true;
                }
                casinoMovimentiBuffers = CasinoSessionMgr.def.BUFFER_GetAll(pars);
            }

            if (getCount)
            {
                result.IsOk = true;
                long cmbAmountTot = longSupportStage <= 1 ? casinoMovimentiBuffers[0].CMB_AMOUNT_TOT : casinoMovimentiBuffers[0].CMB_AMOUNT_TOT_LONG;
                result["COUNT"] = casinoMovimentiBuffers.Count > 0 ? cmbAmountTot : 0L;
            }
            else
            {
                long BonusBetAmount = 0;
                long BonusWinAmount = 0;
                long CashBetAmount = 0;
                long CashWinAmount = 0;
                int PlayBonusBetAmount = 0;
                int PlayBonusWinAmount = 0;

                Dictionary<string, List<CasinoMovimentiBuffer>> allMovByExtId = new Dictionary<string, List<CasinoMovimentiBuffer>>();
                SortedList<long, string> allMovsExtIds = new SortedList<long, string>();
                DateTime firstOpUtc = DateTime.MinValue;
                DateTime lastOpUtc = DateTime.MinValue;
                foreach (CasinoMovimentiBuffer mov in casinoMovimentiBuffers)
                {
                    string logical_movExtId = string.IsNullOrEmpty(mov.CMB_EXTTXREFID) ? mov.CMB_EXTTXID : mov.CMB_EXTTXID;
                    logical_movExtId += "@#@" + mov.CMB_EXTCODE;
                    if (!allMovByExtId.ContainsKey(logical_movExtId))
                        allMovByExtId[logical_movExtId] = new List<CasinoMovimentiBuffer>();
                    allMovByExtId[logical_movExtId].Add(mov);

                    if (!allMovsExtIds.ContainsKey(mov.CMB_ID))
                        allMovsExtIds[mov.CMB_ID] = logical_movExtId;

                    switch (mov.CMB_TYPE)
                    {
                        case (int)CasinoMovimentiBuffer.Type.Win:
                            CashWinAmount += longSupportStage <= 1 ? mov.CMB_AMOUNT_TOT : mov.CMB_AMOUNT_TOT_LONG;
                            PlayBonusWinAmount += mov.CMB_AMOUNT_PLAYBONUS;
                            break;
                        case (int)CasinoMovimentiBuffer.Type.Loose:
                            if (longSupportStage <= 1)
                            {
                                CashBetAmount += (-(mov.CMB_AMOUNT_TOT - mov.CMB_AMOUNT_BONUS - mov.CMB_AMOUNT_PLAYBONUS));
                                BonusBetAmount += (-mov.CMB_AMOUNT_BONUS);
                            }
                            else
                            {
                                CashBetAmount += (-(mov.CMB_AMOUNT_TOT_LONG - mov.CMB_AMOUNT_BONUS_LONG - mov.CMB_AMOUNT_PLAYBONUS));
                                BonusBetAmount += (-mov.CMB_AMOUNT_BONUS_LONG);
                            }
                            PlayBonusBetAmount += (-mov.CMB_AMOUNT_PLAYBONUS);
                            break;
                        default:
                            throw new Exception("Internal Inconsistence: betType not supported:" + mov.CMB_TYPE);
                    }

                    if (mov.CMB_OPDATE_UTC < firstOpUtc || firstOpUtc == DateTime.MinValue)
                        firstOpUtc = mov.CMB_OPDATE_UTC;
                    if (mov.CMB_OPDATE_UTC > lastOpUtc)
                        lastOpUtc = mov.CMB_OPDATE_UTC;
                }

                result.IsOk = true;
                if (!isPlayBonusSub)
                {
                    result["BonusBetAmount"] = BonusBetAmount;
                    result["BonusWinAmount"] = BonusWinAmount;
                    result["CashBetAmount"] = CashBetAmount;
                    result["CashWinAmount"] = CashWinAmount;
                }
                else
                {
                    result["BonusBetAmount"] = 0;
                    result["BonusWinAmount"] = 0;
                    result["CashBetAmount"] = PlayBonusBetAmount;
                    result["CashWinAmount"] = PlayBonusWinAmount;
                }

                result["SessionToken"] = specificSessionToken;
                result["StartDate"] = firstOpUtc == DateTime.MinValue ? DateTime.MinValue : firstOpUtc.ToLocalTime();
                result["EndDate"] = lastOpUtc == DateTime.MinValue ? DateTime.MinValue : lastOpUtc.ToLocalTime();
                result["EndDateUtc"] = lastOpUtc == DateTime.MinValue ? DateTime.MinValue : lastOpUtc;

                ArrayList allSessionBets = new ArrayList();
                if (allMovsExtIds.Count > 0)
                {
                    foreach (string extId in allMovsExtIds.Values)
                    {
                        Hashtable betItem = new Hashtable();
                        betItem["BetID"] = extId;
                        List<CasinoMovimentiBuffer> singleBetMovs = allMovByExtId[extId];
                        BonusBetAmount = 0;
                        BonusWinAmount = 0;
                        CashBetAmount = 0;
                        CashWinAmount = 0;
                        PlayBonusBetAmount = 0;
                        PlayBonusWinAmount = 0;
                        DateTime lastOpUtc2 = DateTime.MinValue;
                        foreach (CasinoMovimentiBuffer mov in singleBetMovs)
                        {
                            switch (mov.CMB_TYPE)
                            {
                                case (int)CasinoMovimentiBuffer.Type.Win:
                                    CashWinAmount += longSupportStage <= 1 ? mov.CMB_AMOUNT_TOT : mov.CMB_AMOUNT_TOT_LONG;
                                    PlayBonusWinAmount += mov.CMB_AMOUNT_PLAYBONUS;
                                    break;
                                case (int)CasinoMovimentiBuffer.Type.Loose:
                                    if (longSupportStage <= 1)
                                    {
                                        CashBetAmount += (-(mov.CMB_AMOUNT_TOT - mov.CMB_AMOUNT_BONUS - mov.CMB_AMOUNT_PLAYBONUS));
                                        BonusBetAmount += (-mov.CMB_AMOUNT_BONUS);
                                    }
                                    else
                                    {
                                        CashBetAmount += (-(mov.CMB_AMOUNT_TOT_LONG - mov.CMB_AMOUNT_BONUS_LONG - mov.CMB_AMOUNT_PLAYBONUS));
                                        BonusBetAmount += (-mov.CMB_AMOUNT_BONUS_LONG);
                                    }
                                    PlayBonusBetAmount += (-mov.CMB_AMOUNT_PLAYBONUS);
                                    break;
                                default:
                                    throw new Exception("Internal Inconsistence: betType not supported:" + mov.CMB_TYPE);
                            }

                            if (mov.CMB_OPDATE_UTC > lastOpUtc2)
                                lastOpUtc2 = mov.CMB_OPDATE_UTC;
                        }

                        if (!isPlayBonusSub)
                        {
                            betItem["BonusBetAmount"] = BonusBetAmount;
                            betItem["BonusWinAmount"] = BonusWinAmount;
                            betItem["CashBetAmount"] = CashBetAmount;
                            betItem["CashWinAmount"] = CashWinAmount;
                        }
                        else
                        {
                            betItem["BonusBetAmount"] = 0;
                            betItem["BonusWinAmount"] = 0;
                            betItem["CashBetAmount"] = PlayBonusBetAmount;
                            betItem["CashWinAmount"] = PlayBonusWinAmount;
                        }
                        betItem["EndDate"] = lastOpUtc2 == DateTime.MinValue ? DateTime.MinValue : lastOpUtc2.ToLocalTime();
                        betItem["EndDateUtc"] = lastOpUtc2 == DateTime.MinValue ? DateTime.MinValue : lastOpUtc2;
                        allSessionBets.Add(betItem);
                    }
                }

                result["BETS"] = allSessionBets;
            }
            return result;
        }

        public override HashResult getStartUrl(EndUser eu, Games game, HashParams otherParams)
        {
            bool fun = otherParams.getTypedValue("forFunSession", false);

            if (game == null) return new HashResult { ErrorMessage = "Missing Game" };
            if (!fun && eu == null) return new HashResult { ErrorMessage = "Missing EndUser" };

            if (eu != null)
                CasinoExtIntMgr.startUrlInfos[$"{eu.EU_UID}@{CasinoAMSwDispatchTest.AMSWFACEKEY}"] = new HashParams("GAME_KEY", game.GAME_KEY);

            return fun
                ? BuildDemoStartUrl(game, otherParams)
                : BuildRealStartUrl(eu, game, otherParams);
        }

        public virtual HashResult BuildRealStartUrl(EndUser eu, Games game, HashParams otherParams)
        {
            HashResult result = new HashResult();

            string playType = otherParams.getTypedValue("playType", "", false);
            bool hasPromo = false;
            string defenceCode;
            string userId;
            string closeUrl = string.Empty;
            string client = otherParams.getTypedValue("isMobile", false) ? "mobile" : "desktop";
            string gameId = game.GAME_EI_KEY;
            string portalCode = playType.Equals("FUN") ? CcConfig.AppSettingStr("CASINOAM_FUN_PORTAL_CODE", "") : CcConfig.AppSettingStr("CASINOAM_PORTAL_CODE", "");
            string language = CcConfig.AppSettingStr("CASINOAM_STARTURL_LANG", "it");
            string country = CcConfig.AppSettingStr("CASINOAM_STARTURL_COUNTRY", "it");
            string casinoPlatform = ExtIntMgr.PlatformKeys.CASINOAM;
            string url;
            EndUser local_eu = new EndUser { EU_ID = eu.EU_ID, unl = true };
            if (eu.EU_ID > 0)
                local_eu.GetById();

            // user-selected lobby close url
            if (otherParams.ContainsKey("bloc"))
            {
                string bloc = otherParams.getTypedValue("bloc", "");
                int idx = bloc.IndexOf("cmn/game.aspx?", StringComparison.Ordinal);
                if (idx >= 0)
                    bloc = bloc.Substring(0, idx) + "cmn/game.aspx?redirAll=" +
                           StringMgr.def.EncryptString(bloc.Substring(idx + "cmn/game.aspx?".Length));

                closeUrl = CcConfig.AppSettingBool("CASINOAM_SET_POSTMESSAGE_EXIT_IN_GAME_LAUNCH", true) ? CcConfig.AppSettingStr("CASINOAM_CLOSEURL_FOR_POSTMESSAGE_ACTIVATION", "com.egt-bg.postMessage") : CcConfig.AppSettingBool("TEMP_CASINO_AM_LOBBY_URL_USE_UNENCODED", true)
                    ? bloc
                    : HttpUtility.UrlEncode(bloc);
            }

            string oneTimeTokenBase = $"{_Encode_ExtToken(local_eu, otherParams).get("CLEAN_TOKEN", "", false)}#{new NetCodInfo(Servers.GetCurrentServer().SERVER_ID).getCode()}";
            // Recupera estensione e genera DefenceCode (token normale + timestamp)
           
            HashResult upRes = _CasinoAM_UpdateUserTokenOnDb(local_eu, oneTimeTokenBase, otherParams);
            if (!upRes.IsOk) return upRes;

            // DefenceCode = token normale + #yyyyMMddHHmmss (UTC) -> cifrato
            string ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string oneTimeTokenWithTs = oneTimeTokenBase + "|" + ts;
            defenceCode = StringMgr.def.EncryptString(oneTimeTokenWithTs);
            userId = "" + upRes["userId"]; // EUEI_KEY
            string cachekey = $"{defenceCode}X{userId}";

            // Cache 2 minuti (one-shot usato da /authenticate)
            defenceCodeCache[cachekey] = defenceCode;
            string urlTemplate = string.Empty;

            if (playType == "FUN")
            {
                urlTemplate = CcConfig.AppSettingStr("CASINOAM_FUN_URL_TEMPLATE_GENERAL", "https://{amusnet-mgs-domain}/bonus-platform/operator/v1?defenceCode={defenceCode}&playerId={euId}&portalCode={portalCode}&language={language}&country={country}&gameId={gameId}&screenName={nickName}&hasPromo={hasPromo}&client={client}&closeurl={closeurl}"
                );
            }
            else
            {
                urlTemplate = CcConfig.AppSettingStr("CASINOAM_URL_TEMPLATE_GENERAL",  "https://{amusnet-mgs-domain}/api/v2/MGL?defenceCode={defenceCode}&playerId={euId}&portalCode={portalCode}&language={language}&country={country}&gameId={gameId}&screenName={nickName}&hasPromo={hasPromo}&client={client}&closeurl={closeurl}"
                );
            }

            url = urlTemplate
                .Replace("{amusnet-mgs-domain}", playType == "FUN" ? CcConfig.AppSettingStr("CASINOAM_FUN_STARTURL_GAMESERVER", "") : CcConfig.AppSettingStr("CASINOAM_STARTURL_GAMESERVER", ""))
                .Replace("{defenceCode}", defenceCode)
                .Replace("{euId}", userId)
                .Replace("{portalCode}", portalCode)
                .Replace("{language}", language)
                .Replace("{country}", country)
                .Replace("{gameId}", gameId)
                .Replace("{nickName}", eu.EU_FRIENDLY_NAME)
                .Replace("{hasPromo}", hasPromo.ToString())
                .Replace("{client}", client)
                .Replace("{closeurl}", closeUrl);

            result.IsOk = true;
            result["TOKEN"] = defenceCode;
            result["URL"] = url;
            result["startType"] = "auto";
            return result;
        }

        public virtual HashResult BuildDemoStartUrl(Games game, HashParams otherParams)
        {
            HashResult result = new HashResult();

            string demoToken = string.Empty;
            string closeUrl = string.Empty;
            string client = otherParams.getTypedValue("isMobile", false) ? "mobile" : "desktop";
            string gameId = game.GAME_EI_KEY;
            string language = CcConfig.AppSettingStr("CASINOAM_STARTURL_LANG", "it");
            string casinoPlatform = ExtIntMgr.PlatformKeys.CASINOAM;
            string url;

            // 1) Prima: richiesta OGO demo al provider
            SmartHash response = _callAMAPI("auth_OGO_demo", SmartHash.byPP(
                "casinoOperatorId",  CcConfig.AppSettingInt("CASINOAM_DEMO_OPERATOR_ID", -1),
                "username", CcConfig.AppSettingStr("CASINOAM_DEMO_OPERATOR_USERNAME", ""),
                "password", CcConfig.AppSettingStr("CASINOAM_DEMO_OPERATOR_PASSWORD", "")
            ));

            if (response.IsOk && string.IsNullOrEmpty(response.ErrorMessage))
            {
                demoToken = ((Cam_OGOAuthResponse)response["CALL_RESULT"]).gameLaunchToken;
            }
            else
            {
                throw new Exception("AM ApiCall::auth_OGO_demo::" + response.ErrorMessage);
            }

            // 2) closeurl
            string bloc = otherParams.getTypedValue("bloc", "");
            int idx = bloc.IndexOf("cmn/game.aspx?", StringComparison.Ordinal);
            if (idx >= 0)
                bloc = bloc.Substring(0, idx) + "cmn/game.aspx?redirAll=" +
                        StringMgr.def.EncryptString(bloc.Substring(idx + "cmn/game.aspx?".Length));

            closeUrl = CcConfig.AppSettingBool("CASINOAM_SET_POSTMESSAGE_EXIT_IN_GAME_LAUNCH", true) ? CcConfig.AppSettingStr("CASINOAM_CLOSEURL_FOR_POSTMESSAGE_ACTIVATION", "com.egt-bg.postMessage") : CcConfig.AppSettingBool("TEMP_CASINO_AM_LOBBY_URL_USE_UNENCODED", true)
                    ? bloc
                    : HttpUtility.UrlEncode(bloc);


            // 3) URL template e Replace IDENTICI al tuo codice (demo)
            string urlTemplate = CcConfig.AppSettingStr("CASINOAM_DEMO_URL_TEMPLATE_GENERAL",
                casinoPlatform,
                "https://{amusnet-mgs-domain}.com/gl/{operatorName}?gameLaunchToken={demoToken}&gameId={gameId}&language={language}&client={client}&closeurl={closeUrl}"
            );

            url = urlTemplate
                .Replace("{amusnet-mgs-domain}", CcConfig.AppSettingStr("CASINOAM_DEMO_STARTURL_GAMESERVER", ""))
                .Replace("{operatorName}", CcConfig.AppSettingStr("CASINOAM_DEMO_OPERATOR_USERNAME", "").ToLower())
                .Replace("{demoToken}", demoToken)
                .Replace("{gameId}", gameId)
                .Replace("{language}", language)
                .Replace("{client}", client)
                .Replace("{closeUrl}", closeUrl);

            result.IsOk = true;
            result["TOKEN"] = "";      // in demo non serve defence code
            result["URL"] = url;
            result["startType"] = "auto";
            return result;
        }

        #region history
        public override HashResult getHistoryUrl(EndUser eu, Games game, string betId, HashParams extPars)
        {
            HashResult result = new HashResult();
            try
            {
                if (betId.EndsWith("_B") || betId.EndsWith("_W"))
                    betId = betId.Substring(0, betId.Length - 2);
                EndUserExtInt euExt = getUserExtension(eu, game, extPars);
                if (euExt != null && euExt.flagRecordFound)
                {
                    string method = "open_history";
                    SmartHash methodPars = SmartHash.byPP("transferId", betId);

                    SmartHash apiRes = _callAMAPI(method, methodPars);
                    if (apiRes.IsOk)
                    {
                        Cam_HistoryResponse callRes = (Cam_HistoryResponse)apiRes["CALL_RESULT"];

                        if (!string.IsNullOrEmpty(callRes.Outcome.OutcomeUrl))
                        {
                            result["URL"] = callRes.Outcome.OutcomeUrl;
                            result["HTMLRESULT"] = JsonConvert.SerializeObject(callRes);
                            result.IsOk = true;
                        }
                        else result.ErrorMessage = "Invalid apicall result";
                    }
                    else result.ErrorMessage = "ApiCall::" + apiRes.ErrorMessage;
                }
                else result.ErrorMessage = "AM Player not found:" + eu.EU_UID;
            }
            catch (Exception exc)
            {
                Log.exc(exc);
                result.ErrorMessage = "EXC:::" + exc.Message;
            }

            return result;
        }

        #endregion history

        public override HashResult getPendingSessions(EndUser eu, Games game, HashParams extPars)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            HashResult res = new HashResult { IsOk = true };
            long countOpen;
            using (new GGConnMgr.CentralCtxWrap("CASINO"))
            {
                int centEuId;
                if (extPars.ContainsKey("centEu"))
                    centEuId = extPars.getTypedValue("centEu", new EndUser()).EU_ID;
                else
                    centEuId = extPars.getTypedValue("centEu", -1);
                CasinoMovimentiBuffer mov = CasinoSessionMgr.def.BUFFER_GetAll(
                    new HashParams(
                        "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                        "CMB_FK_FST_MATCH_Id", extPars.getTypedValue("fstMatchId", -1),
                        "CMB_FK_EU_Id", centEuId,
                        "CMB_STATEs", new[] { (int)CasinoMovimentiBuffer.States.Dumped, (int)CasinoMovimentiBuffer.States.PreCommitted },
                        "CMB_TYPEs", new[] { (int)CasinoMovimentiBuffer.Type.Loose, (int)CasinoMovimentiBuffer.Type.Win },
                        "getCount", true
                    )
                ).First();

                countOpen = longSupportStage <= 1 ? mov.CMB_AMOUNT_TOT : mov.CMB_AMOUNT_TOT_LONG;
            }
            if (countOpen > 0)
                res["PENDING"] = new ArrayList { "DUMMY" };
            else
                res["PENDING"] = new ArrayList();
            if (extPars.ContainsKey("playType"))
                res["playType"] = extPars["playType"];
            return res;
        }

        public override HashResult completePendingSessions(EndUser eu, Games game, HashParams extPars)
        {
            List<string> allOpenIds;
            using (new GGConnMgr.CentralCtxWrap("CASINO"))
            {
                allOpenIds = CasinoSessionMgr.def.BUFFER_GetAll(
                    new HashParams(
                        "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                        "CMB_FK_FST_MATCH_Id", extPars.getTypedValue("fstMatchId", -1),
                        "CMB_FK_EU_Id", extPars.getTypedValue("centEu", new EndUser()).EU_ID,
                        "CMB_STATEs", new[] { (int)CasinoMovimentiBuffer.States.Dumped },
                        "CMB_TYPEs", new[] { (int)CasinoMovimentiBuffer.Type.Loose, (int)CasinoMovimentiBuffer.Type.Win }
                    )
                ).Select(cmb => "" + cmb.CMB_ID).ToList();
            }

            if (allOpenIds.Count > 0)
            {
                SqlConnection conn = null;
                try
                {
                    Log.getLogger("CasinoAMInvalids").Debug("ClosingPendingIds:{0}", string.Join(",", allOpenIds));

                    conn = GGConnMgr.def.GetSqlConnection("CASINO");
                    SqlCommand cmd = new SqlCommand(
                        string.Format("UPDATE CASINO_E_MOVIMENTI_BUFFER SET CMB_STATE=@CMB_STATE WHERE CMB_ID IN ({0})", string.Join(",", allOpenIds)),
                        conn
                    );
                    cmd.Parameters.AddWithValue("@CMB_STATE", (int)CasinoMovimentiBuffer.States.PreCommitted);
                    conn.Open();
                    DataMgr2.executeTracedNonQuery(cmd);
                    conn.Close();
                    conn = null;
                }
                catch (Exception exc1)
                {
                    if (conn != null) conn.Close();
                    Log.exc(new Exception("CLOSE PENDINGS Error updating records - inner", exc1));
                    throw;
                }
            }

            return new HashResult { IsOk = true };
        }

        public override bool delayedRetrySitoutOnInconsistency(HashParams extPars)
        {
            return false;
        }

        public override EndUserExtInt getUserExtension(EndUser eu, Games game, HashParams extPars)
        {
            string elgUserName = _getAMUserName(eu, game, extPars);
            EndUserExtInt res = (EndUserExtInt)_staticDataCache["AMUSER_" + elgUserName];
            if (res == null || extPars.getTypedValue("skipCache", false))
            {
                res = ExtIntMgr.def.getEuExtInt(eu, ExtIntMgr.def.getCasinoAM_EI(), new HashParams("EUEI_KEY", _getAMUserName(eu, game, extPars)));
                if (res != null)
                    _staticDataCache["AMUSER_" + elgUserName] = res;
            }
            return res;
        }

        public override ExternalIntegration getExtIntegration(HashParams extPars)
        {
            return ExtIntMgr.def.getCasinoAM_EI();
        }

        public override HashResult logOut(EndUser eu, Games game, HashParams extPars)
        {
            HashResult nop = new HashResult();
            nop.IsOk = true;

            bool postSitoutLogout = extPars.getTypedValue("postSitoutLogout", false);

            if (postSitoutLogout)
            {
                if (CcConfig.AppSettingBool("CASINOAM_MAKELOGOUT_ASYNCH", true))
                    new CasinoSessionMgr.BUFFER_UpdateAllDelegate(
                        CasinoSessionMgr.def.BUFFER_UpdateAll).BeginInvoke(
                        extPars.getTypedValue("fstMatchId", -1),
                        new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_STATE", (int)CasinoMovimentiBuffer.States.Committed,
                            "newCMB_STATE", (int)CasinoMovimentiBuffer.States.Completed,
                            "blockNum", CcConfig.AppSettingInt("CASINOAM_MAKELOGOUT_BLOCKNUM", 1000)
                        ), null, null);
                else
                    CasinoSessionMgr.def.BUFFER_UpdateAll(
                        extPars.getTypedValue("fstMatchId", -1),
                        new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_STATE", (int)CasinoMovimentiBuffer.States.Committed,
                            "newCMB_STATE", (int)CasinoMovimentiBuffer.States.Completed,
                            "blockNum", CcConfig.AppSettingInt("CASINOAM_MAKELOGOUT_BLOCKNUM", 1000)
                        ));
            }
            return nop;
        }

        public override HashResult getPlayDetail(EndUser eu, Games game, string betId, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult logIn(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region External API Interaction & Helpers

        public virtual string BasicAuthorization(string auth)

        {
            byte[] bytes = Encoding.ASCII.GetBytes(auth);
            string b64 = Convert.ToBase64String(bytes);
            string headerValue = "Basic " + b64;
            return headerValue;
        }

        public virtual HttpClientManager _getApiSvc(string apiType, SmartHash auxPars)
        {
            HttpClientManager sc = new HttpClientManager(false);
            HttpUrlCfg urlCfg = new HttpUrlCfg(_getApiCfgKey(apiType, string.Empty, auxPars), new HashParams("url", ""));
            urlCfg.setClient(sc);
            auxPars["_URLCFG"] = urlCfg;
            return sc;
        }

        public virtual string _getApiCfgKey(string apiType, string key, SmartHash auxPars)
        {
            string prefix;
            if (auxPars.get("useFunCfg", false))
                prefix = "CASINOAM_{0}_API{1}_FUNMODE";
            else if (auxPars.get("useAltCfg", false))
                prefix = "CASINOAM_{0}_API{1}_ALT";
            else
                prefix = "CASINOAM_{0}_API{1}";
            string result = prefix;
            if (key.Length > 0)
                result = prefix + "_" + key;
            string cfgForPartnerRepl = string.Empty;
            string cfgForPartner = auxPars.get("cfgForPartner", string.Empty);
            if (CcConfig.AppSettingStr("CASINOAM_DEFAULT_PARTNER", "XXXX") == cfgForPartner)
                cfgForPartner = string.Empty;
            if (cfgForPartner.Length > 0)
                cfgForPartnerRepl = "_FOR" + cfgForPartner;
            return string.Format(result, apiType, cfgForPartnerRepl);
        }

        public virtual SmartHash _getMethodInfos()
        {
            if (_methodInfos == null)
            {
                string[][] rawMethodInfos =
                {
                    new[] {"GET     ", "XAPITOKEN          ", "open_history                ", "/gameplay-history/v2/transfer-details?transferId={transferId}                                                                                                   ", "                                  "},
                    new[] {"GET     ", "XAPITOKEN          ", "get_jackpot_vals            ", "/jackpot-stats/statistics/{portalUid}?currency={currency}&winnersCount={winnersCount}&jackpotTypes={jackpotTypes}                                               ", "                                  "},
                    new[] {"GET     ", "XAPITOKEN          ", "get_games                   ", "/bonus-platform/operator/v1/game-details?gins={gameGIN}&portalName={portalName}&currency={currency}                                                             ", "                                  "},
                    new[] {"POSTJSON", "OGODEMO            ", "auth_OGO_demo               ", "/auth                                                                                                                                                           ", "casinoOperatorId,username,password"},
                    new[] {"GET     ", "SIMPLE             ", "get_hashcode                ", "/checksums-2025.json                                                                                                                                            ", "                                  "},
                    new[] {"POSTJSON", "SIMPLE             ", "complianceAuth              ", "/api/v1/login                                                                                                                                                   ", "username,password                 "}
                };

                SmartHash result = new SmartHash();
                foreach (string[] rawMethodInfo in rawMethodInfos)
                {
                    string httpMethod = rawMethodInfo[0].Trim();
                    string apiType = rawMethodInfo[1].Trim();
                    string method = rawMethodInfo[2].Trim();
                    string rawRouteStr = rawMethodInfo[3].Trim();
                    string[] requiredPostPars0 = rawMethodInfo[4].Split(',');

                    SmartHash requiredVars = new SmartHash();
                    string[] urlQueryParts = rawRouteStr.Split('?');
                    string[] urlParts = urlQueryParts[0].Split('/');
                    foreach (string urlPart in urlParts)
                    {
                        if (urlPart.StartsWith("{") && urlPart.EndsWith("}"))
                        {
                            string requiredPar = urlPart.Substring(1, urlPart.Length - 2);
                            requiredVars[requiredPar] = requiredPar;
                        }
                    }
                    if (urlQueryParts.Length > 1)
                    {
                        SmartHash queryHash = new SmartHash(urlQueryParts[1], false, "&=");
                        foreach (string qVal in queryHash.Values)
                        {
                            if (qVal.StartsWith("{") && qVal.EndsWith("}"))
                            {
                                string requiredPar = qVal.Substring(1, qVal.Length - 2);
                                requiredVars[requiredPar] = requiredPar;
                            }
                        }
                    }
                    SmartHash requiredPostPars = new SmartHash();
                    foreach (string reqPar in requiredPostPars0)
                        requiredPostPars[reqPar.Trim()] = reqPar.Trim();

                    result[method] = SmartHash.byPP(
                        "rawUrl", rawRouteStr,
                        "apiType", apiType,
                        "httpMethod", httpMethod,
                        "requiredPars", requiredVars,
                        "requiredPostPars", requiredPostPars
                    );
                }
                _methodInfos = result;
            }
            return _methodInfos;
        }

        public virtual SmartHash _callAMAPI(string method, SmartHash pars, SmartHash auxPars = null)
        {
            if (auxPars == null) auxPars = new SmartHash();
            SmartHash result = new SmartHash { IsOk = false };

            SmartHash allMethodInfos = _getMethodInfos();
            if (allMethodInfos.ContainsKey(method))
            {
                #region Resolve method & config
                SmartHash methodInfos = allMethodInfos.get(method, (SmartHash)null, false);
                SmartHash requiredPars = methodInfos.get("requiredPars", (SmartHash)null, false);
                SmartHash requiredPostPars = methodInfos.get("requiredPostPars", (SmartHash)null, false);
                string apiType = methodInfos.get("apiType", (string)null, false);

                string rootUrl = CcConfig.AppSettingStr(_getApiCfgKey(apiType, "PATH_URL", auxPars), "") + methodInfos.get("rawUrl", string.Empty, false);
                string httpMethod = methodInfos.get("httpMethod", string.Empty, false).Trim();

                //if (auxPars.get("useFunCfg", false))
                //    auxPars["useFunCfg"] = CcConfig.AppSettingBool("CASINOAM_USE_ALTERNATE_CFG_FOR_FUNMODE", true);

                #endregion Resolve method & config

                #region fill security
                if (requiredPostPars.ContainsKey("secureLogin") && !pars.ContainsKey("secureLogin"))
                    pars["secureLogin"] = CcConfig.AppSettingStr(_getApiCfgKey(apiType, "SECURELOGIN", auxPars), string.Empty).Trim();

                if (requiredPars.ContainsKey("secureLogin") && !pars.ContainsKey("secureLogin"))
                    pars["secureLogin"] = CcConfig.AppSettingStr(_getApiCfgKey(apiType, "SECURELOGIN", auxPars), string.Empty).Trim();

                #endregion fill security

                #region Validate required params

                bool allOk = true;

                ArrayList reqPars = new ArrayList(requiredPars.Keys);
                for (int i = 0; i < reqPars.Count && allOk; i++)
                {
                    allOk = pars.ContainsKey(reqPars[i]) || ("" + reqPars[i]).Equals("hash");
                    if (!allOk)
                        result.ErrorMessage = string.Format("CallServiceProvder: Missing required parameter {0}", reqPars[i]);
                }

                ArrayList reqPPars = new ArrayList(requiredPostPars.Keys);
                for (int i = 0; i < reqPPars.Count && allOk; i++)
                {
                    if (String.IsNullOrEmpty(reqPPars[i].ToString()))
                    {
                        continue;
                    }
                    allOk = pars.ContainsKey(reqPPars[i]) || ("" + reqPPars[i]).Equals("hash");
                    if (!allOk)
                        result.ErrorMessage = string.Format("CallServiceProvder: Missing required post parameter {0}", reqPPars[i]);
                }

                #endregion Validate required params

                #region main procedure

                if (allOk)
                {
                    #region Build URL & body

                    SmartHash pars2 = new SmartHash(pars);
                    for (int i = 0; i < reqPars.Count; i++)
                    {
                        string parName = "{" + reqPars[i] + "}";
                        if (rootUrl.Contains(parName) && parName != "{hash}")
                        {
                            rootUrl = rootUrl.Replace(parName, "" + pars[reqPars[i]]);
                            pars2.Remove(reqPars[i]);
                        }
                    }

                    string body;
                    SmartHash postPars = new SmartHash(pars2);
                    postPars.AutoEscape = CcConfig.AppSettingBool("CASINOAM_CALLAMAPI_USE_AUTOESCAPE_SMARTHASH_POSTPARS", false);

                    if (httpMethod.Equals("GET"))
                    {
                        body = string.Empty;
                        if (!rootUrl.Contains("?"))
                            rootUrl += "?";
                        if (pars2.Count > 0)
                            rootUrl += pars2.ToString("&=");
                    }
                    else
                    {
                        body = JsonConvert.SerializeObject(postPars);
                    }

                    #endregion Build URL & body

                    #region client and security headers

                    HttpClientManager sc = _getApiSvc(apiType, auxPars);

                    switch (apiType)
                    {
                        //case "COMPLIANCE":
                        //    sc.customRequestHeaders["Authorization"] = _callAMAPI("complianceAuth", SmartHash.byPP("username",  CcConfig.AppSettingStr("CASINOAM_USERNAME", ""), "password", CcConfig.AppSettingStr("CASINOAM_PASSWORD", "")));
                        //    goto default;
                        case "XAPITOKEN":
                            sc.customRequestHeaders["X-API-TOKEN"] = CcConfig.AppSettingStr("CASINOAM_XAPITOKEN", "").Trim();
                            goto default;
                        case "OGODEMO":
                            sc.customRequestHeaders["Referrer"] = CcConfig.AppSettingStr("CASINOAM_REFERRER", "").Trim();
                            goto default;
                        case "SIMPLE":
                            goto default;
                        default:
                            if (method == "get_hashcode")
                            {
                                sc.customRequestHeaders["Authorization"] = BasicAuthorization(CcConfig.AppSettingStr(_getApiCfgKey(apiType, "SECURELOGIN", auxPars), string.Empty).Trim());
                            }
                            sc.customRequestHeaders["DateUtc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                            sc.customRequestHeaders["Content-Type"] = "application/json";
                            break;
                    }

                    #endregion client and security headers

                    #region true call and response handling

                    string rawResponse;
                    try
                    {
                        string server = CcConfig.AppSettingStr(_getApiCfgKey(apiType, "SERVER", auxPars), "");

                        switch (httpMethod)
                        {
                            case "POST":
                                sc.UseDotNetPostMode = true;
                                rawResponse = sc.callPOST(server + rootUrl, body);
                                break;
                            case "POSTJSON":
                                rawResponse = sc.callPOST(server + rootUrl, body);
                                break;
                            case "GET":
                                rawResponse = sc.callGET(server + rootUrl, body);
                                break;
                            default:
                                throw new Exception("Unsupported method:" + httpMethod);
                        }

                        string dumpUrl = rootUrl;
                        if (CcConfig.AppSettingBool("CASINOAM_CALLSVC_PROVIDER_DEBUG_BODY", false)) dumpUrl += "?" + body;
                        def._DumpWsCall(method, dumpUrl, sc.lastStatusCode + ":" + rawResponse);

                        if (sc.lastStatusCode != HttpStatusCode.OK)
                            result.ErrorMessage = "CALL_ERROR:" + sc.lastStatusCode;
                        else
                        {
                            result.IsOk = true;
                            switch (method)
                            {
                                case "open_history":
                                    result["CALL_RESULT"] = JsonConvert.DeserializeObject<Cam_HistoryResponse>(rawResponse);
                                    break;
                                case "get_jackpot_vals":
                                    result["CALL_RESULT"] = JsonConvert.DeserializeObject<SmartHash>(rawResponse);
                                    break;
                                case "get_games":
                                    result["CALL_RESULT"] = JsonConvert.DeserializeObject<List<Cam_CasinoGame>>(rawResponse);
                                    break;
                                case "auth_OGO_demo":
                                    result["CALL_RESULT"] = JsonConvert.DeserializeObject<Cam_OGOAuthResponse>(rawResponse);
                                    break;
                                case "get_hashcode":
                                    result["CALL_RESULT"] = JsonConvert.DeserializeObject<List<Cam_HashCodes>>(rawResponse);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Log.exc(exc);
                        def._DumpWsCall(method, rootUrl, "EXC:" + exc.Message);
                        result.ErrorMessage = "CALL_EXC:" + exc.Message;
                    }

                    #endregion true call and response handling
                }
                #endregion main procedure
            }
            else result.ErrorMessage = string.Format("Method {0} not found", method);
            return result;
        }

        #endregion External API Interaction & Helpers

        #region Session, User, and Balance Management
        /// <summary>
        /// Persist plaintext token into EndUserExtInt.EUEI_TOKEN and returns userId (EUEI_KEY) in result["userId"].
        /// No game/otherParams needed if you already have euExt.
        /// </summary>
        private HashResult _CasinoAM_UpdateUserTokenOnDb(EndUser local_eu, string tokenPlain, HashParams auxPars)
        {
            try {
                Games game = new Games();
                EndUserExtInt euExt = getUserExtension(local_eu, game, auxPars);
                if (euExt == null || !euExt.flagRecordFound)
                    return new HashResult { ErrorMessage = "Missing EndUserExtInt" };

                if (string.IsNullOrWhiteSpace(tokenPlain))
                    return new HashResult { ErrorMessage = "Invalid token (empty)" };

                // Persist token in plaintext (as requested)
                EndUserExtInt euExtUpd = new EndUserExtInt { EUEI_ID = euExt.EUEI_ID };
                euExtUpd.EUEI_TOKEN = tokenPlain;
                euExtUpd.Update();

                var res = new HashResult { IsOk = true };
                res["userId"] = euExt.EUEI_KEY;     // what you need for defenceCode cachekey
                res["EUEI_ID"] = euExt.EUEI_ID;     // optional, useful for debug
                return res;
            }
            catch (Exception ex)
            {
                Log.getLogger("CasinoAM").Warn("CasinoAM_UpdateUserTokenOnDb error: {0}", ex);
                return new HashResult { ErrorMessage = "DB update failed" };
            }
        }



        public virtual void _manageOldSessionEnds(string casinoPlatform, int euId, int centEuId, SmartHash auxPars)
        {
            try
            {
                List<CasinoSubscription> lastDeletedSubs;
                using (new GGConnMgr.AlternateCtxWrap("CASINO"))
                {
                    lastDeletedSubs = CasinoSessionMgr.def.getAllSubscriptions(new HashParams(
                        "EU_ID", centEuId,
                        "MATCH_STATEs", new[] { (int)CasinoSubscription.States.Deleted, (int)CasinoSubscription.States.ValidButNotCurrent },
                        "GAME_KEY", auxPars.get("GAME_KEY", string.Empty),
                        "MATCH_PGA_ORDINAL", 1,
                        "top", CcConfig.AppSettingInt("CASINOSW_SEAMLESS_WALLET_SEND_STARTSESSION_PRE_EXC_FIX_NSESSION", 10),
                        "getFromLast", true //last 10
                    ));
                }

                for (int i = lastDeletedSubs.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        CasinoSubscription sub_i = lastDeletedSubs[i];
                        HashParams endSessionAuxPars = new HashParams();
                        endSessionAuxPars["fstMatchId"] = sub_i.MATCH_Id;
                        string GameMode = "CASH";
                        Games game;
                        string extToken;
                        using (new GGConnMgr.CentralCtxWrap("CASINO"))
                        {
                            extToken = "" + CBSLWalletMgr.def.getActualExtToken("" + sub_i.MATCH_EXTCASINO_REFS, "").dbToken[0];
                            if (sub_i.getReferenceSession(true).CSES_PLAYBONUS_TABLE > 0)
                                GameMode = "FUN";
                            game = sub_i.getReferenceSession(true).ReferenceCasinoTableDef.getReferenceGame(true);
                        }

                        if (extToken.Length > 0)
                            endSessionAuxPars["extToken"] = extToken;
                        endSessionAuxPars["GameId"] = game.GAME_KEY;
                        endSessionAuxPars["GameName"] = game.GAME_NAME;
                        endSessionAuxPars["GameExtKey"] = game.GAME_EI_KEY;
                        endSessionAuxPars["GameMode"] = GameMode;
                        endSessionAuxPars["CasinoSessionId"] = CasinoBigExtWsAccountMgr.def.Encrypt_CasinoSessionId(endSessionAuxPars.getTypedValue("fstMatchId", -1, false), GameMode, new HashParams("generateSessionId_fixedVariant", "s"));
                        endSessionAuxPars["extService"] = (int)FinMovExtern.Servizi.CasinoAM;
                        CBSLWalletMgr.def.SessionEndNotification(euId, endSessionAuxPars);
                    }
                    catch (Exception logContinue)
                    {
                        Log.exc(logContinue);
                    }
                }
            }
            catch (Exception logContinue2)
            {
                Log.exc(logContinue2);
            }
        }

        public virtual HashResult _closeRound_Core(CasinoMovimentiBuffer debitOp)
        {
            HashResult result = new HashResult();
            using (new GGConnMgr.CentralCtxWrap("CASINO"))
            {
                SqlConnection conn = null;
                try
                {
                    if (CcConfig.AppSettingBool("TEMP_CASINOAM_NEW_CLOSE_ROUND_LOGIC", true))
                    {
                        HashParams getPars = new HashParams(
                                "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                                "CMB_FK_FST_MATCH_Id", debitOp.CMB_FK_FST_MATCH_Id,
                                "CMB_FK_EU_Id", debitOp.CMB_FK_EU_Id,
                                "CMB_STATE", (int)CasinoMovimentiBuffer.States.Dumped
                        );
                        bool closeByRoundRef = CcConfig.AppSettingBool("CASINOAM_CLOSEROUND_BYROUNDREF", true);
                        if (closeByRoundRef)
                            getPars["CMB_EXTROUNDREF"] = debitOp.CMB_EXTROUNDREF;
                        List<CasinoMovimentiBuffer> allOpenRounds = CasinoSessionMgr.def.BUFFER_GetAll(getPars).ToList();

                        if (allOpenRounds.Count > 0)
                        {
                            conn = GGConnMgr.def.GetSqlConnection(); //CASINO CONTEXT
                            SqlCommand cmd = new SqlCommand(
                                string.Format("UPDATE CASINO_E_MOVIMENTI_BUFFER SET CMB_STATE={0}, CMB_CLOSED={1} WHERE CMB_ID IN ({2})",
                                    (int)CasinoMovimentiBuffer.States.PreCommitted,
                                    (int)CasinoMovimentiBuffer.Closed.Closed,
                                    string.Join(",", allOpenRounds.Select(cmb => "" + cmb.CMB_ID).ToArray())),
                                conn
                            );
                            conn.Open();
                            DataMgr2.executeTracedNonQuery(cmd);
                            conn.Close();
                            conn = null;
                        }
                        result.IsOk = true;
                    }
                    else
                    {
                        bool closeByRoundRef = CcConfig.AppSettingBool("CASINOAM_CLOSEROUND_BYROUNDREF", true);
                        conn = GGConnMgr.def.GetSqlConnection();
                        SqlCommand cmd = new SqlCommand(
                            "UPDATE CASINO_E_MOVIMENTI_BUFFER SET CMB_STATE=@CMB_STATE_NEW, CMB_CLOSED=@CMB_CLOSED " +
                            "WHERE CMB_FK_FST_MATCH_Id=@CMB_FK_FST_MATCH_Id AND CMB_STATE=@CMB_STATE AND CMB_SERVICE=@CMB_SERVICE " + (closeByRoundRef ? "AND CMB_EXTROUNDREF=@CMB_EXTROUNDREF" : "")
                            , conn);
                        cmd.Parameters.AddWithValue("@CMB_STATE_NEW", (int)CasinoMovimentiBuffer.States.PreCommitted);
                        cmd.Parameters.AddWithValue("@CMB_CLOSED", (short)(int)CasinoMovimentiBuffer.Closed.Closed);

                        cmd.Parameters.AddWithValue("@CMB_FK_FST_MATCH_Id", debitOp.CMB_FK_FST_MATCH_Id);
                        cmd.Parameters.AddWithValue("@CMB_STATE", (int)CasinoMovimentiBuffer.States.Dumped);
                        cmd.Parameters.AddWithValue("@CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM);
                        if (closeByRoundRef)
                            cmd.Parameters.AddWithValue("@CMB_EXTROUNDREF", debitOp.CMB_EXTROUNDREF);

                        conn.Open();
                        DataMgr2.executeTracedNonQuery(cmd);
                        conn.Close();
                        conn = null;
                        result.IsOk = true;
                    }
                }
                catch (Exception exc)
                {
                    Log.exc(exc);
                    if (conn != null) conn.Close();
                    result.ErrorMessage = "EXC:" + exc.Message;
                }
            }
            return result;
        }

        public virtual HashResult _closeRound(CasinoMovimentiBuffer debitOp)
        {
            HashResult result = new HashResult();
            if (CcConfig.AppSettingBool("TEMP_CASINOSW_CLOSE_MULTIPLE", true))
            {
                try
                {
                    //retry 3 times wait 1sec, no max wait
                    PatternLib.def.RetryUntil(
                        3, 1000, -1, () => result.IsOk = _closeRound_Core(debitOp).IsOk
                    );
                }
                catch (Exception exc)
                {
                    Log.exc(exc);
                    result.ErrorMessage = "EXC:" + exc.Message;
                }
            }
            else result = _closeRound_Core(debitOp);
            return result;
        }

        public virtual string _getAMUserName(EndUser eu, Games game, HashParams auxPars)
        {
            return string.Join("@", CcConfig.AppSettingStr("CASINOEXTINTAM_ENVIRONMENT", "AD"), eu.EU_UID, auxPars.getTypedValue("playType", "CASH"));
        }

        public virtual HashParams _getSessionInfosByTicket(string ticket, EndUser local_eu, string idTransazione, _MethNames methName, HashParams auxPars = null)
        {
            if (auxPars == null)
                auxPars = new HashParams();

            HashParams sessionInfos = (HashParams)sessionInfosByTicketCache[ticket];
            if (sessionInfos == null)
            {
                sessionInfos = new HashParams();
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    CasinoSubscription fstSub = null;
                    CasinoSubscription currSub = null;
                    List<CasinoSubscription> allSubs = CasinoSessionMgr.def.getAllSubscriptions(new HashParams(
                        "MATCH_PGA_TICKET", ticket
                    ));
                    for (int i = 0; i < allSubs.Count && (fstSub == null || currSub == null); i++)
                    {
                        CasinoSubscription sub_i = allSubs[i];
                        if (currSub == null && sub_i.MATCH_State == (int)CasinoSubscription.States.Current)
                            currSub = sub_i;
                        if (fstSub == null && sub_i.MATCH_PGA_ORDINAL == 1)
                            fstSub = sub_i;
                    }
                    if (fstSub == null)
                    {
                        string msg = string.Format("TICKET_NOT_FOUND#Ticket non trovato:" + ticket);
                        _LogDebug(10, msg, local_eu, idTransazione, methName);
                        throw new Exception(msg);
                    }

                    if (currSub == null && !auxPars.getTypedValue("skipSessionStateCheck", false))
                    {
                        string msg = string.Format("SESSION_INVALID_STATE#Sessione utente in stato non valido per ticket:" + ticket);
                        _LogDebug(10, msg, local_eu, idTransazione, methName);
                        throw new Exception(msg);
                    }

                    sessionInfos["EU_UID"] = local_eu.EU_UID;
                    sessionInfos["MATCH_Id"] = fstSub.MATCH_Id;
                    sessionInfos["centEU_Id"] = fstSub.MATCH_FK_EU_Id;
                    CasinoSession session = fstSub.referenceSession;
                    sessionInfos["CSES_Id"] = session.CSES_Id;
                    sessionInfos["CSES_PLAYBONUS_TABLE"] = session.CSES_PLAYBONUS_TABLE;
                    bool funSession = session.CSES_PLAYBONUS_TABLE > 0;
                    sessionInfos["funSession"] = funSession;
                    sessionInfos["freeRoundSession"] = funSession && fstSub.MATCH_FR_START > 0;
                    sessionInfos["MATCH_FR_VALUE"] = fstSub.MATCH_FR_VALUE;
                    sessionInfos["TAB_Id"] = session.getReferenceTable(true).TAB_Id;
                    Games refGame = session.ReferenceCasinoTableDef.getReferenceGame(true);
                    sessionInfos["GAME_KEY"] = refGame.GAME_KEY;
                    sessionInfos["GAME_ISLIVE"] = refGame.GAME_ISLIVE;
                    sessionInfos["GAME_EI_KEY"] = refGame.GAME_EI_KEY;

                    sessionInfos["SWTOKEN"] = CasinoBigExtWsAccountMgr.def.Encrypt_CasinoSessionId(fstSub.MATCH_Id, funSession ? "FUN" : "CASH", new HashParams("generateSessionId_fixedVariant", "s"));
                    sessionInfos["EXTTOKEN"] = "" + fstSub.MATCH_EXTCASINO_REFS;

                    sessionInfos["PARTITION"] = session.CSES_PARTITION;

                    CasinoSitPlace sitPlace = CasinoSessionMgr.def.getCurrentTable(new HashParams(
                        "centralEu", new EndUser { EU_ID = fstSub.MATCH_FK_EU_Id },
                        "extInt", ExtIntMgr.def.getCasinoAM_EI()
                        ));
                    if (sitPlace != null && sitPlace.flagRecordFound)
                        sessionInfos["TABUSER_Id"] = sitPlace.TABUSER_Id;
                }
                sessionInfosByTicketCache[ticket] = sessionInfos;
            }
            return sessionInfos;
        }

        public virtual HashParams _getCurrentSessionInfo(int euId, string gameExtKey, HashParams auxPars = null)
        {
            if (auxPars == null)
                auxPars = new HashParams();

            HashParams result = new HashParams();
            EndUser local_eu = auxPars.getTypedValue("_local_eu", (EndUser)null);
            if (local_eu == null)
            {
                local_eu = new EndUser { EU_ID = euId, useNoLockWhePossible = true };
                local_eu.GetById();
            }

            EndUser centEu = new EndUser { EU_UID = local_eu.GetCentralEndUserName(), useNoLockWhePossible = true };
            CasinoSitPlace casinoSitPlace = null;
            bool multiSession = CcConfig.AppSettingBool("CASINOAM_MULTISESSION_ENABLE",  false);

            Games game = null;
            if (multiSession && !string.IsNullOrEmpty(gameExtKey))
            {
                game = _getActualGameByGameEiKey(local_eu, gameExtKey);
            }

            using (new GGConnMgr.CentralCtxWrap("CASINO"))
            {
                centEu.getByUserId();
                if (multiSession && game != null)
                {
                    casinoSitPlace = CasinoSessionMgr.def.getCurrentTable(
                        new HashParams(
                            "casinoPlatform", ExtIntMgr.PlatformKeys.CASINOAM,
                            "centralEu", centEu,
                            "extInt", getExtIntegration(new HashParams()),
                            "game", new Games { GAME_KEY = game.GAME_KEY }
                        )
                    );
                }
                else
                {
                    casinoSitPlace = CasinoSessionMgr.def.getCurrentTable(
                        new HashParams(
                            "centralEu", centEu,
                            "extInt", getExtIntegration(new HashParams())
                        )
                    );
                }
            }

            if (casinoSitPlace != null && casinoSitPlace.flagRecordFound)
            {
                string cacheKey = "INFOS_BY_TABUSER_" + casinoSitPlace.TABUSER_Id;
                Hashtable otherInfos = (Hashtable)gameDecodeCache[cacheKey];
                if (otherInfos == null)
                {
                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    {
                        CasinoTable table = new CasinoTable { TAB_Id = casinoSitPlace.TABUSER_FK_TAB_ID, useNoLockWhenPossible = true };
                        table.GetById();
                        CasinoSubscription fstSub = new CasinoSubscription { MATCH_Id = casinoSitPlace.TABUSER_FK_MATCH_Id, useNoLockWhenPossible = true };
                        fstSub.GetById();
                        if (table.flagRecordFound && fstSub.flagRecordFound)
                        {
                            otherInfos = new Hashtable();
                            otherInfos["game"] = table.referenceGame;
                            otherInfos["sub"] = fstSub;
                            otherInfos["ticket"] = fstSub.MATCH_PGA_TICKET;
                            gameDecodeCache[cacheKey] = otherInfos;
                        }
                    }
                }

                Games centGame = null;
                if (otherInfos != null)
                {
                    result = new HashParams(otherInfos) { { "casinoSitPlace", casinoSitPlace } };
                    centGame = otherInfos["game"] as Games;
                }

                if (CcConfig.AppSettingBool("TEMP_FIX_CASINOS_GETCURRSESS_CHECKGAME", true) && ("" + gameExtKey).Length > 0 && centGame != null && !("" + centGame.GAME_EI_KEY).Equals(gameExtKey) && !CasinoAM_GameISJackpot(gameExtKey))
                {
                    Log.def.Warn("Received wrong gameId:" + gameExtKey + " expecting:" + centGame.GAME_EI_KEY);
                    result = null;
                }
            }
            return result;
        }

        public virtual bool CasinoAM_GameISJackpot(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var list = CcConfig.AppSettingStr("CASINOAM_JACKPOT_GAME_KEYS", "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);

            return list.Contains(key.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public virtual SessionBalancesHolder _loadRealSessionBalancesBySessInfos(string ticket, HashParams sessionInfos, HashParams auxPars = null)
        {
            int longSupportStage = CcConfig.AppSettingInt("LONG_SUPPORT_STAGE", 2);
            if (auxPars == null)
                auxPars = new HashParams();
            SessionBalancesHolder result = new SessionBalancesHolder();

            int _avoidLoop_ = auxPars.getTypedValue("_avoidLoop_", 0);
            if (_avoidLoop_ > 2)
                throw new Exception("_loadRealSessionBalancesBySessInfos loop");
            _avoidLoop_++;
            auxPars["_avoidLoop_"] = _avoidLoop_;
            string gameKey = sessionInfos.getTypedValue("GAME_KEY", string.Empty);

            bool forceSessionBalanceForLive = CcConfig.AppSettingBool("CASINOAM_AUTHENTICATE_USE_LOCALSESSION_BALANCE", false) && sessionInfos.getTypedValue("GAME_ISLIVE", 0) == 1;
            bool getLocalBalances = auxPars.getTypedValue("_getLocalBalances_", forceSessionBalanceForLive);
            bool getBalancesForSitout = auxPars.getTypedValue("_getBalancesForSitout_", false);

            bool loadBalancesFromLocalSessionInfos = !useSeamlessExternalIntegration("balance", sessionInfos) || getLocalBalances;
            bool loadBalancesFromRemoteSessionInfos = !loadBalancesFromLocalSessionInfos;
            bool justAddRemoteSessionBalanceAsAccountBalance = loadBalancesFromLocalSessionInfos && auxPars.getTypedValue("loadAccountBalanceFromSession", false);
            if (justAddRemoteSessionBalanceAsAccountBalance)
                loadBalancesFromRemoteSessionInfos = true;

            string EU_UID = sessionInfos.getTypedValue("EU_UID", string.Empty);
            bool funSession = sessionInfos.getTypedValue("funSession", false, false);
            HashParams userBalancePars = new HashParams(
                "GameId", gameKey,
                "casinoPlatform", ExtIntMgr.PlatformKeys.CASINOAM
            );

            if (CcConfig.AppSettingBool("CASINOAM_RECOVER_EUEXTTOKEN_FOR_BALANCE", false))
            {
                EndUser localEu = new EndUser { EU_UID = EU_UID, unl = true };
                localEu.getByUserId();
                if (localEu.EU_ID > 0)
                {
                    EndUserExtInt euExt = getUserExtension(localEu, null, new HashParams("playType", funSession ? "FUN" : "CASH", "skipCache", true));
                    if (euExt.flagRecordFound)
                    {
                        string[] pp = (euExt.EUEI_AUX_DATA + "##").Split('#');
                        userBalancePars["CasinoSessionId"] = "fk" + StringMgr.def.EncryptString("fake_" + gameKey);
                        userBalancePars["extToken"] = pp[1];
                        if (gameKey.Length == 0)
                            userBalancePars["GameId"] = gameKey = pp[2];
                    }
                }
            }

            if (loadBalancesFromLocalSessionInfos)
            {
                bool freeRoundSession = sessionInfos.getTypedValue("freeRoundSession", false, false);
                CasinoMovimentiBuffer sumMov;
                CasinoMovimentiBuffer sumMovFr = null;
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    HashParams getAllPars = new HashParams(
                        "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                        "CMB_FK_FST_MATCH_Id", sessionInfos.getTypedValue("MATCH_Id", -1, false),
                        "CMB_FK_EU_Id", sessionInfos.getTypedValue("centEU_Id", -1, false),
                        "CMB_STATEs", new[] { (int)CasinoMovimentiBuffer.States.Dumped, (int)CasinoMovimentiBuffer.States.PreCommitted, (int)CasinoMovimentiBuffer.States.Committed },
                        "CMB_IDMaxExcl", auxPars.getTypedValue("maxExtId", -1),
                        "getAggregatedSum", true,
                        "getAggregatedDetails", "cash,bonus,playbonus"
                    );
                    if (getBalancesForSitout)
                        getAllPars["excludeCMB_TYPEs"] = new[] { (int)CasinoMovimentiBuffer.Type.SitOut };
                    sumMov = CasinoSessionMgr.def.BUFFER_GetAll(getAllPars).First();
                    if (!getBalancesForSitout && freeRoundSession)
                    {
                        getAllPars["CMB_TYPEs"] = new[] { (int)CasinoMovimentiBuffer.Type.Win, (int)CasinoMovimentiBuffer.Type.WinCancel };
                        sumMovFr = CasinoSessionMgr.def.BUFFER_GetAll(getAllPars).First();
                    }
                }
                if (sumMov != null)
                {
                    result.amountTotal = longSupportStage <= 1 ? sumMov.CMB_AMOUNT_TOT : sumMov.CMB_AMOUNT_TOT_LONG;
                    result.bonus = longSupportStage <= 1 ? sumMov.CMB_AMOUNT_BONUS : sumMov.CMB_AMOUNT_BONUS_LONG;
                    result.funBonus = sumMov.CMB_AMOUNT_PLAYBONUS;
                    result.lastProgr = sumMov.CMB_PROGR;
                    if (freeRoundSession)
                    {
                        CasinoMovimentiBuffer lastCmb;
                        using (new GGConnMgr.CentralCtxWrap("CASINO"))
                        {
                            lastCmb = new CasinoMovimentiBuffer { CMB_ID = sumMov.CMB_ID, unl = true };
                            lastCmb.GetById();
                        }
                        result.lastRemFr = lastCmb.CMB_REM_FR;
                        if (sumMovFr != null)
                            result.amountTotalWin = longSupportStage <= 1 ? sumMovFr.CMB_AMOUNT_TOT : sumMovFr.CMB_AMOUNT_TOT_LONG;
                    }
                }

                if (auxPars.getTypedValue("loadAccountBalance", false))
                {
                    CBSLWalletBase.CallHandler callHandler = CBSLWalletMgr.def.WalletLink.AuxCall("GetUserBalance", EU_UID, userBalancePars);
                    if (callHandler.IsOk && callHandler.counterByName.ContainsKey("RESULT"))
                    {
                        result.accountBalance = callHandler.counterByName["RESULT"];
                    }
                    else
                    {
                        Log.def.Warn("Invalid GetBalance for uid:{0} error:{1}", EU_UID, callHandler.Error);
                        result.accountBalance = 0;
                    }
                }
            }

            if (loadBalancesFromRemoteSessionInfos)
            {
                int lastProgr = -1;
                long balance = -1;
                long accountBalance = -1;

                int fstMatchId = sessionInfos.getTypedValue("MATCH_Id", -1, false);
                string swToken = sessionInfos.getTypedValue("SWTOKEN", string.Empty);
                string extToken = sessionInfos.getTypedValue("EXTTOKEN", auxPars.getTypedValue("overrideExtToken", string.Empty));
                if (extToken.Length == 0)
                {
                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    {
                        CasinoSubscription fstSub = new CasinoSubscription { MATCH_Id = fstMatchId, unl = true };
                        if (fstMatchId >= 0)
                            fstSub.GetById();
                        swToken = CasinoBigExtWsAccountMgr.def.Encrypt_CasinoSessionId(fstMatchId, funSession ? "FUN" : "CASH", new HashParams("generateSessionId_fixedVariant", "s"));
                        extToken = "" + fstSub.MATCH_EXTCASINO_REFS;
                        lastProgr = (CasinoSessionMgr.def.BUFFER_GetAll(
                            new HashParams(
                                "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                                "CMB_FK_FST_MATCH_Id", sessionInfos.getTypedValue("MATCH_Id", -1, false),
                                "CMB_FK_EU_Id", sessionInfos.getTypedValue("centEU_Id", -1, false),
                                "CMB_STATEs", new[] { (int)CasinoMovimentiBuffer.States.Dumped, (int)CasinoMovimentiBuffer.States.PreCommitted, (int)CasinoMovimentiBuffer.States.Committed },
                                "CMB_IDMaxExcl", auxPars.getTypedValue("maxExtId", -1),
                                "top", 1,
                                "orderBy", "CMB_ID DESC"
                            )).FirstOrDefault() ?? new CasinoMovimentiBuffer()).CMB_PROGR;
                    }
                }

                try
                {
                    if (fstMatchId < 0)
                    {
                        balance = 0;
                        CBSLWalletBase.CallHandler callHandler = CBSLWalletMgr.def.WalletLink.AuxCall("GetUserBalance", EU_UID, userBalancePars);
                        if (callHandler.IsOk && callHandler.counterByName.ContainsKey("RESULT"))
                        {
                            accountBalance = callHandler.counterByName["RESULT"];
                            if (callHandler.counterByName.ContainsKey("TOTALBALANCE"))
                                accountBalance = callHandler.counterByName["TOTALBALANCE"];
                        }
                        else
                        {
                            Log.def.Warn("Invalid GetBalance for uid:{0} error:{1}", EU_UID, callHandler.Error);
                            accountBalance = 0;
                        }
                    }
                    else
                    {
                        CBSLWalletBase.CallHandler balanceResp = CBSLWalletMgr.def.WalletLink.GetBalance(
                            EU_UID,
                            new HashParams(
                                "CasinoSessionId", "" + swToken,
                                "extToken", "" + extToken,
                                "skipCache", true,
                                "GameId", gameKey,
                                "casinoPlatform", ExtIntMgr.PlatformKeys.CASINOAM
                            ));
                        balance = balanceResp.counterByName["RESULT"];
                        if (balanceResp.counterByName.ContainsKey("TOTALBALANCE"))
                            accountBalance = balanceResp.counterByName["TOTALBALANCE"];
                        else if (CcConfig.AppSettingBool("CASINOAM_BALANCE_GET_FULLBALANCE_ALLWAYS", false))
                        {
                            CBSLWalletBase.CallHandler callHandler = CBSLWalletMgr.def.WalletLink.AuxCall("GetUserBalance", EU_UID, userBalancePars);
                            if (callHandler.IsOk && callHandler.counterByName.ContainsKey("RESULT"))
                            {
                                accountBalance = callHandler.counterByName["RESULT"];
                            }
                            else
                            {
                                Log.def.Warn("Invalid GetBalance for uid:{0} error:{1}", EU_UID, callHandler.Error);
                                accountBalance = 0;
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    if (exc.Message.Contains("#SESSION_CLOSED#") && CcConfig.AppSettingBool("CASINOAM_SW_SESSCLOSE_BALANCE", true))
                    {
                        auxPars["_getLocalBalances_"] = true;
                        return _loadRealSessionBalancesBySessInfos(ticket, sessionInfos, auxPars);
                    }
                    Log.exc(exc);
                }

                if (!justAddRemoteSessionBalanceAsAccountBalance)
                {
                    result.lastProgr = lastProgr;
                    result.amountTotal = balance;
                    result.accountBalance = accountBalance;
                    if (funSession)
                        result.funBonus = Convert.ToInt32(balance);
                }
                else
                {
                    result.accountBalance = balance;
                }
            }

            return result;
        }

        public virtual SessionBalancesHolder _getFastSessionBalancesByTicket(string ticket, HashParams sessionInfos, HashParams auxPars = null)
        {
            if (auxPars == null)
                auxPars = new HashParams();

            bool forceSessionBalanceForLive = CcConfig.AppSettingBool("CASINOAM_AUTHENTICATE_USE_LOCALSESSION_BALANCE", false) && sessionInfos.getTypedValue("GAME_ISLIVE", 0) == 1;
            bool reload = auxPars.getTypedValue("reload", false) || forceSessionBalanceForLive;
            bool skipCacheWrite = auxPars.getTypedValue("skipCacheWrite", false);
            SessionBalancesHolder balances = new SessionBalancesHolder();
            HashParams balanceInfos = (HashParams)sessionFastBalanceByTicketCache[ticket];
            if (balanceInfos == null || reload)
            {
                balanceInfos = new HashParams("balance", balances = _loadRealSessionBalancesBySessInfos(ticket, sessionInfos, auxPars));
                if (!skipCacheWrite)
                    sessionFastBalanceByTicketCache[ticket] = balanceInfos;
            }
            else balances = balanceInfos.getTypedValue("balance", balances);
            return balances;
        }

        public virtual void _setFastSessionBalancesByTicket(string ticket, SessionBalancesHolder balanceHolder, HashParams sessionInfos)
        {
            try
            {
                sessionFastBalanceByTicketCache[ticket] = new HashParams("balance", balanceHolder); ;
            }
            catch (Exception ex)
            {
                Log.exc(ex);
            }
        }

        public virtual long _getSaldo(EndUser local_eu, Games locGame, bool isFunBonus, HashParams auxPars = null)
        {
            if (auxPars == null)
                auxPars = new HashParams();
            long saldo;
            bool extAccountMangeFunBonus = CcConfig.AppSettingBool("EXTACCOUNT_MANAGE_FUNBONUS", false);
            bool local_eu_ExtAccount = local_eu.EU_IS_EXTERNAL > 0 && (!isFunBonus || extAccountMangeFunBonus);
            if (local_eu_ExtAccount)
            {
                if (ExtAccountMgr.EXTACCOUNT_V2)
                {
                    if (isFunBonus)
                        saldo = 0;
                    else
                        saldo = ExtAccountMgr.def.getBalance(ExtAccountMgr.SendChannel.SaldiOnLine, ExtAccountMgr.ProductType.Casino, GameKeys.CASINOAM, local_eu.EU_UID, new Hashtable(), new HashParams("additionalCounters", new[] { "REALBONUS" }))[0];
                }
                else
                    saldo = EndUserAccountExtTx_Manager.
                        getInterface_Long(FinancialEndUserMov.__defaultExtInterface).
                        getCurrentAccount(local_eu.EU_UID, new HashParams("faceType", locGame.GAME_ACC_CAUS_KEY));
            }
            else
            {
                bool useAccKey = auxPars.getTypedValue("useAccKey", true);
                saldo = local_eu.GetSaldoContoTotale(useAccKey ? BonusAccount.getContoIdByGameCAUS_KEY(locGame.GAME_ACC_CAUS_KEY) : -1);
                if (isFunBonus)
                    saldo = (int)EndUserMgr.def.EndUser_GetSaldoFunBonus(local_eu, null, new HashParams("GAME_KEY", locGame.GAME_KEY));
            }
            return saldo;
        }

        #endregion

        #region Monitor, Concurrency & Data Helpers

        public virtual HashParams _acquireMonitor(string ticket)
        {
            HashParams monitor = null;
            DateTime startUtc = DateTime.UtcNow;
            long totalElapseMsecs = 0;
            do
            {
                lock (monitorByTicketCache)
                {
                    HashParams monitor0 = (HashParams)monitorByTicketCache[ticket];
                    if (monitor0 == null)
                        monitorByTicketCache[ticket] = monitor = new HashParams("ticket", ticket);
                }
                if (monitor == null)
                {
                    DateTime utcNow = DateTime.UtcNow;
                    totalElapseMsecs += (int)(utcNow - startUtc).TotalMilliseconds;
                    startUtc = utcNow;
                    Thread.Sleep(500);
                }
            } while (monitor == null && totalElapseMsecs < Cam_MAX_WAIT_ON_MONITOR_SECS * 1000);
            if (monitor == null)
                throw new Exception("CANNOT ENTER MONITOR FOR TICKET " + ticket);
            return monitor;
        }

        public virtual void _releaseMonitor(HashParams monitor)
        {
            if (monitor != null)
                lock (monitorByTicketCache)
                {
                    monitorByTicketCache.remove(monitor.getTypedValue<string>("ticket", null, false));
                }
        }

        public virtual CasinoMovimentiBuffer _getSingleOperation(HashParams sessionInfos, string extId)
        {
            int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
            string cacheKey = string.Format("{0}x{1}", MATCH_Id, extId);
            CasinoMovimentiBuffer commitedOp = (CasinoMovimentiBuffer)committedByExtIdCache[cacheKey];
            if (commitedOp == null)
            {
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    commitedOp = CasinoSessionMgr.def.BUFFER_GetAll(
                        new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_FK_FST_MATCH_Id", MATCH_Id,
                            "CMB_FK_EU_Id", sessionInfos.getTypedValue("centEU_Id", -1, false),
                            "CMB_STATEs", new[] { (int)CasinoMovimentiBuffer.States.Dumped, (int)CasinoMovimentiBuffer.States.PreCommitted, (int)CasinoMovimentiBuffer.States.Committed, (int)CasinoMovimentiBuffer.States.Deleted },
                            "CMB_EXTTXID", extId
                            )
                        ).FirstOrDefault();
                }
                if (commitedOp != null)
                    committedByExtIdCache[cacheKey] = commitedOp;
            }
            return commitedOp;
        }

        public virtual void _setSingleOperationInCache(HashParams sessionInfos, string extId, CasinoMovimentiBuffer committedOp)
        {
            int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
            string cacheKey = string.Format("{0}x{1}", MATCH_Id, extId);
            committedByExtIdCache[cacheKey] = committedOp;
        }

        public virtual List<CasinoMovimentiBuffer> _getOpenDebits(string method, HashParams sessionInfos, SmartHash auxPars)
        {
            bool TEMP_CasinoAM_FIX_BUFFER_ALL = CcConfig.AppSettingBool("TEMP_CASINOAM_FIX_BUFFER_ALL", false) && useSeamlessExternalIntegration(method, sessionInfos);
            if (TEMP_CasinoAM_FIX_BUFFER_ALL)
            {
                int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
                string cacheKey = string.Format("MM_{0}", MATCH_Id);
                SmartHash openRoundsInfo = (SmartHash)openRoundsByRoundRefCache[cacheKey];
                if (openRoundsInfo != null)
                {
                    if (openRoundsInfo.get("pendingRetry", false))
                        openRoundsInfo = null;
                }
                if (openRoundsInfo == null)
                {
                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    {
                        HashParams getBufferPars = new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_FK_FST_MATCH_Id", MATCH_Id,
                            "CMB_FK_EU_Id", sessionInfos.getTypedValue("centEU_Id", -1, false),
                            "orderBy", "CMB_ID"
                        );
                        List<CasinoMovimentiBuffer> allRounds = CasinoSessionMgr.def.BUFFER_GetAll(getBufferPars);
                        List<CasinoMovimentiBuffer> openRounds2 = new List<CasinoMovimentiBuffer>();
                        bool pendingRetry = false;
                        foreach (CasinoMovimentiBuffer cmb in allRounds)
                        {
                            if (cmb.CMB_TYPE == (int)CasinoMovimentiBuffer.Type.Loose && cmb.CMB_STATE == (int)CasinoMovimentiBuffer.States.Dumped && cmb.CMB_CLOSED == 0)
                                openRounds2.Add(cmb);
                            else if (CasinoMovimentiBuffer.pendingRetryStates.Contains(cmb.CMB_STATE))
                                auxPars["pendingRetry"] = pendingRetry = true;
                        }
                        openRoundsInfo = SmartHash.byPP(
                            "openRounds", openRounds2,
                            "pendingRetry", pendingRetry
                            );
                    }
                    openRoundsByRoundRefCache[cacheKey] = openRoundsInfo;
                }
                return new List<CasinoMovimentiBuffer>(openRoundsInfo.get("openRounds", new List<CasinoMovimentiBuffer>()));
            }
            else
            {
                int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
                string cacheKey = string.Format("M_{0}", MATCH_Id);
                List<CasinoMovimentiBuffer> openRounds = (List<CasinoMovimentiBuffer>)openRoundsByRoundRefCache[cacheKey];
                if (openRounds == null)
                {
                    using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    {
                        openRounds = CasinoSessionMgr.def.BUFFER_GetAll(new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_FK_FST_MATCH_Id", MATCH_Id,
                            "CMB_FK_EU_Id", sessionInfos.getTypedValue("centEU_Id", -1, false),
                            "CMB_TYPE", (int)CasinoMovimentiBuffer.Type.Loose,
                            "CMB_STATE", (int)CasinoMovimentiBuffer.States.Dumped,
                            "CMB_CLOSED", 0,
                            "orderBy", "CMB_ID"
                        ));
                    }
                    if (openRounds != null)
                        openRoundsByRoundRefCache[cacheKey] = openRounds;
                }

                return new List<CasinoMovimentiBuffer>(openRounds != null ? openRounds : new List<CasinoMovimentiBuffer>());
            }
        }

        public virtual void _setOpenDebits(string method, HashParams sessionInfos, List<CasinoMovimentiBuffer> openRounds)
        {
            bool TEMP_CasinoAM_FIX_BUFFER_ALL = CcConfig.AppSettingBool("TEMP_CASINOAM_FIX_BUFFER_ALL", false) && useSeamlessExternalIntegration(method, sessionInfos);
            if (TEMP_CasinoAM_FIX_BUFFER_ALL)
            {
                int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
                string cacheKey = string.Format("MM_{0}", MATCH_Id);
                if (openRounds == null)
                    openRoundsByRoundRefCache.remove(cacheKey);
                else
                {
                    SmartHash openRoundsInfo = (SmartHash)openRoundsByRoundRefCache[cacheKey];
                    if (openRoundsInfo != null)
                        openRoundsInfo["openRounds"] = openRounds;
                }
            }
            else
            {
                int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
                string cacheKey = string.Format("M_{0}", MATCH_Id);
                if (openRounds == null)
                    openRoundsByRoundRefCache.remove(cacheKey);
                else
                    openRoundsByRoundRefCache[cacheKey] = new List<CasinoMovimentiBuffer>(openRounds);
            }
        }

        public virtual void _clearOpenDebitsCache(string method, HashParams sessionInfos)
        {
            bool TEMP_CasinoAM_FIX_BUFFER_ALL = CcConfig.AppSettingBool("TEMP_CASINOAM_FIX_BUFFER_ALL", false) && useSeamlessExternalIntegration(method, sessionInfos);
            int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
            string cacheKey = TEMP_CasinoAM_FIX_BUFFER_ALL ? string.Format("MM_{0}", MATCH_Id) : string.Format("M_{0}", MATCH_Id);
            openRoundsByRoundRefCache.remove(cacheKey);
        }

        public virtual CasinoMovimentiBuffer _getStakeByRef(HashParams sessionInfos, string stakeRef)
        {
            int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
            string cacheKey = string.Format("{0}x{1}", MATCH_Id, stakeRef);

            CasinoMovimentiBuffer commitedOp = null;

            commitedOp = (CasinoMovimentiBuffer)stakeByRoundRef[cacheKey];

            if (commitedOp == null)
            {
                List<CasinoMovimentiBuffer> allRoundBets;
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    allRoundBets = CasinoSessionMgr.def.BUFFER_GetAll(
                        new HashParams(
                            "CMB_SERVICE", (int)FinMovExtern.Servizi.CasinoAM,
                            "CMB_FK_FST_MATCH_Id", MATCH_Id,
                            "CMB_FK_EU_Id", sessionInfos.getTypedValue("centEU_Id", -1, false),
                            "CMB_STATEs", new[] { (int)CasinoMovimentiBuffer.States.Dumped },
                            "CMB_CLOSED", 0,
                            "CMB_EXTROUNDREF", stakeRef
                        )
                    );
                }

                foreach (CasinoMovimentiBuffer casinoMovimentiBuffer in allRoundBets)
                {
                    if (casinoMovimentiBuffer.CMB_RELATED_FK_CMB_ID <= 0)
                    {
                        if (commitedOp == null)
                            commitedOp = casinoMovimentiBuffer;
                        else
                            Log.def.Warn("CasinoAM Multiple Round Unrealted Bets fstMatchId:{0} roundRef:{1}", MATCH_Id, stakeRef);
                    }
                }

                if (commitedOp == null && allRoundBets.Count > 0)
                    commitedOp = allRoundBets.LastOrDefault();
                if (commitedOp != null)
                    stakeByRoundRef[cacheKey] = commitedOp;
            }
            return commitedOp;
        }

        public virtual void _setStakeByRefInCache(HashParams sessionInfos, string stakeRef, CasinoMovimentiBuffer committedOp)
        {
            int MATCH_Id = sessionInfos.getTypedValue("MATCH_Id", -1, false);
            string cacheKey = string.Format("{0}x{1}", MATCH_Id, stakeRef);
            if (CcConfig.AppSettingBool("TEMP_FIX_STAKECACHE", true))
                stakeByRoundRef[cacheKey] = committedOp;
            else
                committedByExtIdCache[cacheKey] = committedOp;
        }

        public virtual SmartHash _getFastJackpotAccumulator(EndUser local_eu, HashParams sessionInfos, HashParams auxPars)
        {
            string GAME_KEY = sessionInfos.getTypedValue("GAME_KEY", string.Empty);
            string cacheKey = string.Format("{0}x{1}", local_eu.EU_ID, GAME_KEY);
            SmartHash currentAccumulator = (SmartHash)sessionFastJackpotByUserGame[cacheKey];
            if (currentAccumulator == null)
            {
                int casinoSvc = ExtIntMgr.def.getCasinoExtSvc(ExtIntMgr.CasinoKeys.CASINOAM);
                CasinoMovimentiBuffer lastMov = CasinoSessionMgr.def.BUFFER_GetAll(
                    new HashParams(
                        "top", 1,
                        "CMB_SERVICE", casinoSvc,
                        "CMB_FK_EU_Id", sessionInfos.getTypedValue("centEU_Id", -1, false),
                        "CMB_TYPE", (int)CasinoMovimentiBuffer.Type.Loose,
                        "CMB_STATEs", new[] { (int)CasinoMovimentiBuffer.States.Dumped, (int)CasinoMovimentiBuffer.States.PreCommitted, (int)CasinoMovimentiBuffer.States.Committed, (int)CasinoMovimentiBuffer.States.Completed },
                        "GAME_KEY", GAME_KEY,
                        "orderBy", "CMB_ID DESC",
                        "optionForceOrder", true
                    )
                ).FirstOrDefault() ?? new CasinoMovimentiBuffer();
                sessionFastJackpotByUserGame[cacheKey] = currentAccumulator = SmartHash.byPP("CMB_AMOUNT_JCKP_ACC", -lastMov.CMB_AMOUNT_JCKP_ACC);
            }
            return currentAccumulator;
        }

        public virtual void _setFastJackpotAccumulator(EndUser local_eu, HashParams sessionInfos, SmartHash currentAccumulator, HashParams auxPars)
        {
            string GAME_KEY = sessionInfos.getTypedValue("GAME_KEY", string.Empty);
            sessionFastJackpotByUserGame[string.Format("{0}x{1}", local_eu.EU_ID, GAME_KEY)] = currentAccumulator;
        }

        public virtual Games _getActualGameByGameEiKey(EndUser local_eu, string eiKey, SmartHash auxPars = null)
        {
            if (eiKey.Contains("(m)"))
                eiKey = eiKey.Remove(eiKey.IndexOf("(m)"), 3).Trim();

            Dictionary<string, Hashtable> gameMap;
            HashParams newGameInfo = new HashParams();
            //20230925 lucab NOTE only SV is false take it to true when SV has migrated on SE
            if (CcConfig.AppSettingBool("TEMP_CASINOAM_AUTHENT_USE_CORRECT_GAME", false))
            {
                EndUserLoginNew lastLogin = EndUserMgr.def.EndUserLogin_getLastAccessNew(1, local_eu.EU_ID).FirstOrDefault() ?? new EndUserLoginNew();
                int gameKeyFilter = lastLogin.EUL_MOBILE > 0 ? 1 : 0;
                gameMap = getAllAvailableGamesAll(new HashParams())["GAMEMAP"] as Dictionary<string, Hashtable>;
                string gameMapKey = eiKey + "x" + gameKeyFilter;
                if (gameMap != null && gameMap.ContainsKey(gameMapKey))
                    newGameInfo = gameMap[gameMapKey] as HashParams;
            }
            else
            {
                gameMap = getAvailableGames(new HashParams())["GAMEMAP"] as Dictionary<string, Hashtable>;
                if (gameMap != null && gameMap.ContainsKey(eiKey))
                    newGameInfo = gameMap[eiKey] as HashParams;
            }




            Games newGame = null;
            if (newGameInfo != null && newGameInfo.getTypedValue("gameKey", string.Empty).Length > 0)
            {
                newGame = new Games { GAME_KEY = newGameInfo.getTypedValue("gameKey", string.Empty), unl = true };
                newGame.GetByKey();
            }

            if (newGame == null)
                throw new Exception("Invalid game:" + eiKey);

            return newGame;
        }


        #endregion

        #region Logging and Utilities

        public virtual SmartHash _Encode_ExtToken(EndUser local_eu, HashParams auxPars)
        {
            string casinoPlatform = ExtIntMgr.PlatformKeys.CASINOAM;
            SmartHash result = new SmartHash() { IsOk = true };
            result["CLEAN_TOKEN"] = string.Join("@",
                    CcConfig.AppSettingStr("CASINOEXTINTAM_ENVIRONMENT", "AD"),
                    local_eu.EU_UID,
                    auxPars.getTypedValue("playType", "CASH"),
                    "" + auxPars.getTypedValue("matchId", -1, false)
                );
            result["TOKEN"] = StringMgr.def.EncryptString(
                (string)result["CLEAN_TOKEN"],
                CcConfig.AppSettingBool("CASINOEXTSW_AM_ENCODE_EXTTOKEN_FIXEDVARIANT", true) ? new HashParams("fixedVariant", "t") : null);
            return result;
        }

        public virtual void _LogDebug(int logLevel, string log, EndUser eu = null, string txId = null, _MethNames methName = _MethNames.NoName, bool makeWarn = false)
        {
            if (ENANCHE_DEBUG || makeWarn)
            {
                Log logger = Log.def;
                if (ENANCHE_DEBUG_LOG.Length > 0)
                    logger = Log.getLogger(ENANCHE_DEBUG_LOG);
                if (logLevel <= ENANCHE_DEBUG_LEVEL || makeWarn)
                {
                    string dataStr = string.Format("euId={0},txId={1},methName={2}", eu != null ? "" + eu.EU_ID : "", txId, methName);
                    string logStr = string.Format("{0}::{1}::[DATAS={2}]", typeof(CasinoExtIntAMSWCoreTest).Name, log, dataStr);
                    if (ENANCHE_DEBUG && logLevel <= ENANCHE_DEBUG_LEVEL)
                        logger.Debug(logStr);
                    if (makeWarn)
                        Log.warn(logStr);
                }
            }
        }

        public virtual void _DumpWsCall(string wsCALL, string overrideRequest = null, string overrideResponse = null)
        {
            string callPadded = ("" + wsCALL).PadRight(10, ' ');
            string callIdPadded = ("" + (_callId++)).PadLeft(5, '0');
            _LogDebug(100, string.Format("{0}::{1}::REQUEST ::{2}", callIdPadded, callPadded, string.IsNullOrEmpty(overrideRequest) ? BehaviorMgr.def.TraceRequest : overrideRequest));
            _LogDebug(100, string.Format("{0}::{1}::RESPONSE::{2}", callIdPadded, callPadded, string.IsNullOrEmpty(overrideResponse) ? BehaviorMgr.def.TraceResponse : overrideResponse));
        }

        public virtual string _ErrorRes(int nError, _MethSigns errorMethod, string errorDesc, bool warn = false)
        {
            int nErrorComplete = nError;
            nErrorComplete += ((int)errorMethod) * 100;
            string res = string.Join("::", "ERROR", ("" + nErrorComplete).PadLeft(5, '0'), ("" + errorMethod).PadLeft(6, '_'), errorDesc);
            if (warn)
                Log.warn("CasinoExtIntPNSWCore_WARN>>" + res);
            else
                Log.debug("CasinoExtIntPNSWCore_ERROR>>" + res);
            return res;
        }

        public virtual bool useSeamlessExternalIntegration(string method, HashParams sessionInfos, SmartHash auxPars = null)
        {
            bool isSeamlessExternalEnabled = CcConfig.AppSettingBool("CASINOAM_SEAMLESS_WALLET", false);
            if (isSeamlessExternalEnabled)
            {
                bool isFreeRoundSession = sessionInfos.getTypedValue("freeRoundSession", false);
                if (isFreeRoundSession && CcConfig.AppSettingBool("CASINOAM_FORCE_FREEROUND_INTERNAL", true))
                    isSeamlessExternalEnabled = false;
            }
            return isSeamlessExternalEnabled;
        }

        public virtual HashResult receiveCheckSums()
        {
            string algorithm = CcConfig.AppSettingStr("CASINOAM_HASH_ALGRITHM", "SHA-1");

            HashResult result = new HashResult();
            SortedList<string, SortedList<string, HashModuleStub>> resultMap = new SortedList<string, SortedList<string, HashModuleStub>>();
            Dictionary<string, GAME_E_GAMES> amGamesByEiCode = new Dictionary<string, GAME_E_GAMES>();

            SmartHash moduleVersionByGameKey00 = new SmartHash(CcConfig.AppSettingStr("CASINOAM_HASHMODULE_VERSION_BY_GK", ""));
            string moduleVersionDefault = CcConfig.AppSettingStr("CASINOAM_HASHMODULE_VERSION_DEFAULT", "1.0");
            Dictionary<string, decimal> moduleVersionByGameKey = new Dictionary<string, decimal>();
            //getting all master games
            List<GAME_E_GAMES> allAMGames = new GAME_E_GAMES().GetAll<GAME_E_GAMES>(-1, string.Empty, null, " GAME_PRODUCT_GAMEKEY='CASINOAM' AND ISNULL(GAME_ISPRODUCT,0)=0 AND GAME_KEY=GAME_MASTER_KEY ");
            foreach (GAME_E_GAMES amGame in allAMGames)
            {
                amGamesByEiCode["" + amGame.GAME_EI_KEY] = amGame;

                string gameModVersionS = moduleVersionDefault;
                if (moduleVersionByGameKey00.ContainsKey(amGame.GAME_MASTER_KEY))
                    gameModVersionS = moduleVersionByGameKey00.get(amGame.GAME_MASTER_KEY, "");

                decimal gameModVersion;
                if (!decimal.TryParse(gameModVersionS, out gameModVersion))
                    gameModVersion = 1.0m;

                moduleVersionByGameKey[amGame.GAME_MASTER_KEY] = gameModVersion;
            }

            //no partner info here
            List<string> checksumDataTypes = CcConfig.AppSettingStr("CASINOAM_REGULATOR_CHECKSUM_DATATYPES", "RNG").Split(',').ToList();
            List<Cam_HashCodes> ChecksumsGames = new List<Cam_HashCodes>();
            foreach (string checksumDataType in checksumDataTypes)
            {
                SmartHash currentChekData = _callAMAPI("get_hashcode", SmartHash.byPP());
                if (currentChekData.IsOk)
                {
                    ChecksumsGames =
                        currentChekData.get("CALL_RESULT", (List<Cam_HashCodes>)null, false);
                }
            }

            if (ChecksumsGames.Count > 0)
            {
                #region game related HASHCODES
                for (int i = 0; i < ChecksumsGames.Count; i++)
                {
                    foreach (string csComponentName in ChecksumsGames[i].checksums.Keys)
                    {
                        string compName = "" + csComponentName;
                        string hash = ChecksumsGames[i].checksums[csComponentName][algorithm];
                        string compNameUpp = compName.ToUpper();
                        string gameCode = "" + ChecksumsGames[i].GameId;

                        int gameCodeInt;
                        if (amGamesByEiCode.ContainsKey(gameCode)) //stub model with int only
                        {
                            GAME_E_GAMES locGame = amGamesByEiCode[gameCode];
                            if (int.TryParse(locGame.GAME_PGA_CODAAMS, out gameCodeInt))
                            {
                                if (!resultMap.ContainsKey(gameCode))
                                    resultMap[gameCode] = new SortedList<string, HashModuleStub>();
                                if (resultMap[gameCode].ContainsKey(compNameUpp))
                                    Log.def.Warn(string.Format(
                                        "Received multiple components for Game:{0} Component:{1}", gameCode,
                                        compNameUpp));
                                byte majorVersionB = 1, minorVersionB = 0;
                                if (moduleVersionByGameKey.ContainsKey(locGame.GAME_MASTER_KEY))
                                {
                                    decimal vv = moduleVersionByGameKey[locGame.GAME_MASTER_KEY];
                                    majorVersionB = (byte)Math.Truncate(vv);
                                    minorVersionB = (byte)((int)Math.Truncate(vv * 100.0m) % 100);
                                }

                                resultMap[gameCode][compNameUpp] = new HashModuleStub
                                {
                                    AAMS_CODICE = gameCodeInt,
                                    AAMS_ESTREMI = compName,
                                    AAMS_HASH = hash.ToUpper(),
                                    AAMS_MODULE = string.Format("PN_{0}_2_{1}", gameCodeInt, compNameUpp),
                                    AAMS_VERSION = majorVersionB,
                                    AAMS_SUBVERSION = minorVersionB
                                };
                            }
                        }
                    }
                }
                #endregion game related HASHCODES

                result["MAP"] = resultMap;
                string autosendUrl = CcConfig.AppSettingStr("CASINO_PN_HASHMODULE_AUTOSEND_URL", "LOCAL").Trim();
                if (autosendUrl.Length > 0 && resultMap.Count > 0)
                {
                    List<HashModuleStub> listModules = resultMap.Values.Select(gameHashes => gameHashes.Values.ToList()).SelectMany(x => x).ToList();
                    if (autosendUrl.Equals("LOCAL"))
                    {
                        HashMgr.def.receiveExternalHashes(
                            listModules.Select(hm => SmartHash.byPP(
                                "AAMS_MODULE", hm.AAMS_MODULE,
                                "AAMS_CODICE", hm.AAMS_CODICE,
                                "AAMS_ESTREMI", hm.AAMS_ESTREMI,
                                "AAMS_HASH", hm.AAMS_HASH,
                                "AAMS_VERSION", hm.AAMS_VERSION,
                                "AAMS_SUBVERSION", hm.AAMS_SUBVERSION
                            )).ToList());
                    }
                    else
                    {
                        HttpClientManager cm = new HttpClientManager(false);
                        string res = cm.callPOST(autosendUrl, JsonConvert.SerializeObject(listModules));
                        result.IsOk = res.Contains("OK");
                    }
                }
                else result.IsOk = true;
            }

            return result;
        }

        public virtual Hashtable _toHashComplete(object obj, bool extended, int depth, string[] excludes)
        {
            ArrayList allowedTypesSimples = new ArrayList(new Type[] { typeof(int), typeof(short), typeof(long), typeof(double), typeof(string), typeof(DateTime), typeof(decimal), typeof(bool), typeof(Hashtable), typeof(ArrayList), typeof(byte) });
            ArrayList allowedTypesCompl = new ArrayList(new Type[] { typeof(int), typeof(int[]), typeof(short), typeof(long), typeof(double), typeof(string), typeof(string[]), typeof(string[,]), typeof(DateTime), typeof(decimal), typeof(bool), typeof(Hashtable), typeof(Hashtable[]), typeof(ArrayList), typeof(byte) });
            ArrayList allowedTypes = extended ? allowedTypesCompl : allowedTypesSimples;

            if (obj == null) return new Hashtable();
            Hashtable res = new Hashtable();
            if (depth > CcConfig.AppSettingInt("CASINOEXTINTCBIGSW_TOHASH_MAXDEPTH", 3, "Massima profondita nella conversione di obj a hash per serializzazione verso front-end")) return res;

            if (obj.GetType() == typeof(Hashtable))
            {
                ArrayList allowedTypesForKey = new ArrayList(new Type[] { typeof(int), typeof(short), typeof(long), typeof(double), typeof(string), typeof(DateTime), typeof(decimal), typeof(bool), typeof(byte) });
                foreach (DictionaryEntry entry in (Hashtable)obj)
                {
                    Type keyType = entry.Key.GetType();
                    if (allowedTypesForKey.Contains(keyType))
                    {
                        res[entry.Key] = _toHashComplete(entry.Value, extended, depth + 1, excludes);
                    }
                }
            }
            else
            {
                PropertyInfo[] props = obj.GetType().GetProperties();
                foreach (PropertyInfo prop in props)
                {
                    bool exclude = false;
                    for (int i = 0; i < excludes.Length && !exclude; i++)
                    {
                        if (excludes[i].Length > 0 && excludes[i][0] == 'P')
                        {
                            exclude = prop.Name.StartsWith(excludes[i].Substring(1));
                        }
                    }
                    if (!exclude && prop.GetGetMethod() != null && prop.GetGetMethod().GetParameters().Length == 0)
                    {
                        Type propType = prop.PropertyType;
                        if (allowedTypes.IndexOf(propType) >= 0)
                        {
                            object value = prop.GetGetMethod().Invoke(obj, new object[] { });
                            if (value != null)
                            {
                                if (value.GetType() == typeof(Hashtable))
                                {
                                    res[prop.Name] = _toHashComplete(value, extended, depth + 1, excludes);
                                }
                                else if (value.GetType() == typeof(ArrayList))
                                {
                                    ArrayList propVal = new ArrayList();
                                    foreach (object prop1 in (ArrayList)value)
                                    {
                                        propVal.Add(_toHashComplete(prop1, extended, depth + 1, excludes));
                                    }
                                    res[prop.Name] = propVal;
                                }
                                else res[prop.Name] = value;
                            }
                        }
                    }
                }

                if (extended)
                {
                    FieldInfo[] flds = obj.GetType().GetFields();
                    foreach (FieldInfo fld in flds)
                    {
                        bool exclude = false;
                        for (int i = 0; i < excludes.Length && !exclude; i++)
                        {
                            if (excludes[i].Length > 0 && excludes[i][0] == 'F')
                            {
                                exclude = fld.Name.StartsWith(excludes[i].Substring(1));
                            }
                        }
                        if (!exclude && allowedTypes.Contains(fld.FieldType))
                        {
                            object value = fld.GetValue(obj);
                            if (value != null && !res.ContainsKey(fld.Name)) res[fld.Name] = value;
                        }
                    }
                }
            }
            return res;
        }

        public virtual HashParams getGameInfos(Games game)
        {
            return new HashParams(
                "gameId", game.GAME_EI_KEY,
                "game", _toHashComplete(game, false, 5, new string[] { }),
                "gameKey", game.GAME_KEY
            );
        }

        public virtual string _CalculateHash(string rqstQueryVars, SmartHash auxPars)
        {
            string hash;
            string operatorPassword = CcConfig.AppSettingStr("CASINOAM_SECUREPASSWORD", String.Empty).Trim();
            if (string.IsNullOrEmpty(operatorPassword))
                throw new Exception("Secure Password Not Found!");

            List<string> varsToList = rqstQueryVars.Split('&').ToList();
            IDictionary sl = new SortedList<string, string>();
            foreach (string item in varsToList)
            {
                sl.Add(item.Split('=')[0], item.Split('=')[1]);
            }

            sl.Remove("hash");
            List<string> items = new List<string>();
            foreach (DictionaryEntry de in sl)
            {
                items.Add(string.Concat(de.Key, "=", HttpUtility.UrlDecode(de.Value.ToString())));
            }
            string s = string.Join("&", items.ToArray());
            hash = getSignature(s + operatorPassword);
            return hash;
        }

        public virtual string getSignature(string signature)
        {
            return new Hashing(Hashing.Algorithms.MD5).ComputeHash_Hex(Encoding.ASCII.GetBytes(signature));
        }

        public virtual HashResult manageFreeRounds(string action, SmartHash auxPars)
        {
            HashResult result = new HashResult { IsOk = false };
            SmartHash apiRes = _callAMAPI(action, auxPars);
            if (apiRes.IsOk)
            {
                SmartHash callRes = apiRes["CALL_RESULT"] as SmartHash;
                if (callRes != null)
                {
                    string callResCode = callRes.get("error", "9999");
                    if (callResCode == "10")
                    {
                        if (action == "get_frb" || action == "cancel_frb" || action == "cancel_frb_v2")
                            callResCode = "0";
                    }
                    if (callResCode == "0")
                    {
                        if (action == "get_frb")
                        {
                            List<string> bCodes = new List<string>();
                            if (callRes.ContainsKey("bonuses"))
                            {
                                JArray bList = callRes["bonuses"] as JArray;
                                if (bList != null)
                                    foreach (JToken bObj0 in bList)
                                    {
                                        if (bObj0 is JObject)
                                        {
                                            string bCode = "" + ((JObject)bObj0)["bonusCode"];
                                            if (bCode.Length > 0)
                                                bCodes.Add(bCode);
                                        }
                                    }
                            }
                            result["bonusCodes"] = string.Join(",", bCodes.ToArray());
                        }
                        result.IsOk = true;
                    }
                    else result.ErrorMessage = "Invalid apicall [" + callRes.get("error", "9999") + ":" + callRes.get("description", "unk_error") + "]";
                }
                else result.ErrorMessage = "Invalid apicall result";
            }
            else result.ErrorMessage = "ApiCall::" + apiRes.ErrorMessage;
            return result;
        }

        public virtual HashResult resetFreeRounds(string playerId, SmartHash auxPars)
        {
            HashResult result = new HashResult { IsOk = true };
            HashResult frBalance = manageFreeRounds("get_frb", SmartHash.byPP("playerId", playerId));
            if (frBalance.IsOk)
            {
                string[] bonusCodes = ("" + frBalance["bonusCodes"]).Split(',');
                for (var i = 0; i < bonusCodes.Length && result.IsOk; i++)
                {
                    string bonusCode = ("" + bonusCodes[i]).Trim();
                    if (bonusCode.Length > 0)
                    {
                        string cancelAction = CcConfig.AppSettingBool("CASINOAM_USE_VARIABLE_FRB", false) ? "cancel_frb_v2" : "cancel_frb";
                        HashResult resetResult = manageFreeRounds(cancelAction, SmartHash.byPP("bonusCode", bonusCode));
                        if (!resetResult.IsOk)
                        {
                            result.IsOk = false;
                            result.ErrorMessage = "Cannot reset:" + resetResult.ErrorMessage;
                        }
                    }
                }
            }
            else
            {
                result.IsOk = false;
                result.ErrorMessage = "Cannot retrieve fr balance:" + frBalance.ErrorMessage;
            }
            return result;
        }

        public virtual void _pingTableSession_Central(HashParams sessionInfos, string author)
        {
            try
            {
                int TABUSER_Id = sessionInfos.getTypedValue("TABUSER_Id", -1);
                if (TABUSER_Id >= 0 && CcConfig.AppSettingBool("CASINOAM_ENABLE_SWOP_PING", true))
                    CasinoSessionMgr.def.Ping(TABUSER_Id, author);
            }
            catch (Exception logCont)
            {
                Log.exc(logCont);
            }
        }

        public virtual List<SmartHash> getGames(HashParams auxPars)
        {
            string method = "get_games";
            SmartHash methodPars = SmartHash.byPP(
                "gins", makeGinsArray(),
                "portalName", CcConfig.AppSettingStr("CASINOAM_PORTALNAME", ""),
                "currency", CcConfig.AppSettingStr("CASINOAM_CURRENCY", "EUR")
            );

            bool alignCoinLevels = auxPars.getTypedValue("alignCoinLevels", false);
            SortedList<string, SmartHash> gameInfosByEiKey = new SortedList<string, SmartHash>();
            SmartHash currentChekData = _callAMAPI(method, methodPars);
            if (currentChekData.IsOk)
            {
                List<Cam_CasinoGame> casinoGameList = currentChekData.get("CALL_RESULT", (List<Cam_CasinoGame>)null, false);
                foreach (Cam_CasinoGame casinoGame in casinoGameList)
                {
                    string gameId = casinoGame.gin.ToString();
                    if (!gameInfosByEiKey.ContainsKey(gameId))
                    {
                        gameInfosByEiKey[gameId] = SmartHash.byPP(
                            "GameId", gameId,
                            "Platform", casinoGame.portal.id,
                            "FrbAvailable", casinoGame.supportedBonusTypes.Any(bonus => bonus == "FREE_SPIN"),
                            "Currency", casinoGame.currency,
                            "TotalBetScales", casinoGame.betAmounts
                        );
                    }
                }
            }
            else
            {
                Log.def.Warn("CasinoAM_getGames:ERR on getCasinoGames:" + currentChekData.ErrorMessage);
            }
            return gameInfosByEiKey.Values.ToList();
        }

        public virtual List<int> makeGinsArray()
        {
            List<int> gins = new List<int>();
            HashResult gameListResult = getAvailableGames(new HashParams());
            if (gameListResult.IsOk)
            {
                Dictionary<string, Hashtable> gameList = (Dictionary<string, Hashtable>)gameListResult["GAMEMAP"];
                if (gameList != null)
                {
                    foreach (string gameId in gameList.Keys)
                    {
                        if (int.TryParse(gameId, out int gameIdInt))
                        {
                            gins.Add(gameIdInt);
                        }
                        else
                        {
                            Log.getLogger("CasinoAM").Warn("Cannot parse gameId to int:" + gameId + " in getGames");
                        }
                    }
                }
            }
            return gins;
        }

        #region hide 

        //public virtual SmartHash getJackpotValues(string gameKey, HashParams auxPars)
        //{
        //    string cacheKey = "JKPVAL";
        //    SortedList<string,  SmartHash> jackpotInfosByEiKey = (SortedList<string, SmartHash>)jackpotValueCache[cacheKey];
        //    if (jackpotInfosByEiKey == null)
        //    {
        //        jackpotInfosByEiKey = new SortedList<string, SmartHash>();

        //        string method = "get_jackpot_vals";
        //        SmartHash methodPars = SmartHash.byPP(
        //            "portalUid", CcConfig.AppSettingStr("CASINOAM_PORTALNAME", "")
        //        );
        //        SmartHash currentChekData = _callAMAPI(method, methodPars);

        //        if (currentChekData.IsOk)
        //        {
        //            int jkpFactor = CcConfig.AppSettingInt("CASINOAM_JKPHNDLER_FACTOR", 1); //in production is in cents!!!!!
        //            Cam_Jackpot jkpList = currentChekData.get("CALL_RESULT", (Cam_Jackpot)null, false);
        //            string key;

        //            if(jkpList != null)
        //            {
        //                if (jkpList.JackpotCards != null)
        //                {
        //                    Cam_JackpotCards jkpCards = jkpList.JackpotCards;
        //                    key = "JACKPOTCARDS";
        //                    if (!jackpotInfosByEiKey.ContainsKey(key))
        //                    {
        //                        SmartHash totalWinInfo = SmartHash.byPP(
        //                            "id", jkpCards.,
        //                            "name", jackpot.JackpotId + " - " + jackpot.Description,
        //                            "valueCent", jackpot.BaseAmount * jkpFactor //API returns values in decimals, we expose it as an integer representing cents
        //                        );

        //                        jackpotInfosByEiKey[key] = new SmartHash();
        //                    }
        //                }
        //                else if(jkpList.GoldenCoinsLink != null)
        //                {
        //                    key = "GOLDENCOINSLINK";
        //                    if (!jackpotInfosByEiKey.ContainsKey(key))
        //                    {
        //                        SmartHash totalWinInfo = SmartHash.byPP(
        //                            "id", jackpot.JackpotId,
        //                            "name", jackpot.JackpotId + " - " + jackpot.Description,
        //                            "valueCent", jackpot.BaseAmount * jkpFactor //API returns values in decimals, we expose it as an integer representing cents
        //                        );

        //                        jackpotInfosByEiKey[key] = new SmartHash();
        //                    }
        //                }
        //                else if(jkpList.CashBomb != null)
        //                {
        //                    key = "CASHBOMB";
        //                    if (!jackpotInfosByEiKey.ContainsKey(key))
        //                    {
        //                        SmartHash totalWinInfo = SmartHash.byPP(
        //                            "id", jackpot.JackpotId,
        //                            "name", jackpot.JackpotId + " - " + jackpot.Description,
        //                            "valueCent", jackpot.BaseAmount * jkpFactor //API returns values in decimals, we expose it as an integer representing cents
        //                        );
        //                        jackpotInfosByEiKey[key] = new SmartHash();
        //                    }
        //                }
        //            }
        //            foreach (Cam_Jackpot jackpot in jkpList)
        //            {
        //                foreach (Cpn_Game game in jackpot.Games)
        //                {
        //                    string gameId = game.GameId.ToString();
        //                    if (!jackpotInfosByEiKey.ContainsKey(gameId))
        //                        jackpotInfosByEiKey[gameId] = new SortedList<int, SmartHash[]>();

        //                    if (!jackpotInfosByEiKey[gameId].ContainsKey(jackpot.JackpotId))
        //                    {
        //                        SmartHash totalWinInfo = SmartHash.byPP(
        //                            "id", jackpot.JackpotId,
        //                            "name", jackpot.JackpotId + " - " + jackpot.Description,
        //                            "valueCent", jackpot.BaseAmount * jkpFactor //API returns values in decimals, we expose it as an integer representing cents
        //                        );
        //                        jackpotInfosByEiKey[gameId][jackpot.JackpotId] = new[] { totalWinInfo, totalWinInfo };
        //                    }
        //                }
        //            }
        //            jackpotValueCache[cacheKey] = jackpotInfosByEiKey;
        //        }
        //        else
        //        {
        //            jackpotInfosByEiKey = new SortedList<string, SmartHash>();
        //            Log.def.Warn("CASINOAM_getJackpotValues:ERR:" + currentChekData.ErrorMessage);
        //        }
        //    }

        //    string extKey = auxPars.getTypedValue("extKey", "", false);
        //    SmartHash jkpValue = new SmartHash();
        //    if (jackpotInfosByEiKey.ContainsKey(extKey))
        //    {
        //        SortedList<int, SmartHash[]> gameJkpVals = jackpotInfosByEiKey[extKey];
        //        HashParams srcAuxPars = auxPars.getTypedValue("srcAuxPars", new HashParams("XXX", "INVALID"));
        //        if (srcAuxPars.getTypedValue("XXX", "") == "INVALID")
        //            Log.def.Warn("PN GetJkpVal-Invalid path1");
        //        bool contrib = srcAuxPars.getTypedValue("contrib", false);
        //        List<SmartHash> gameJkpInfos = new List<SmartHash>();
        //        foreach (int jkpId in gameJkpVals.Keys)
        //        {
        //            SmartHash[] jkpInfos = gameJkpVals[jkpId];
        //            if (jkpInfos.Length == 2)
        //            {
        //                gameJkpInfos.Add(jkpInfos[contrib ? 1 : 0]);
        //            }
        //            else
        //            {
        //                Log.def.Warn("PN GetJkpVal-Invalid path2");
        //            }
        //        }

        //        jkpValue["jackpotValues"] = gameJkpInfos;
        //        //jkpValue["jackpotValues"] = gameJkpInfos.OrderByDescending(o => o["valueCent"]).ToList();
        //    }
        //    else Log.def.Warn("CASINOAM_getJackpotValues:ERR:Game not found {0} {1}", gameKey, extKey);
        //    return jkpValue;
        //}
        #endregion hide
        public virtual SmartHash getJackpotValues(string gameKey, HashParams auxPars)
        {
            const string CACHE_KEY = "JKPVAL";
            const string T_CARDS = "__JKP_TEMPLATE_CARDS__";
            const string T_CASH = "__JKP_TEMPLATE_CASH__";
            // GCL è per currency: "__JKP_TEMPLATE_GCL__:EUR"

            var cache = (SortedList<string, SortedList<int, SmartHash[]>>)jackpotValueCache[CACHE_KEY];

            // 1) Se la cache manca, popolala dal feed Amusnet
            if (cache == null)
            {
                cache = new SortedList<string, SortedList<int, SmartHash[]>>();

                string method = "get_jackpot_vals"; // dispatcher interno verso Amusnet
                SmartHash methodPars = SmartHash.byPP(
                    "portalUid", CcConfig.AppSettingStr("CASINOAM_PORTALNAME", "")
                );

                SmartHash api = _callAMAPI(method, methodPars);
                if (!api.IsOk)
                {
                    Log.def.Warn("CASINOAM_getJackpotValues:ERR:{0}", api.ErrorMessage);
                    jackpotValueCache[CACHE_KEY] = cache;
                    return new SmartHash(); // handler superiore gestirà lista vuota
                }

                Cam_Jackpot feed = api.get("CALL_RESULT", (Cam_Jackpot)null, false);
                if (feed == null)
                {
                    Log.def.Warn("CASINOAM_getJackpotValues: CALL_RESULT non è Cam_Jackpot");
                    jackpotValueCache[CACHE_KEY] = cache;
                    return new SmartHash();
                }

                int factor = CcConfig.AppSettingInt("CASINOAM_JKPHNDLER_FACTOR", 1); // es. 100 per centesimi

                // 1.a) Jackpot Cards (4 livelli)
                if (feed.JackpotCards != null)
                {
                    var jc = feed.JackpotCards;
                    var tpl = new SortedList<int, SmartHash[]>();
                    AddOrUpdate(tpl, 1101, "Jackpot Cards - Level I", ToCent(jc.CurrentLevelI, factor));
                    AddOrUpdate(tpl, 1102, "Jackpot Cards - Level II", ToCent(jc.CurrentLevelII, factor));
                    AddOrUpdate(tpl, 1103, "Jackpot Cards - Level III", ToCent(jc.CurrentLevelIII, factor));
                    AddOrUpdate(tpl, 1104, "Jackpot Cards - Level IV", ToCent(jc.CurrentLevelIV, factor));
                    cache[T_CARDS] = tpl;
                }

                // 1.b) Golden Coins Link (Mini, Minor, Major, Grand) — per currency
                if (feed.GoldenCoinsLink != null && feed.GoldenCoinsLink.CurrencyStatistics != null)
                {
                    string wantedCur = GetParam(auxPars, "currency",
                                        CcConfig.AppSettingStr("CASINOAM_FEED_CURRENCY", ""));
                    var cs = SelectGclCurrency(feed.GoldenCoinsLink.CurrencyStatistics, wantedCur);
                    if (cs != null && cs.Levels != null && cs.Levels.Count >= 4)
                    {
                        var tpl = new SortedList<int, SmartHash[]>();
                        // Ordine atteso: Mini, Minor, Major, Grand
                        AddOrUpdate(tpl, 1201, "Golden Coins - Mini", ToCent(cs.Levels[0].Balance, factor));
                        AddOrUpdate(tpl, 1202, "Golden Coins - Minor", ToCent(cs.Levels[1].Balance, factor));
                        AddOrUpdate(tpl, 1203, "Golden Coins - Major", ToCent(cs.Levels[2].Balance, factor));
                        AddOrUpdate(tpl, 1204, "Golden Coins - Grand", ToCent(cs.Levels[3].Balance, factor));
                        cache[$"__JKP_TEMPLATE_GCL__:{cs.Currency?.ToUpperInvariant()}"] = tpl;
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(wantedCur))
                            Log.def.Warn("GCL currency '{0}' non trovata o livelli insufficienti", wantedCur);
                    }
                }

                // 1.c) Cash Bomb (saldo complessivo)
                if (feed.CashBomb != null)
                {
                    var cb = feed.CashBomb;
                    var tpl = new SortedList<int, SmartHash[]>();
                    AddOrUpdate(tpl, 1300, "Cash Bomb", ToCent(cb.Balance, factor));
                    cache[T_CASH] = tpl;
                }

                // 1.d) (Opzionale) Template ALL = merge di quelli presenti
                var all = MergeTemplates(
                    cache.ContainsKey(T_CARDS) ? cache[T_CARDS] : null,
                    SelectAnyGclTemplate(cache),
                    cache.ContainsKey(T_CASH) ? cache[T_CASH] : null
                );
                if (all != null) cache["__JKP_TEMPLATE_ALL__"] = all;

                jackpotValueCache[CACHE_KEY] = cache;
            }

            // 2) Scelta del TEMPLATE in base al tipo richiesto (retrocompat: default CARDS)
            string jkpType = GetParam(auxPars, "jkpType", "").ToUpperInvariant(); // CARDS | GCL | CASH | ALL
            string currency = GetParam(auxPars, "currency",
                              CcConfig.AppSettingStr("CASINOAM_FEED_CURRENCY", "")).ToUpperInvariant();

            string templateKey = ResolveTemplateKey(cache, jkpType, currency);
            if (templateKey == null)
            {
                Log.def.Warn("JKP: tipo richiesto '{0}' non disponibile (currency '{1}')", jkpType, currency);
                // Risposta vuota ma compatibile
                return new SmartHash { ["jackpotValues"] = new List<SmartHash>() };
            }

            // 3) Materializza per extKey se non esiste già
            string extKey = auxPars.getTypedValue("extKey", "", false);
            if (!string.IsNullOrWhiteSpace(extKey) && !cache.ContainsKey(extKey))
            {
                cache[extKey] = ClonePairs(cache[templateKey]);
                jackpotValueCache[CACHE_KEY] = cache; // aggiorna cache globale
            }

            // 4) Costruzione risposta per il gioco richiesto (identico al tuo schema)
            SmartHash jkpValue = new SmartHash();
            if (!string.IsNullOrWhiteSpace(extKey) && cache.ContainsKey(extKey))
            {
                SortedList<int, SmartHash[]> gameJkpVals = cache[extKey];
                HashParams srcAuxPars = auxPars.getTypedValue("srcAuxPars", new HashParams("XXX", "INVALID"));
                if (srcAuxPars.getTypedValue("XXX", "") == "INVALID")
                    Log.def.Warn("AM GetJkpVal-Invalid path1");

                bool contrib = srcAuxPars.getTypedValue("contrib", false);
                List<SmartHash> gameJkpInfos = new List<SmartHash>();

                foreach (int jkpId in gameJkpVals.Keys)
                {
                    SmartHash[] jkpInfos = gameJkpVals[jkpId];
                    if (jkpInfos != null && jkpInfos.Length == 2)
                        gameJkpInfos.Add(jkpInfos[contrib ? 1 : 0]);
                    else
                        Log.def.Warn("AM GetJkpVal-Invalid path2");
                }

                jkpValue["jackpotValues"] = gameJkpInfos;
            }
            else
            {
                // Nessun extKey o non materializzato -> lista vuota per compatibilità
                jkpValue["jackpotValues"] = new List<SmartHash>();
                if (!string.IsNullOrWhiteSpace(extKey))
                    Log.def.Warn("CasinoAM_getJackpotValues: Game not found {0} {1}", gameKey, extKey);
            }

            return jkpValue;
        }

        #region Jackpot Utils
        private static string GetParam(HashParams auxPars, string name, string fallback = "")
        {
            // cerca prima in callPars, poi al root
            var callPars = auxPars.getTypedValue<HashParams>("callPars", null, false);
            if (callPars != null)
            {
                var v = callPars.getTypedValue(name, "", false);
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            var direct = auxPars.getTypedValue(name, "", false);
            return string.IsNullOrWhiteSpace(direct) ? fallback : direct;
        }

        private static Cam_GclCurrencyStats SelectGclCurrency(List<Cam_GclCurrencyStats> stats, string wanted)
        {
            if (!string.IsNullOrWhiteSpace(wanted))
                return stats.Find(s => string.Equals(s.Currency, wanted, StringComparison.OrdinalIgnoreCase));
            return stats.Count > 0 ? stats[0] : null; // fallback: prima disponibile
        }

        private static string ResolveTemplateKey(SortedList<string, SortedList<int, SmartHash[]>> cache, string jkpType, string currency)
        {
            switch (jkpType)
            {
                case "GCL":
                    // preferisci currency richiesta; se manca, prendi qualunque GCL
                    var key = $"__JKP_TEMPLATE_GCL__:{currency}";
                    if (!string.IsNullOrWhiteSpace(currency) && cache.ContainsKey(key)) return key;
                    return SelectAnyGclKey(cache);
                case "CASH":
                    return cache.ContainsKey("__JKP_TEMPLATE_CASH__") ? "__JKP_TEMPLATE_CASH__" : null;
                case "ALL":
                    return cache.ContainsKey("__JKP_TEMPLATE_ALL__") ? "__JKP_TEMPLATE_ALL__" : null;
                case "CARDS":
                default:
                    if (cache.ContainsKey("__JKP_TEMPLATE_CARDS__")) return "__JKP_TEMPLATE_CARDS__";
                    // default conservativo: se non ci sono Cards, prova GCL poi CASH
                    var g = SelectAnyGclKey(cache);
                    if (g != null) return g;
                    return cache.ContainsKey("__JKP_TEMPLATE_CASH__") ? "__JKP_TEMPLATE_CASH__" : null;
            }
        }

        private static string SelectAnyGclKey(SortedList<string, SortedList<int, SmartHash[]>> cache)
        {
            foreach (var k in cache.Keys)
                if (k.StartsWith("__JKP_TEMPLATE_GCL__", StringComparison.Ordinal))
                    return k;
            return null;
        }

        private static SortedList<int, SmartHash[]> SelectAnyGclTemplate(SortedList<string, SortedList<int, SmartHash[]>> cache)
        {
            var k = SelectAnyGclKey(cache);
            return k != null ? cache[k] : null;
        }

        private static SortedList<int, SmartHash[]> MergeTemplates(SortedList<int, SmartHash[]> cards,SortedList<int, SmartHash[]> gcl,SortedList<int, SmartHash[]> cash)
        {
            var merge = new SortedList<int, SmartHash[]>();
            if (cards != null) foreach (var kv in cards) merge[kv.Key] = ClonePair(kv.Value);
            if (gcl != null) foreach (var kv in gcl) merge[kv.Key] = ClonePair(kv.Value);
            if (cash != null) foreach (var kv in cash) merge[kv.Key] = ClonePair(kv.Value);
            return merge.Count > 0 ? merge : null;
        }

        private static void AddOrUpdate(SortedList<int, SmartHash[]> dst, int id, string name, long valueCent)
        {
            var totalWinInfo = SmartHash.byPP(
                "id", id,
                "name", $"{id} - {name}",
                "valueCent", valueCent
            );
            // formato legacy: [total, contrib] (uguali)
            dst[id] = new[] { totalWinInfo, totalWinInfo };
        }

        private static SortedList<int, SmartHash[]> ClonePairs(SortedList<int, SmartHash[]> src)
        {
            var dst = new SortedList<int, SmartHash[]>();
            foreach (var kv in src) dst[kv.Key] = ClonePair(kv.Value);
            return dst;
        }

        private static SmartHash[] ClonePair(SmartHash[] p)
        {
            var total = SmartHash.byPP(
                "id", p[0].getTypedValue("id", 0),
                "name", p[0].getTypedValue("name", "", false),
                "valueCent", p[0].getTypedValue("valueCent", 0L)
            );
            var contrib = SmartHash.byPP(
                "id", p[1].getTypedValue("id", 0),
                "name", p[1].getTypedValue("name", "", false),
                "valueCent", p[1].getTypedValue("valueCent", 0L)
            );
            return new[] { total, contrib };
        }

        private static long ToCent(long raw, int factor) => checked(raw * (long)factor);
        private static long ToCent(decimal raw, int factor)
            => checked((long)Math.Round(raw * factor, MidpointRounding.AwayFromZero));

        public override HashResult checkUserRegistered(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult registerUser(EndUser eu, Games game, HashParams extPars, string author)
        {
            throw new NotImplementedException();
        }

        public override HashResult getCurrentBalances(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult sitIn(EndUser eu, Games game, long amount, TransferType transferType, HashParams extPars, string author)
        {
            throw new NotImplementedException();
        }

        public override HashResult sitOut(EndUser eu, Games game, long amount, TransferType transferType, HashParams extPars, string author)
        {
            throw new NotImplementedException();
        }

        public override HashResult logIn(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult logOut(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getSessionBalances(EndUser eu, Games game, string specificSessionToken, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getStartUrl(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getHistoryUrl(EndUser eu, Games game, string wmBetId, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getPendingSessions(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult completePendingSessions(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getPlayDetail(EndUser eu, Games game, string seqPlay, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override EndUserExtInt getUserExtension(EndUser eu, Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        #endregion Jackpot Utils

        #endregion

        #region Inner Classes and Enums
        public class HashModuleStub
        {
            public string AAMS_MODULE;
            public int AAMS_CODICE;
            public string AAMS_ESTREMI;
            public string AAMS_HASH;
            public byte AAMS_VERSION;
            public byte AAMS_SUBVERSION;
        }
        public enum _MethSigns
        {
            CHKUSR, REGUSR, GETGAM, GETBAL, SITIN0, SITOUT,
            SESBAL, STAURL, HISURL, PENSES, COMSES, USREXT
        }

        public enum _MethNames
        {
            NoName, Cam_Authenticate, Cam_Withdraw, Cam_Deposit, Cam_WithdrawAndDeposit, Cam_AwardDeposit, Cam_RollBack, Cam_AuxInfos, CAM_CloseSession, CAM_OpenSession
        }

        public class SessionBalancesHolder
        {
            public long amountTotal;
            public long bonus;
            public int funBonus;
            public int lastProgr;
            public int lastRemFr;
            public long amountTotalWin;
            public long accountBalance;
        }

        #endregion
    }
}