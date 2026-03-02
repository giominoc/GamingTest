using System.Collections.Generic;

namespace GamingTests.Net48.Casino.ExtInt
{
    public interface IWalletSharedHelpersContract
    {
        bool IsDuplicate(string provider, string transferId);
        bool HasValidSession(string provider, string playerId, string sessionId);
        long GetBalance(string provider, string playerId);
        void SetBalance(string provider, string playerId, long newBalance);
        WalletMovement PersistMovement(WalletMovement movement);
        void MarkCancelled(string provider, string gameRound, string transferId);
        SmartHash _toHashComplete(params object[] kvp);
        HashResult getGameInfos(string key, HashParams auxPars);
        List<SmartHash> getAllAvailableGamesAll(HashParams auxPars);
        string _getApiCfgKey(string methodName, HashParams auxPars);
        object _getApiSvc(string methodName, HashParams auxPars);
        SmartHash _getMethodInfos(string methodName, string route, HashParams auxPars);
        SessionBalancesHolder _loadRealSessionBalancesBySessInfos(HashParams sessionInfos, HashParams auxPars = null);
        SessionBalancesHolder _getFastSessionBalancesByTicket(string ticket, HashParams sessionInfos, HashParams auxPars = null);
        void _setFastSessionBalancesByTicket(string ticket, SessionBalancesHolder holder, HashParams sessionInfos);
        HashParams _getSessionInfosByTicket(string ticket, object localEu, string txId, object methName, HashParams auxPars = null);
        HashParams _getCurrentSessionInfo(int euId, string gameId, HashParams auxPars = null);
        HashParams _acquireMonitor(string ticket);
        void _releaseMonitor(HashParams monitor);
        WalletMovement _getSingleOperation(HashParams sessionInfos, string transactionId);
        void _setSingleOperationInCache(HashParams sessionInfos, string transactionId, WalletMovement movement);
        List<WalletMovement> _getOpenDebits(string action, HashParams sessionInfos, SmartHash aux);
        void _setOpenDebits(string action, HashParams sessionInfos, List<WalletMovement> openDebits);
        void _clearOpenDebitsCache(string action, HashParams sessionInfos);
        WalletMovement _getStakeByRef(HashParams sessionInfos, string roundRef);
        void _setStakeByRefInCache(HashParams sessionInfos, string roundRef, WalletMovement movement);
        long _getSaldo(HashParams sessionInfos, HashParams auxPars = null);
        void _pingTableSession_Central(HashParams sessionInfos, string author);
        HashResult _closeRound_Core(WalletMovement relatedOp, HashParams auxPars = null);
        HashResult _closeRound(WalletMovement relatedOp, HashParams auxPars = null);
        void _manageOldSessionEnds(HashParams sessionInfos, HashParams auxPars = null);
        WalletResult _ErrorRes(string code, string errorMessage, long balance);
        void _LogDebug(int level, string text, string userUid, string transactionId, string methodName, bool alert = false);
        void _DumpWsCall(string methodName, string rawRequest, string rawResponse, HashParams auxPars = null);
        string getSignature(string clearText, string secretKey);
        string _CalculateHash(string clearText, string secretKey);
        HashResult manageFreeRounds(HashParams sessionInfos, HashParams auxPars = null);
        HashResult resetFreeRounds(HashParams sessionInfos, HashParams auxPars = null);
        HashResult receiveCheckSums(HashParams auxPars = null);
        HashResult getJackpotValues(string key, HashParams auxPars);
    }
}