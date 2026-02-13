using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.account.ext;
using it.capecod.gridgame.business.elements2.logic.casino;
using it.capecod.gridgame.business.util.data;
using it.capecod.log;
using it.capecod.util;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Cancel
{
    /// <summary>
    /// Definizioni di Hook + hook base/abstract per la pipeline di Cancel (RollBack).
    /// </summary>
    public abstract class CancelHooks : CasinoExtIntPipelineBase
    {
        // =====================================================================
        //  Cancel pipeline hooks
        // =====================================================================

        /// <summary>
        /// Hook standard della pipeline Cancel.
        /// </summary>
        public enum CancelHook
        {
            // Macro-flow
            ResponseDefinition,
            ContextBaseGeneration,
            RequestValidation,
            IdempotencyLookup,
            LoadSession,
            FindRelatedBet,
            CreateMovement,
            PersistMovementCreate,
            ExecuteExternalTransfer,
            PersistMovementFinalize,
            BuildResponse,

            // Branch target
            Resend
        }

        /// <summary>
        /// Customization point for provider-specific extensions.
        /// </summary>
        protected virtual void CustomizeCancelSteps(List<Step<CancelCtx>> steps) { }

        // =====================================================================
        //  Cancel hook implementations
        // =====================================================================

        /// <summary>
        /// (1) RESPONSE DEFINITION
        /// </summary>
        protected virtual void Cancel_ResponseDefinition(CancelCtx ctx)
        {
            ctx.TargetStatus = "500";
            ctx.TargetState = CasinoMovimentiBuffer.States.Deleted;
            ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Deleted;

            ctx.Response["responseCodeReason"] = "500";
            ctx.Response["errorMessage"] = "UNHANDLED";
            ctx.Response["balance"] = 0L;
            ctx.Response["casinoTransferId"] = string.Empty;
        }

        /// <summary>
        /// (2) CONTEXT BASE GENERATION
        /// </summary>
        protected virtual void Cancel_ContextBaseGeneration(CancelCtx ctx)
        {
            ctx.Author = ctx.AuxPars.getTypedValue("author", "system", false);
            ctx.RawRequest = ctx.AuxPars.getTypedValue("rawRequest", string.Empty, false);
            ctx.TransactionId = ctx.AuxPars.getTypedValue("transactionId", string.Empty, false);
            ctx.Ticket = ctx.AuxPars.getTypedValue("ticket", string.Empty, false);
            ctx.RoundRef = ctx.AuxPars.getTypedValue("roundRef", string.Empty, false);
            ctx.Service = FinMovExtern.Servizi.CasinoAM;
        }

        /// <summary>
        /// (3) REQUEST VALIDATION
        /// Abstract: provider-specific validation logic
        /// </summary>
        protected abstract void Cancel_RequestValidation(CancelCtx ctx);

        /// <summary>
        /// (4) IDEMPOTENCY CHECK
        /// </summary>
        protected virtual void Cancel_IdempotencyLookup(CancelCtx ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.TransactionId))
                return;

            CasinoMovimentiBuffer retryOp = null;
            try
            {
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    retryOp = CasinoSessionMgr.def.BUFFER_GetAll(
                        new HashParams(
                            "CMB_SERVICE", (int)ctx.Service,
                            "CMB_FK_EU_Id", ctx.CentEu.EU_ID,
                            "CMB_STATEs", new[]
                            {
                                (int)CasinoMovimentiBuffer.States.Dumped,
                                (int)CasinoMovimentiBuffer.States.PreCommitted,
                                (int)CasinoMovimentiBuffer.States.Committed,
                                (int)CasinoMovimentiBuffer.States.Deleted,
                                (int)CasinoMovimentiBuffer.States.Completed
                            },
                            "CMB_EXTTXID", ctx.TransactionId
                        )
                    ).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Log.exc(ex);
            }

            if (retryOp != null)
            {
                ctx.IdempotencyMov = retryOp;
                ctx.JumpToKey = CancelHook.Resend.ToString();
            }
        }

        /// <summary>
        /// (5) LOAD SESSION
        /// Abstract: provider-specific session loading logic
        /// </summary>
        protected abstract void Cancel_LoadSession(CancelCtx ctx);

        /// <summary>
        /// (6) FIND RELATED BET
        /// Find the bet movement to cancel based on round reference or transaction ID
        /// </summary>
        protected virtual void Cancel_FindRelatedBet(CancelCtx ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.RoundRef))
                return;

            try
            {
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                {
                    ctx.RelatedBetMov = CasinoSessionMgr.def.BUFFER_GetAll(
                        new HashParams(
                            "CMB_SERVICE", (int)ctx.Service,
                            "CMB_FK_EU_Id", ctx.CentEu.EU_ID,
                            "CMB_STATEs", new[]
                            {
                                (int)CasinoMovimentiBuffer.States.Dumped,
                                (int)CasinoMovimentiBuffer.States.PreCommitted,
                                (int)CasinoMovimentiBuffer.States.Committed,
                                (int)CasinoMovimentiBuffer.States.Deleted,
                                (int)CasinoMovimentiBuffer.States.Completed
                            },
                            "CMB_EXTROUNDREF", ctx.RoundRef
                        )
                    ).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Log.exc(ex);
            }
        }

        /// <summary>
        /// (7) CREATE MOVEMENT
        /// </summary>
        protected virtual void Cancel_CreateMovement(CancelCtx ctx)
        {
            var utcNow = DateTime.UtcNow;

            ctx.NewMov = new CasinoMovimentiBuffer
            {
                CMB_SERVICE = (int)ctx.Service,
                CMB_FK_EU_Id = ctx.CentEu.EU_ID,
                CMB_FK_FST_MATCH_Id = ctx.CurrMatch?.MATCH_Id ?? -1,
                CMB_FK_CURR_MATCH_Id = ctx.CurrMatch?.MATCH_Id ?? -1,
                CMB_TYPE = (int)ctx.CmbType,
                CMB_STATE = (int)ctx.TargetState,
                CMB_CLOSED = (int)CasinoMovimentiBuffer.Closed.Opened,
                CMB_OPDATE_UTC = utcNow,
                CMB_CREATEDAT = utcNow.ToLocalTime(),
                CMB_EXTTXID = ctx.TransactionId,
                CMB_EXTROUNDREF = ctx.RoundRef,
                CMB_RECVTEXT = ctx.RawRequest,
                CMB_RESULT = ctx.TargetStatus,
            };

            // Link to related bet if found
            if (ctx.RelatedBetMov != null)
            {
                ctx.NewMov.CMB_RELATED_FK_CMB_ID = ctx.RelatedBetMov.CMB_ID;
            }
        }

        /// <summary>
        /// (8) PERSIST MOVEMENT (CREATE)
        /// </summary>
        protected virtual void Cancel_PersistMovementCreate(CancelCtx ctx)
        {
            if (ctx.NewMov == null)
                return;

            try
            {
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    ctx.NewMov.Create(ctx.Author);
            }
            catch (Exception ex)
            {
                Log.exc(ex);
                ctx.TargetStatus = "500";
                ctx.Response["responseCodeReason"] = "500";
                ctx.Response["errorMessage"] = "DB_ERROR";
                ctx.Stop = true;
            }
        }

        /// <summary>
        /// (9) EXECUTE EXTERNAL TRANSFER
        /// Abstract: provider-specific wallet communication
        /// </summary>
        protected abstract void Cancel_ExecuteExternalTransfer(CancelCtx ctx);

        /// <summary>
        /// (10) PERSIST MOVEMENT (FINALIZE)
        /// </summary>
        protected virtual void Cancel_PersistMovementFinalize(CancelCtx ctx)
        {
            if (ctx.NewMov == null)
                return;

            try
            {
                ctx.NewMov.CMB_STATE = (int)ctx.TargetStateFinal;
                ctx.NewMov.CMB_RESULT = ctx.TargetStatus;
                ctx.NewMov.CMB_SENTTEXT = JsonConvert.SerializeObject(new Hashtable(ctx.Response));
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    ctx.NewMov.Update(ctx.Author);
            }
            catch (Exception ex)
            {
                Log.exc(ex);
            }
        }

        /// <summary>
        /// (11) BUILD RESPONSE
        /// </summary>
        protected virtual void Cancel_BuildResponse(CancelCtx ctx)
        {
            ctx.Response["responseCodeReason"] = ctx.TargetStatus;

            if (ctx.TargetStatus == "200")
            {
                ctx.Response.Remove("errorMessage");
            }
            else
            {
                if (!ctx.Response.ContainsKey("errorMessage"))
                    ctx.Response["errorMessage"] = "ERROR";
            }

            if (ctx.NewMov != null)
                ctx.Response["casinoTransferId"] = ctx.NewMov.CMB_ID.ToString();
        }

        /// <summary>
        /// (12) RESEND
        /// </summary>
        protected virtual void Cancel_Resend(CancelCtx ctx)
        {
            var op = ctx.IdempotencyMov;
            if (op == null)
                return;

            try
            {
                if (!string.IsNullOrWhiteSpace(op.CMB_SENTTEXT))
                {
                    var ht = JsonConvert.DeserializeObject<Hashtable>(op.CMB_SENTTEXT);
                    if (ht != null)
                    {
                        ctx.Response.Clear();
                        foreach (DictionaryEntry e in ht)
                            ctx.Response[e.Key] = e.Value;
                    }
                }

                var status = op.CMB_RESULT;
                if (string.IsNullOrWhiteSpace(status))
                    status = "200";

                ctx.TargetStatus = status;
                ctx.Response["responseCodeReason"] = status;
                ctx.Response["casinoTransferId"] = op.CMB_ID.ToString();
            }
            catch (Exception ex)
            {
                Log.exc(ex);
                ctx.TargetStatus = "200";
                ctx.Response["responseCodeReason"] = "200";
                ctx.Response["casinoTransferId"] = op.CMB_ID.ToString();
            }
            finally
            {
                ctx.Stop = true;
            }
        }
    }
}
