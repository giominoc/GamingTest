using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GamingTests.Net48.Casino.ExtInt
{
    public abstract class WalletSharedHelpersBase : IWalletSharedHelpersContract
    {
        private readonly ConcurrentDictionary<string, WalletMovement> _movements = new ConcurrentDictionary<string, WalletMovement>();
        private readonly ConcurrentDictionary<string, long> _balances = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, string> _sessions = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, SessionBalancesHolder> _fastBalanceByTicket = new ConcurrentDictionary<string, SessionBalancesHolder>();
        private readonly ConcurrentDictionary<string, object> _monitorByTicket = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, WalletMovement> _singleOperationByTx = new ConcurrentDictionary<string, WalletMovement>();
        private readonly ConcurrentDictionary<string, List<WalletMovement>> _openDebitsByKey = new ConcurrentDictionary<string, List<WalletMovement>>();
        private readonly ConcurrentDictionary<string, WalletMovement> _stakeByRoundRef = new ConcurrentDictionary<string, WalletMovement>();

        protected virtual string BuildMovementKey(string provider, string transferId)
        {
            return provider + "::" + transferId;
        }

        protected virtual string BuildPlayerKey(string provider, string playerId)
        {
            return provider + "::" + playerId;
        }

        protected virtual string BuildOpenDebitKey(string action, HashParams sessionInfos)
        {
            return action + "::" + sessionInfos.getTypedValue("ticket", "noticket", false);
        }

        public virtual bool IsDuplicate(string provider, string transferId)
        {
            return _movements.ContainsKey(BuildMovementKey(provider, transferId));
        }

        public virtual bool HasValidSession(string provider, string playerId, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(sessionId)) return false;

            string key = BuildPlayerKey(provider, playerId);
            string existing;
            if (_sessions.TryGetValue(key, out existing)) return existing == sessionId;

            _sessions[key] = sessionId;
            return true;
        }

        public virtual long GetBalance(string provider, string playerId)
        {
            long value;
            return _balances.TryGetValue(BuildPlayerKey(provider, playerId), out value) ? value : 0L;
        }

        public virtual void SetBalance(string provider, string playerId, long newBalance)
        {
            _balances[BuildPlayerKey(provider, playerId)] = newBalance;
        }

        public virtual WalletMovement PersistMovement(WalletMovement movement)
        {
            _movements[BuildMovementKey(movement.Provider, movement.TransferId)] = movement;
            return movement;
        }

        public virtual void MarkCancelled(string provider, string gameRound, string transferId)
        {
            WalletMovement movement;
            if (_movements.TryGetValue(BuildMovementKey(provider, transferId), out movement))
            {
                movement.Status = "Cancelled";
            }
        }

        public virtual SmartHash _toHashComplete(params object[] kvp)
        {
            return SmartHash.byPP(kvp);
        }

        public virtual HashResult getGameInfos(string key, HashParams auxPars)
        {
            return new HashResult { IsOk = !string.IsNullOrWhiteSpace(key), ErrorMessage = string.Empty };
        }

        public virtual List<SmartHash> getAllAvailableGamesAll(HashParams auxPars)
        {
            return new List<SmartHash>();
        }

        public virtual string _getApiCfgKey(string methodName, HashParams auxPars)
        {
            return methodName + "::cfg";
        }

        public virtual object _getApiSvc(string methodName, HashParams auxPars)
        {
            return new { Method = methodName, Route = auxPars.getTypedValue("route", "default", false) };
        }

        public virtual SmartHash _getMethodInfos(string methodName, string route, HashParams auxPars)
        {
            return SmartHash.byPP("method", methodName, "route", route, "cfgKey", _getApiCfgKey(methodName, auxPars));
        }

        public virtual SessionBalancesHolder _loadRealSessionBalancesBySessInfos(HashParams sessionInfos, HashParams auxPars = null)
        {
            return new SessionBalancesHolder { amountTotal = _getSaldo(sessionInfos, auxPars) };
        }

        public virtual SessionBalancesHolder _getFastSessionBalancesByTicket(string ticket, HashParams sessionInfos, HashParams auxPars = null)
        {
            SessionBalancesHolder holder;
            if (_fastBalanceByTicket.TryGetValue(ticket ?? string.Empty, out holder)) return holder;
            holder = _loadRealSessionBalancesBySessInfos(sessionInfos ?? new HashParams(), auxPars);
            _fastBalanceByTicket[ticket ?? string.Empty] = holder;
            return holder;
        }

        public virtual void _setFastSessionBalancesByTicket(string ticket, SessionBalancesHolder holder, HashParams sessionInfos)
        {
            _fastBalanceByTicket[ticket ?? string.Empty] = holder ?? new SessionBalancesHolder();
        }

        public virtual HashParams _getSessionInfosByTicket(string ticket, object localEu, string txId, object methName, HashParams auxPars = null)
        {
            return new HashParams("ticket", ticket ?? string.Empty, "txId", txId ?? string.Empty, "method", methName == null ? string.Empty : methName.ToString());
        }

        public virtual HashParams _getCurrentSessionInfo(int euId, string gameId, HashParams auxPars = null)
        {
            return new HashParams("EU_ID", euId, "GAME_ID", gameId ?? string.Empty, "ticket", auxPars == null ? string.Empty : auxPars.getTypedValue("ticket", string.Empty, false));
        }

        public virtual HashParams _acquireMonitor(string ticket)
        {
            object monitor = _monitorByTicket.GetOrAdd(ticket ?? string.Empty, new object());
            return new HashParams("ticket", ticket ?? string.Empty, "monitor", monitor);
        }

        public virtual void _releaseMonitor(HashParams monitor)
        {
        }

        public virtual WalletMovement _getSingleOperation(HashParams sessionInfos, string transactionId)
        {
            WalletMovement movement;
            _singleOperationByTx.TryGetValue(transactionId ?? string.Empty, out movement);
            return movement;
        }

        public virtual void _setSingleOperationInCache(HashParams sessionInfos, string transactionId, WalletMovement movement)
        {
            _singleOperationByTx[transactionId ?? string.Empty] = movement;
        }

        public virtual List<WalletMovement> _getOpenDebits(string action, HashParams sessionInfos, SmartHash aux)
        {
            List<WalletMovement> value;
            string key = BuildOpenDebitKey(action, sessionInfos ?? new HashParams());
            if (_openDebitsByKey.TryGetValue(key, out value)) return value;
            return new List<WalletMovement>();
        }

        public virtual void _setOpenDebits(string action, HashParams sessionInfos, List<WalletMovement> openDebits)
        {
            _openDebitsByKey[BuildOpenDebitKey(action, sessionInfos ?? new HashParams())] = openDebits ?? new List<WalletMovement>();
        }

        public virtual void _clearOpenDebitsCache(string action, HashParams sessionInfos)
        {
            List<WalletMovement> dummy;
            _openDebitsByKey.TryRemove(BuildOpenDebitKey(action, sessionInfos ?? new HashParams()), out dummy);
        }

        public virtual WalletMovement _getStakeByRef(HashParams sessionInfos, string roundRef)
        {
            WalletMovement movement;
            _stakeByRoundRef.TryGetValue(roundRef ?? string.Empty, out movement);
            return movement;
        }

        public virtual void _setStakeByRefInCache(HashParams sessionInfos, string roundRef, WalletMovement movement)
        {
            _stakeByRoundRef[roundRef ?? string.Empty] = movement;
        }

        public virtual long _getSaldo(HashParams sessionInfos, HashParams auxPars = null)
        {
            string provider = (sessionInfos == null) ? string.Empty : sessionInfos.getTypedValue("provider", string.Empty, false);
            string playerId = (sessionInfos == null) ? string.Empty : sessionInfos.getTypedValue("playerId", string.Empty, false);
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(playerId)) return 0L;
            return GetBalance(provider, playerId);
        }

        public virtual void _pingTableSession_Central(HashParams sessionInfos, string author)
        {
        }

        public virtual HashResult _closeRound_Core(WalletMovement relatedOp, HashParams auxPars = null)
        {
            return new HashResult { IsOk = true, ErrorMessage = string.Empty };
        }

        public virtual HashResult _closeRound(WalletMovement relatedOp, HashParams auxPars = null)
        {
            return _closeRound_Core(relatedOp, auxPars);
        }

        public virtual void _manageOldSessionEnds(HashParams sessionInfos, HashParams auxPars = null)
        {
        }

        public virtual WalletResult _ErrorRes(string code, string errorMessage, long balance)
        {
            return WalletResult.Fail(code, errorMessage, balance);
        }

        public virtual void _LogDebug(int level, string text, string userUid, string transactionId, string methodName, bool alert = false)
        {
        }

        public virtual void _DumpWsCall(string methodName, string rawRequest, string rawResponse, HashParams auxPars = null)
        {
        }

        public virtual string getSignature(string clearText, string secretKey)
        {
            return _CalculateHash(clearText, secretKey);
        }

        public virtual string _CalculateHash(string clearText, string secretKey)
        {
            var bytes = Encoding.UTF8.GetBytes((clearText ?? string.Empty) + "|" + (secretKey ?? string.Empty));
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
        }

        public virtual HashResult manageFreeRounds(HashParams sessionInfos, HashParams auxPars = null)
        {
            return new HashResult { IsOk = true };
        }

        public virtual HashResult resetFreeRounds(HashParams sessionInfos, HashParams auxPars = null)
        {
            return new HashResult { IsOk = true };
        }

        public virtual HashResult receiveCheckSums(HashParams auxPars = null)
        {
            return new HashResult { IsOk = true };
        }

        public virtual HashResult getJackpotValues(string key, HashParams auxPars)
        {
            return new HashResult
            {
                IsOk = true,
                ErrorMessage = string.Empty,
                ["jackpotValues"] = new List<SmartHash>()
            };
        }
    }
}
