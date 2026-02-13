using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Bet;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Win;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Cancel;
using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.casino;
using it.capecod.gridgame.business.util.data;
using it.capecod.util;
using it.capecod.log;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.am
{
    /// <summary>
    /// Implementazione concreta delle tre pipeline (Bet, Win, Cancel) 
    /// per CasinoAM usando l'architettura pipeline.
    /// 
    /// Questa classe sostituisce i metodi legacy _CasinoAM_Withdraw, _CasinoAM_Deposit, _CasinoAM_RollBack
    /// con una implementazione basata su pipeline riutilizzabile e testabile.
    /// </summary>
    public class CasinoExtIntAMSWCorePipelined : CasinoExtIntFaceTest
    {
        private readonly CompiledSteps<BetCtx> _betPipeline;
        private readonly CompiledSteps<WinCtx> _winPipeline;
        private readonly CompiledSteps<CancelCtx> _cancelPipeline;

        public CasinoExtIntAMSWCorePipelined()
        {
            // Compila le pipeline una sola volta
            _betPipeline = new BetPipelineAM().BuildPipeline();
            _winPipeline = new WinPipelineAM().BuildPipeline();
            _cancelPipeline = new CancelPipelineAM().BuildPipeline();
        }

        /// <summary>
        /// Esegue un Bet (Withdraw/Debit) tramite pipeline.
        /// </summary>
        public Hashtable ExecuteBet(int euId, HashParams auxPars)
        {
            var pipeline = new BetPipelineAM();
            return pipeline.RunBetPipeline(euId, auxPars, _betPipeline);
        }

        /// <summary>
        /// Esegue un Win (Deposit/Credit) tramite pipeline.
        /// </summary>
        public Hashtable ExecuteWin(int euId, HashParams auxPars)
        {
            var pipeline = new WinPipelineAM();
            return pipeline.RunWinPipeline(euId, auxPars, _winPipeline);
        }

        /// <summary>
        /// Esegue un Cancel (RollBack) tramite pipeline.
        /// </summary>
        public Hashtable ExecuteCancel(int euId, HashParams auxPars)
        {
            var pipeline = new CancelPipelineAM();
            return pipeline.RunCancelPipeline(euId, auxPars, _cancelPipeline);
        }

        // =====================================================================
        // Implementazioni concrete delle tre pipeline
        // =====================================================================

        /// <summary>
        /// Pipeline Bet concreta per CasinoAM.
        /// </summary>
        private class BetPipelineAM : CasinoExtIntBetPipeline
        {
            public CompiledSteps<BetCtx> BuildPipeline()
            {
                return BuildBetPipeline();
            }

            protected override void Bet_RequestValidation(BetCtx ctx)
            {
                // TODO: Implementare validazione specifica per CasinoAM
                // Es: validare playerId, transactionId, amount, gameId, sessionId
                // Se validazione fallisce: ctx.Stop = true; ctx.TargetStatus = "409";
                
                var transactionId = ctx.AuxPars.getTypedValue("transactionId", string.Empty, false);
                var amount = ctx.AuxPars.getTypedValue("amount", 0L, false);

                if (string.IsNullOrWhiteSpace(transactionId) || amount <= 0)
                {
                    ctx.TargetStatus = "409";
                    ctx.Response["responseCodeReason"] = "409";
                    ctx.Response["errorMessage"] = "BAD_REQUEST";
                    ctx.Stop = true;
                    return;
                }

                // Imposta tipo movimento e stati
                ctx.CmbType = CasinoMovimentiBuffer.Type.Loose;
                ctx.TargetState = CasinoMovimentiBuffer.States.PreDumped;
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
            }

            protected override void Bet_LoadSession(BetCtx ctx)
            {
                // TODO: Implementare caricamento sessione
                // Es: _getCurrentSessionInfo, _getSessionInfosByTicket
                
                try
                {
                    var ticket = ctx.AuxPars.getTypedValue("ticket", string.Empty, false);
                    if (string.IsNullOrWhiteSpace(ticket))
                    {
                        ctx.TargetStatus = "401";
                        ctx.Response["responseCodeReason"] = "401";
                        ctx.Response["errorMessage"] = "INVALID_SESSION";
                        ctx.Stop = true;
                        return;
                    }

                    // Placeholder: in produzione chiamare metodi reali
                    ctx.SessionInfos = new HashParams(
                        "MATCH_Id", -1,
                        "centEU_Id", ctx.CentEu.EU_ID
                    );
                }
                catch (Exception ex)
                {
                    Log.exc(ex);
                    ctx.TargetStatus = "500";
                    ctx.Response["errorMessage"] = "SESSION_ERROR";
                    ctx.Stop = true;
                }
            }

            protected override void Bet_ExecuteExternalTransfer(BetCtx ctx)
            {
                // TODO: Implementare chiamata wallet seamless
                // Es: chiamata HTTP al provider esterno, update balance
                
                try
                {
                    // Placeholder: in produzione chiamare API wallet esterna
                    // Per ora simuliamo successo
                    ctx.TargetStatus = "200";
                    ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
                    
                    // Aggiorna balance (placeholder)
                    ctx.Response["balance"] = 1000L;
                }
                catch (Exception ex)
                {
                    Log.exc(ex);
                    ctx.TargetStatus = "500";
                    ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Deleted;
                    ctx.Response["errorMessage"] = "TRANSFER_FAILED";
                }
            }
        }

        /// <summary>
        /// Pipeline Win concreta per CasinoAM.
        /// </summary>
        private class WinPipelineAM : CasinoExtIntWinPipeline
        {
            public CompiledSteps<WinCtx> BuildPipeline()
            {
                return BuildWinPipeline();
            }

            protected override void Win_RequestValidation(WinCtx ctx)
            {
                var transactionId = ctx.AuxPars.getTypedValue("transactionId", string.Empty, false);
                var amount = ctx.AuxPars.getTypedValue("amount", 0L, false);

                if (string.IsNullOrWhiteSpace(transactionId) || amount < 0)
                {
                    ctx.TargetStatus = "409";
                    ctx.Response["responseCodeReason"] = "409";
                    ctx.Response["errorMessage"] = "BAD_REQUEST";
                    ctx.Stop = true;
                    return;
                }

                ctx.CmbType = CasinoMovimentiBuffer.Type.Win;
                ctx.TargetState = CasinoMovimentiBuffer.States.PreDumped;
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
            }

            protected override void Win_LoadSession(WinCtx ctx)
            {
                try
                {
                    var ticket = ctx.AuxPars.getTypedValue("ticket", string.Empty, false);
                    if (string.IsNullOrWhiteSpace(ticket))
                    {
                        ctx.TargetStatus = "401";
                        ctx.Response["responseCodeReason"] = "401";
                        ctx.Response["errorMessage"] = "INVALID_SESSION";
                        ctx.Stop = true;
                        return;
                    }

                    ctx.SessionInfos = new HashParams(
                        "MATCH_Id", -1,
                        "centEU_Id", ctx.CentEu.EU_ID
                    );
                }
                catch (Exception ex)
                {
                    Log.exc(ex);
                    ctx.TargetStatus = "500";
                    ctx.Response["errorMessage"] = "SESSION_ERROR";
                    ctx.Stop = true;
                }
            }

            protected override void Win_ExecuteExternalTransfer(WinCtx ctx)
            {
                try
                {
                    ctx.TargetStatus = "200";
                    ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
                    ctx.Response["balance"] = 1200L;
                }
                catch (Exception ex)
                {
                    Log.exc(ex);
                    ctx.TargetStatus = "500";
                    ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Deleted;
                    ctx.Response["errorMessage"] = "TRANSFER_FAILED";
                }
            }
        }

        /// <summary>
        /// Pipeline Cancel concreta per CasinoAM.
        /// </summary>
        private class CancelPipelineAM : CasinoExtIntCancelPipeline
        {
            public CompiledSteps<CancelCtx> BuildPipeline()
            {
                return BuildCancelPipeline();
            }

            protected override void Cancel_RequestValidation(CancelCtx ctx)
            {
                var transactionId = ctx.AuxPars.getTypedValue("transactionId", string.Empty, false);
                var roundRef = ctx.AuxPars.getTypedValue("roundRef", string.Empty, false);

                if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(roundRef))
                {
                    ctx.TargetStatus = "409";
                    ctx.Response["responseCodeReason"] = "409";
                    ctx.Response["errorMessage"] = "BAD_REQUEST";
                    ctx.Stop = true;
                    return;
                }

                ctx.CmbType = CasinoMovimentiBuffer.Type.LooseCancel;
                ctx.TargetState = CasinoMovimentiBuffer.States.PreDumped;
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
            }

            protected override void Cancel_LoadSession(CancelCtx ctx)
            {
                try
                {
                    var ticket = ctx.AuxPars.getTypedValue("ticket", string.Empty, false);
                    if (string.IsNullOrWhiteSpace(ticket))
                    {
                        ctx.TargetStatus = "401";
                        ctx.Response["responseCodeReason"] = "401";
                        ctx.Response["errorMessage"] = "INVALID_SESSION";
                        ctx.Stop = true;
                        return;
                    }

                    ctx.SessionInfos = new HashParams(
                        "MATCH_Id", -1,
                        "centEU_Id", ctx.CentEu.EU_ID
                    );
                }
                catch (Exception ex)
                {
                    Log.exc(ex);
                    ctx.TargetStatus = "500";
                    ctx.Response["errorMessage"] = "SESSION_ERROR";
                    ctx.Stop = true;
                }
            }

            protected override void Cancel_ExecuteExternalTransfer(CancelCtx ctx)
            {
                try
                {
                    ctx.TargetStatus = "200";
                    ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
                    ctx.Response["balance"] = 1100L;
                }
                catch (Exception ex)
                {
                    Log.exc(ex);
                    ctx.TargetStatus = "500";
                    ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Deleted;
                    ctx.Response["errorMessage"] = "TRANSFER_FAILED";
                }
            }
        }

        #region CasinoExtIntFaceTest Implementation (Stubs)
        
        // Questi metodi sono richiesti dalla classe base ma non sono rilevanti per le pipeline
        // Li implementiamo come stub/throw per ora
        
        public override HashResult checkUserRegistered(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars)
        {
            throw new NotImplementedException("Use pipeline-based methods: ExecuteBet, ExecuteWin, ExecuteCancel");
        }

        public override HashResult registerUser(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars, string author)
        {
            throw new NotImplementedException();
        }

        public override HashResult getAvailableGames(HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getCurrentBalances(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult sitIn(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, long amount, TransferType transferType, HashParams extPars, string author)
        {
            throw new NotImplementedException();
        }

        public override HashResult sitOut(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, long amount, TransferType transferType, HashParams extPars, string author)
        {
            throw new NotImplementedException();
        }

        public override HashResult logIn(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult logOut(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getSessionBalances(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, string specificSessionToken, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getStartUrl(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getHistoryUrl(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, string wmBetId, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getPendingSessions(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult completePendingSessions(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override bool delayedRetrySitoutOnInconsistency(HashParams extPars)
        {
            return false;
        }

        public override HashResult getPlayDetail(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, string seqPlay, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override it.capecod.gridgame.business.elements2.logic.account.ext.EndUserExtInt getUserExtension(it.capecod.gridgame.business.elements2.logic.account.ext.EndUser eu, it.capecod.gridgame.business.elements2.logic.general.Games game, HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override it.capecod.gridgame.business.elements2.logic.casino.extint.ExternalIntegration getExtIntegration(HashParams extPars)
        {
            throw new NotImplementedException();
        }

        public override HashResult getAuxInfos(string auxInfo, HashParams auxPar)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
