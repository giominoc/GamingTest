using System;
using System.Collections;
using System.Reflection;
using it.capecod.gridgame.business.elements2.logic.account.ext;
using it.capecod.gridgame.business.elements2.logic.general;

namespace GamingTests.Net48.Casino.ExtInt
{
    public abstract class CasinoExtIntBase : CasinoExtIntFace
    {
        #region base composition
        protected readonly IWalletPipelineContract Pipelines;
        protected readonly IWalletSharedHelpersContract Helpers;

        protected CasinoExtIntBase()
            : this(new WalletSharedHelpers(), new WalletPipelineBase())
        {
        }

        protected CasinoExtIntBase(IWalletSharedHelpersContract helpers)
            : this(helpers, new WalletPipelineBase())
        {
        }

        protected CasinoExtIntBase(IWalletPipelineContract pipelines)
            : this(new WalletSharedHelpers(), pipelines)
        {
        }

        protected CasinoExtIntBase(IWalletSharedHelpersContract helpers, IWalletPipelineContract pipelines)
        {
            Helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
            Pipelines = pipelines ?? throw new ArgumentNullException(nameof(pipelines));
        }
        #endregion

        #region pipeline execution bridge
        protected virtual WalletResult ExecuteBet(WalletRequest request)
        {
            return Pipelines.ExecuteBet(request);
        }

        protected virtual WalletResult ExecuteWin(WalletRequest request)
        {
            return Pipelines.ExecuteWin(request);
        }

        protected virtual WalletResult ExecuteCancel(WalletRequest request)
        {
            return Pipelines.ExecuteCancel(request);
        }
        #endregion

        #region CasinoExtIntFace default contract implementations

        protected virtual HashResult NotImplementedFace(string method)
        {
            return new HashResult
            {
                IsOk = false,
                ErrorMessage = method + " not implemented in wallet pipeline base"
            };
        }

        public override virtual HashResult checkUserRegistered(EndUser eu, Games game, HashParams extPars)
        {
            var result = new HashResult();
            try
            {
                EndUserExtInt extEu = getUserExtension(eu, game, extPars);
                bool found = extEu != null;
                if (extEu != null)
                {
                    object flag = GetMemberValue(extEu, "flagRecordFound");
                    if (flag is bool)
                    {
                        found = (bool)flag;
                    }
                }

                result.IsOk = found;
                if (extEu != null) result["extEu"] = extEu;
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult registerUser(EndUser eu, Games game, HashParams extPars, string author)
        {
            var result = new HashResult();
            try
            {
                EndUserExtInt already = getUserExtension(eu, game, extPars);
                bool alreadyFound = already != null;
                if (already != null)
                {
                    object flag = GetMemberValue(already, "flagRecordFound");
                    if (flag is bool) alreadyFound = (bool)flag;
                }

                if (alreadyFound)
                {
                    result.IsOk = true;
                    result["extEu"] = already;
                    return result;
                }

                var extEu = new EndUserExtInt();
                string provider = ResolveProvider(eu, game, extPars);
                ExternalIntegration extInt = getExtIntegration(extPars);

                SetMemberValue(extEu, "EUEI_CATEGORY", provider);
                SetMemberValue(extEu, "EUEI_EXTCODE", "-1");
                SetMemberValue(extEu, "EUEI_FK_EI_ID", GetIntMemberValue(extInt, "EI_ID", -1));
                SetMemberValue(extEu, "EUEI_FK_EU_ID", GetIntMemberValue(eu, "EU_ID", -1));
                SetMemberValue(extEu, "EUEI_KEY", ResolvePlayerId(eu, extPars));
                SetMemberValue(extEu, "EUEI_REGDATE", DateTime.Now);

                Type statesType = typeof(EndUserExtInt).GetNestedType("States", BindingFlags.Public | BindingFlags.NonPublic);
                if (statesType != null && Enum.IsDefined(statesType, "Temporary"))
                {
                    object tempValue = Enum.Parse(statesType, "Temporary");
                    SetMemberValue(extEu, "EUEI_STATE", Convert.ToInt32(tempValue));
                }

                MethodInfo createWithAuthor = typeof(EndUserExtInt).GetMethod("Create", new[] { typeof(string) });
                if (createWithAuthor != null)
                {
                    createWithAuthor.Invoke(extEu, new object[] { author ?? string.Empty });
                }

                result.IsOk = true;
                result["extEu"] = extEu;
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult getAvailableGames(HashParams extPars)
        {
            var result = new HashResult { IsOk = true };
            try
            {
                result["GAMEMAP"] = Helpers.getAllAvailableGamesAll(extPars ?? new HashParams());

                string gameKey = extPars == null ? string.Empty : extPars.getTypedValue("gameKey", string.Empty, false);
                if (string.IsNullOrWhiteSpace(gameKey) && extPars != null)
                {
                    gameKey = extPars.getTypedValue("GAME_KEY", string.Empty, false);
                }
                if (!string.IsNullOrWhiteSpace(gameKey))
                {
                    result["GAMEINFO"] = Helpers.getGameInfos(gameKey, extPars ?? new HashParams());
                }
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult getCurrentBalances(EndUser eu, Games game, HashParams extPars)
        {
            var result = new HashResult();
            try
            {
                HashParams sessionInfos = ResolveSessionInfos(eu, game, extPars);
                string ticket = extPars == null ? string.Empty : extPars.getTypedValue("ticket", string.Empty, false);

                SessionBalancesHolder counters = string.IsNullOrWhiteSpace(ticket)
                    ? Helpers._loadRealSessionBalancesBySessInfos(sessionInfos, extPars)
                    : Helpers._getFastSessionBalancesByTicket(ticket, sessionInfos, extPars);

                long total = counters == null ? 0L : counters.amountTotal;
                long bonus = counters == null ? 0L : counters.bonus;
                long funBonus = counters == null ? 0L : counters.funBonus;

                result.IsOk = true;
                result["CASH"] = total - bonus;
                result["BONUS"] = bonus;
                result["FUNBONUS"] = funBonus;
                result["FUNBONUS_FORCE"] = funBonus;
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult sitIn(EndUser eu, Games game, long amount, TransferType transferType, HashParams extPars, string author)
        {
            return ExecuteWalletTransfer(eu, game, Math.Abs(amount), extPars, isCredit: true);
        }

        public override virtual HashResult sitOut(EndUser eu, Games game, long amount, TransferType transferType, HashParams extPars, string author)
        {
            return ExecuteWalletTransfer(eu, game, Math.Abs(amount), extPars, isCredit: false);
        }

        public override virtual HashResult logIn(EndUser eu, Games game, HashParams extPars)
        {
            var result = new HashResult();
            try
            {
                string provider = ResolveProvider(eu, game, extPars);
                string playerId = ResolvePlayerId(eu, extPars);
                string sessionId = extPars == null ? string.Empty : extPars.getTypedValue("sessionId", string.Empty, false);
                if (string.IsNullOrWhiteSpace(sessionId)) sessionId = Guid.NewGuid().ToString("N");

                result.IsOk = Helpers.HasValidSession(provider, playerId, sessionId);
                result["SESSIONID"] = sessionId;
                result["PLAYERID"] = playerId;
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult logOut(EndUser eu, Games game, HashParams extPars)
        {
            var result = new HashResult();
            try
            {
                string provider = ResolveProvider(eu, game, extPars);
                string playerId = ResolvePlayerId(eu, extPars);
                result.IsOk = true;
                result["BALANCE"] = Helpers.GetBalance(provider, playerId);
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult getSessionBalances(EndUser eu, Games game, string specificSessionToken, HashParams extPars)
        {
            var result = new HashResult();
            try
            {
                HashParams sessionInfos = ResolveSessionInfos(eu, game, extPars);
                string ticket = string.IsNullOrWhiteSpace(specificSessionToken)
                    ? (extPars == null ? string.Empty : extPars.getTypedValue("ticket", string.Empty, false))
                    : specificSessionToken;

                SessionBalancesHolder counters = Helpers._getFastSessionBalancesByTicket(ticket, sessionInfos, extPars);
                long total = counters == null ? 0L : counters.amountTotal;
                long bonus = counters == null ? 0L : counters.bonus;
                long cash = total - bonus;

                result.IsOk = true;
                result["SessionToken"] = ticket;
                result["CashBetAmount"] = 0L;
                result["CashWinAmount"] = cash;
                result["BonusBetAmount"] = 0L;
                result["BonusWinAmount"] = bonus;
                result["BETS"] = new ArrayList();
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult getStartUrl(EndUser eu, Games game, HashParams extPars)
        {
            var result = new HashResult();
            try
            {
                string startUrl = extPars == null ? string.Empty : extPars.getTypedValue("startUrl", string.Empty, false);
                if (string.IsNullOrWhiteSpace(startUrl) && extPars != null)
                {
                    startUrl = extPars.getTypedValue("STARTURL", string.Empty, false);
                }

                if (string.IsNullOrWhiteSpace(startUrl))
                {
                    string baseUrl = extPars == null ? string.Empty : extPars.getTypedValue("launchBaseUrl", string.Empty, false);
                    string gameId = ResolveGameKey(game, extPars);
                    string playerId = ResolvePlayerId(eu, extPars);
                    string sessionId = extPars == null ? string.Empty : extPars.getTypedValue("sessionId", string.Empty, false);
                    if (string.IsNullOrWhiteSpace(sessionId)) sessionId = Guid.NewGuid().ToString("N");
                    startUrl = string.Format("{0}?game={1}&player={2}&session={3}", baseUrl, gameId, playerId, sessionId);
                }

                result.IsOk = !string.IsNullOrWhiteSpace(startUrl);
                result["STARTURL"] = startUrl;
                result["URL"] = startUrl;
                if (!result.IsOk) result.ErrorMessage = "START_URL_NOT_AVAILABLE";
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult getHistoryUrl(EndUser eu, Games game, string wmBetId, HashParams extPars)
        {
            var result = new HashResult();
            try
            {
                string historyUrl = extPars == null ? string.Empty : extPars.getTypedValue("historyUrl", string.Empty, false);
                if (string.IsNullOrWhiteSpace(historyUrl) && extPars != null)
                {
                    historyUrl = extPars.getTypedValue("HISTORYURL", string.Empty, false);
                }

                if (string.IsNullOrWhiteSpace(historyUrl))
                {
                    string baseUrl = extPars == null ? string.Empty : extPars.getTypedValue("historyBaseUrl", string.Empty, false);
                    historyUrl = string.Format("{0}?betId={1}", baseUrl, wmBetId ?? string.Empty);
                }

                result.IsOk = !string.IsNullOrWhiteSpace(historyUrl);
                result["HISTORYURL"] = historyUrl;
                result["URL"] = historyUrl;
                if (!result.IsOk) result.ErrorMessage = "HISTORY_URL_NOT_AVAILABLE";
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }
            return result;
        }

        public override virtual HashResult getPendingSessions(EndUser eu, Games game, HashParams extPars)
        {
            var result = new HashResult
            {
                IsOk = true
            };
            result["PENDING"] = new ArrayList();
            return result;
        }

        public override virtual HashResult completePendingSessions(EndUser eu, Games game, HashParams extPars)
        {
            var result = new HashResult
            {
                IsOk = true
            };
            result["COMPLETED"] = new ArrayList();
            return result;
        }

        public override virtual bool delayedRetrySitoutOnInconsistency(HashParams extPars)
        {
            return extPars != null && extPars.getTypedValue("delayedRetrySitout", false, false);
        }

        public override virtual HashResult getPlayDetail(EndUser eu, Games game, string seqPlay, HashParams extPars)
        {
            var result = new HashResult
            {
                IsOk = !string.IsNullOrWhiteSpace(seqPlay)
            };
            if (result.IsOk)
            {
                result["SEQPLAY"] = seqPlay;
                result["DETAIL"] = extPars == null ? new HashParams() : extPars;
            }
            else
            {
                result.ErrorMessage = "PLAY_DETAIL_NOT_AVAILABLE";
            }
            return result;
        }

        public override virtual EndUserExtInt getUserExtension(EndUser eu, Games game, HashParams extPars)
        {
            if (extPars == null) return null;

            EndUserExtInt extEu = extPars.getTypedValue<EndUserExtInt>("extEu", null, false);
            if (extEu != null) return extEu;

            extEu = extPars.getTypedValue<EndUserExtInt>("EUEXT", null, false);
            return extEu;
        }

        public override virtual ExternalIntegration getExtIntegration(HashParams extPars)
        {
            if (extPars == null) return null;

            ExternalIntegration extInt = extPars.getTypedValue<ExternalIntegration>("extIntegration", null, false);
            if (extInt != null) return extInt;

            extInt = extPars.getTypedValue<ExternalIntegration>("extInt", null, false);
            return extInt;
        }

        public override virtual HashResult getAuxInfos(string auxInfo, HashParams auxPar)
        {
            string action = (auxInfo ?? string.Empty).Trim().ToLowerInvariant();
            switch (action)
            {
                case "withdraw":
                case "deposit":
                case "rollback":
                    return ExecuteAuxWalletAction(action, auxPar);

                case "getfastbalance":
                case "swext_getfastbalance":
                {
                    var result = new HashResult();
                    try
                    {
                        HashParams sessionInfos = auxPar == null
                            ? new HashParams()
                            : auxPar.getTypedValue("sessionInfos", new HashParams(), false);
                        string ticket = auxPar == null ? string.Empty : auxPar.getTypedValue("ticket", string.Empty, false);
                        SessionBalancesHolder counters = Helpers._getFastSessionBalancesByTicket(ticket, sessionInfos, auxPar);
                        long total = counters == null ? 0L : counters.amountTotal;
                        long bonus = counters == null ? 0L : counters.bonus;
                        long funBonus = counters == null ? 0L : counters.funBonus;
                        result.IsOk = true;
                        result["CASH"] = total - bonus;
                        result["BONUS"] = bonus;
                        result["FUNBONUS"] = funBonus;
                        result["FUNBONUS_FORCE"] = funBonus;
                    }
                    catch (Exception exc)
                    {
                        result.IsOk = false;
                        result.ErrorMessage = exc.Message;
                    }
                    return result;
                }

                case "setfastbalance":
                case "swext_setfastbalance":
                {
                    var result = new HashResult();
                    try
                    {
                        HashParams sessionInfos = auxPar == null
                            ? new HashParams()
                            : auxPar.getTypedValue("sessionInfos", new HashParams(), false);
                        string ticket = auxPar == null ? string.Empty : auxPar.getTypedValue("ticket", string.Empty, false);
                        long cash = auxPar == null ? 0L : auxPar.getTypedValue("CASH", 0L, false);
                        long bonus = auxPar == null ? 0L : auxPar.getTypedValue("BONUS", 0L, false);
                        long funBonus = auxPar == null ? 0L : auxPar.getTypedValue("FUNBONUS", 0L, false);
                        var holder = new SessionBalancesHolder
                        {
                            amountTotal = cash + bonus,
                            bonus = bonus,
                            funBonus = funBonus
                        };
                        Helpers._setFastSessionBalancesByTicket(ticket, holder, sessionInfos);
                        result.IsOk = true;
                    }
                    catch (Exception exc)
                    {
                        result.IsOk = false;
                        result.ErrorMessage = exc.Message;
                    }
                    return result;
                }

                case "swext_getsessioninfos":
                case "getsessioninfos":
                {
                    var result = new HashResult();
                    try
                    {
                        string ticket = auxPar == null ? string.Empty : auxPar.getTypedValue("ticket", string.Empty, false);
                        object localEu = auxPar == null ? null : auxPar.getTypedValue<object>("localEu", null, false);
                        string txId = auxPar == null ? string.Empty : auxPar.getTypedValue("txId", string.Empty, false);
                        object methodName = auxPar == null ? null : auxPar.getTypedValue<object>("methodName", null, false);
                        result["sessionInfos"] = Helpers._getSessionInfosByTicket(ticket, localEu, txId, methodName, auxPar);
                        result.IsOk = true;
                    }
                    catch (Exception exc)
                    {
                        result.IsOk = false;
                        result.ErrorMessage = exc.Message;
                    }
                    return result;
                }

                case "swext_clearmatchcache":
                {
                    var result = new HashResult();
                    try
                    {
                        HashParams sessionInfos = auxPar == null
                            ? new HashParams()
                            : auxPar.getTypedValue("sessionInfos", new HashParams(), false);
                        string clearAction = auxPar == null ? "default" : auxPar.getTypedValue("action", "default", false);
                        Helpers._clearOpenDebitsCache(clearAction, sessionInfos);
                        result.IsOk = true;
                    }
                    catch (Exception exc)
                    {
                        result.IsOk = false;
                        result.ErrorMessage = exc.Message;
                    }
                    return result;
                }

                case "hashcodes":
                case "receivechecksums":
                    return Helpers.receiveCheckSums(auxPar);

                case "getjackpotvalues":
                {
                    string key = auxPar == null ? string.Empty : auxPar.getTypedValue("key", string.Empty, false);
                    return Helpers.getJackpotValues(key, auxPar);
                }

                default:
                    return NotImplementedFace(nameof(getAuxInfos) + ":" + auxInfo);
            }
        }

        private HashResult ExecuteWalletTransfer(EndUser eu, Games game, long amount, HashParams extPars, bool isCredit)
        {
            var request = new WalletRequest
            {
                Provider = ResolveProvider(eu, game, extPars),
                PlayerId = ResolvePlayerId(eu, extPars),
                TransferId = ResolveTransferId(extPars),
                SessionId = extPars == null ? string.Empty : extPars.getTypedValue("sessionId", string.Empty, false),
                GameId = ResolveGameKey(game, extPars),
                GameRound = extPars == null ? string.Empty : extPars.getTypedValue("gameRound", string.Empty, false),
                Amount = Math.Abs(amount),
                Currency = extPars == null ? "EUR" : extPars.getTypedValue("currency", "EUR", false),
                ForceRoundClose = extPars != null && extPars.getTypedValue("forceRoundClose", false, false)
            };

            WalletResult walletResult = isCredit ? ExecuteWin(request) : ExecuteBet(request);
            return BuildHashResultFromWalletResult(walletResult);
        }

        private HashResult ExecuteAuxWalletAction(string action, HashParams auxPar)
        {
            var result = new HashResult();
            try
            {
                HashParams callPars = auxPar == null
                    ? new HashParams()
                    : auxPar.getTypedValue("callPars", new HashParams(), false);

                var request = new WalletRequest
                {
                    Provider = callPars.getTypedValue("provider", string.Empty, false),
                    PlayerId = callPars.getTypedValue("playerId", string.Empty, false),
                    TransferId = callPars.getTypedValue("transferId", string.Empty, false),
                    SessionId = callPars.getTypedValue("sessionId", string.Empty, false),
                    GameId = callPars.getTypedValue("gameId", string.Empty, false),
                    GameRound = callPars.getTypedValue("gameNumber", string.Empty, false),
                    Amount = callPars.getTypedValue("amount", 0L, false),
                    Currency = callPars.getTypedValue("currency", "EUR", false),
                    ForceRoundClose = callPars.getTypedValue("forceRoundClose", false, false)
                };

                if (string.IsNullOrWhiteSpace(request.Provider))
                {
                    request.Provider = callPars.getTypedValue("providerId", string.Empty, false);
                }

                WalletResult walletResult;
                switch (action)
                {
                    case "withdraw":
                        walletResult = ExecuteBet(request);
                        break;
                    case "deposit":
                        walletResult = ExecuteWin(request);
                        break;
                    default:
                        walletResult = ExecuteCancel(request);
                        break;
                }

                result.IsOk = true;
                result["MSGRESULT"] = walletResult;
            }
            catch (Exception exc)
            {
                result.IsOk = false;
                result.ErrorMessage = exc.Message;
            }

            return result;
        }

        private static HashResult BuildHashResultFromWalletResult(WalletResult walletResult)
        {
            var result = new HashResult
            {
                IsOk = walletResult != null && walletResult.IsOk,
                ErrorMessage = walletResult == null ? "EMPTY_WALLET_RESULT" : walletResult.Error
            };

            if (walletResult != null)
            {
                result["CODE"] = walletResult.Code;
                result["BALANCE"] = walletResult.Balance;
                result["CASINOTRANSFERID"] = walletResult.CasinoTransferId;
                result["MSGRESULT"] = walletResult;
            }

            return result;
        }

        private HashParams ResolveSessionInfos(EndUser eu, Games game, HashParams extPars)
        {
            if (extPars != null)
            {
                HashParams existing = extPars.getTypedValue("sessionInfos", new HashParams(), false);
                if (existing != null)
                {
                    return existing;
                }
            }

            int euId = GetIntMemberValue(eu, "EU_ID", -1);
            string gameId = ResolveGameKey(game, extPars);
            HashParams current = Helpers._getCurrentSessionInfo(euId, gameId, extPars);

            string provider = ResolveProvider(eu, game, extPars);
            string playerId = ResolvePlayerId(eu, extPars);
            string ticket = extPars == null ? string.Empty : extPars.getTypedValue("ticket", string.Empty, false);

            return new HashParams(
                "provider", provider,
                "playerId", playerId,
                "ticket", ticket,
                "sessionInfos", current
            );
        }

        private static string ResolveProvider(EndUser eu, Games game, HashParams extPars)
        {
            if (extPars != null)
            {
                string value = extPars.getTypedValue("provider", string.Empty, false);
                if (!string.IsNullOrWhiteSpace(value)) return value;

                value = extPars.getTypedValue("Provider", string.Empty, false);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            string gameEiKey = GetStringMemberValue(game, "GAME_EI_KEY", string.Empty);
            if (!string.IsNullOrWhiteSpace(gameEiKey)) return gameEiKey;

            return "GEN";
        }

        private static string ResolvePlayerId(EndUser eu, HashParams extPars)
        {
            if (extPars != null)
            {
                string value = extPars.getTypedValue("playerId", string.Empty, false);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            string uid = GetStringMemberValue(eu, "EU_UID", string.Empty);
            if (!string.IsNullOrWhiteSpace(uid)) return uid;

            int id = GetIntMemberValue(eu, "EU_ID", -1);
            return id < 0 ? string.Empty : id.ToString();
        }

        private static string ResolveGameKey(Games game, HashParams extPars)
        {
            if (extPars != null)
            {
                string value = extPars.getTypedValue("gameId", string.Empty, false);
                if (!string.IsNullOrWhiteSpace(value)) return value;

                value = extPars.getTypedValue("GAME_KEY", string.Empty, false);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            string gameKey = GetStringMemberValue(game, "GAME_KEY", string.Empty);
            if (!string.IsNullOrWhiteSpace(gameKey)) return gameKey;

            return GetStringMemberValue(game, "GAME_EI_KEY", string.Empty);
        }

        private static string ResolveTransferId(HashParams extPars)
        {
            if (extPars != null)
            {
                string transferId = extPars.getTypedValue("transferId", string.Empty, false);
                if (!string.IsNullOrWhiteSpace(transferId)) return transferId;

                transferId = extPars.getTypedValue("txId", string.Empty, false);
                if (!string.IsNullOrWhiteSpace(transferId)) return transferId;
            }

            return Guid.NewGuid().ToString("N");
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName)) return null;

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return property.GetValue(instance, null);

            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            return field == null ? null : field.GetValue(instance);
        }

        private static void SetMemberValue(object instance, string memberName, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName)) return;

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value, null);
                return;
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
            }
        }

        private static int GetIntMemberValue(object instance, string memberName, int fallback)
        {
            object value = GetMemberValue(instance, memberName);
            if (value == null) return fallback;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static string GetStringMemberValue(object instance, string memberName, string fallback)
        {
            object value = GetMemberValue(instance, memberName);
            return value == null ? fallback : Convert.ToString(value);
        }

        #endregion
    }
}
