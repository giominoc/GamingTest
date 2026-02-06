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

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Bet
{
    /// <summary>
    /// Definizioni di Hook + hook base/abstract per la pipeline di Bet.
    /// Qui NON ci sono le macro di Build/Run (sono in CasinoExtIntBetPipeline).
    /// </summary>
    public abstract class BetHooks : CasinoExtIntPipelineBase
    {
        // =====================================================================
        //  Bet pipeline
        // =====================================================================

        /// <summary>
        /// Hook standard della pipeline Bet.
        ///
        /// Nota su Jump:
        /// - JumpToKey deve puntare a una Key esistente (di default: hook.ToString()).
        /// - per saltare a Resend: ctx.JumpToKey = BetHook.Resend.ToString();
        /// </summary>
        public enum BetHook
        {
            // Macro-flow
            ResponseDefinition,
            ContextBaseGeneration,
            RequestValidation,
            IdempotencyLookup,
            LoadSession,
            BalanceCheck,
            CreateMovement,
            PersistMovementCreate,
            ExecuteExternalTransfer,
            PersistMovementFinalize,
            BuildResponse,

            // Branch target
            Resend
        }

        /// <summary>
        /// Unico punto “polimorfico” per inserire codice tra hook e/o sostituire hook.
        /// Niente override del macro-flow.
        ///
        /// Esempio pratico: in una integrazione provider X vuoi aggiungere un precheck tra
        /// RequestValidation e IdempotencyLookup.
        /// </summary>
        protected virtual void CustomizeBetSteps(List<Step<BetCtx>> steps) { }

        /// <summary>
        /// Crea la pipeline standard e applica eventuali personalizzazioni.
        ///
        /// Tipicamente chiamato UNA volta (in ctor della tua classe concreta) e salvato in un campo:
        /// <code>
        /// _depositSteps = new CompiledSteps&lt;BetCtx&gt;(BuildBetPipeline().ToArray());
        /// </code>
        /// </summary>

        // =====================================================================
        //  Bet hook implementations
        // =====================================================================

        /// <summary>
        /// (1) RESPONSE DEFINITION
        ///
        /// Inizializza una response "OK" e un default status, che verranno poi aggiornati.
        ///
        /// Esempio:
        /// - impostare responseCodeReason=200
        /// - impostare balance=0 come default
        /// </summary>
        protected virtual void Bet_ResponseDefinition(BetCtx ctx)
        {
            // Default: errore finché non arrivi a fine flow
            ctx.TargetStatus = "500";
            ctx.TargetState = CasinoMovimentiBuffer.States.Deleted;
            ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Deleted;

            // Schema response volutamente "generico" (SmartHash), così resta comune.
            ctx.Response["responseCodeReason"] = "500";
            ctx.Response["errorMessage"] = "UNHANDLED";
            ctx.Response["balance"] = 0L;
            ctx.Response["casinoTransferId"] = string.Empty;
        }

        /// <summary>
        /// (2) CONTEXT BASE GENERATION
        ///
        /// Popola i campi base dal chiamante (auxPars) e dall'ambiente:
        /// - author
        /// - rawRequest
        /// - transactionId/ticket (se presenti)
        /// - service / type default
        ///
        /// Esempio:
        /// ctx.Author = auxPars.getTypedValue("author", "system", false);
        /// </summary>
        protected virtual void Bet_ContextBaseGeneration(BetCtx ctx)
        {
            ctx.Author = ctx.AuxPars.getTypedValue("author", "system", false);
            ctx.RawRequest = ctx.AuxPars.getTypedValue("rawRequest", string.Empty, false);
            ctx.TransactionId = ctx.AuxPars.getTypedValue("transactionId", string.Empty, false);
            ctx.Ticket = ctx.AuxPars.getTypedValue("ticket", string.Empty, false);

            // Tipicamente definito dal metodo chiamante o da derived class.
            // Qui lasciamo un default stabile.
            ctx.Service = FinMovExtern.Servizi.CasinoAM;
        }

        /// <summary>
        /// (3) PRECHECK REQUEST VALIDATION
        ///
        /// Hook ABSTRACT: ogni integrazione decide:
        /// - come validare la request (parsing, mandatory params)
        /// - come impostare CmbType e stati target
        /// - eventuali stop immediati (set ctx.Stop=true)
        ///
        /// Esempio tipico:
        /// - se transactionId mancante: ctx.TargetStatus="409"; ctx.Response["errorMessage"]="INVALID_REQUEST"; ctx.Stop=true;
        /// </summary>
        protected abstract void Bet_RequestValidation(BetCtx ctx);

        /// <summary>
        /// (4) IDEMPOTENCY CHECK
        ///
        /// Default implementation: cerca su CMB (buffer) un movimento con stesso EXTTXID.
        /// Se trovato, imposta ctx.IdempotencyMov e fa Jump a Resend.
        ///
        /// Nota: la query è "best effort"; se vuoi un criterio diverso, fai Replace() su questo hook.
        /// </summary>
        protected virtual void Bet_IdempotencyLookup(BetCtx ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.TransactionId))
                return;

            CasinoMovimentiBuffer retryOp = null;
            try
            {
                // ATTENZIONE: nel codice reale spesso l'EU central viene risolto via EU_UID.
                // Qui abbiamo già CentEu nel ctx, quindi usiamo quello.
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

                // Branch: vai direttamente a Resend.
                ctx.JumpToKey = BetHook.Resend.ToString();
            }
        }

        /// <summary>
        /// (5) PRECHECK-BINDING EU - CURRENT SESSION - CURRENT MATCH
        ///
        /// Hook ABSTRACT: ogni integrazione decide come reperire la sessione corrente.
        ///
        /// Esempio:
        /// - ctx.CurrSession = CasinoSessionMgr.def.getCurrentSessionInfo(...)
        /// - ctx.SessionInfos = _getSessionInfosByTicket(...)
        /// - ctx.CurrMatch = sessionInfos.getTypedValue&lt;CasinoSubscription&gt;(...)
        ///
        /// Nota: se non trovi session e non hai idempotency, tipicamente qui setti:
        /// - ctx.TargetStatus="401"
        /// - ctx.Response["errorMessage"]="INVALID_SESSION"
        /// - ctx.Stop=true (e magari registri un CMB Deleted out-of-session)
        /// </summary>
        protected abstract void Bet_LoadSession(BetCtx ctx);

        /// <summary>
        /// (6) BALANCE CHECK
        ///
        /// Default neutro.
        ///
        /// In un'implementazione reale di solito:
        /// - ctx.Counters = _getFastSessionBalancesByTicket(...)
        /// - validazioni saldo/pending
        /// - eventuali policy (freeround, multibet, ecc.)
        /// </summary>
        protected virtual void Bet_BalanceCheck(BetCtx ctx)
        {
            // no-op by default
        }

        /// <summary>
        /// (7) NEW MOV CREATION ON DB (prepare)
        ///
        /// Prepara ctx.NewMov con i campi minimi.
        ///
        /// Questo hook dovrebbe creare l'oggetto ma NON persisterlo.
        /// La persistenza va nello step PersistMovementCreate.
        /// </summary>
        protected virtual void Bet_CreateMovement(BetCtx ctx)
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
                CMB_RECVTEXT = ctx.RawRequest,
                CMB_RESULT = ctx.TargetStatus,
            };
        }

        /// <summary>
        /// (8) NEW MOV PERSIST (create)
        ///
        /// Persiste ctx.NewMov nel DB.
        ///
        /// Esempio:
        /// <code>
        /// using(new GGConnMgr.CentralCtxWrap("CASINO")) ctx.NewMov.Create(ctx.Author);
        /// </code>
        /// </summary>
        protected virtual void Bet_PersistMovementCreate(BetCtx ctx)
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
                // Fallimento persistenza -> stop
                ctx.TargetStatus = "500";
                ctx.Response["responseCodeReason"] = "500";
                ctx.Response["errorMessage"] = "DB_ERROR";
                ctx.Stop = true;
            }
        }

        /// <summary>
        /// (9) WALLET COMMUNICATION (seamless)
        ///
        /// Hook ABSTRACT: qui si effettua la chiamata al provider/wallet.
        ///
        /// Deve aggiornare almeno:
        /// - ctx.ExternalTransferResult
        /// - ctx.TargetStatus / ctx.TargetStateFinal
        /// - eventuale response fields (es. balance)
        ///
        /// Esempio tipico:
        /// - if ok: TargetStatus="200"; TargetStateFinal=Committed; Response["balance"]=newBal;
        /// - if ko: TargetStatus="409"; TargetStateFinal=Deleted; Response["errorMessage"]=...
        /// </summary>
        protected abstract void Bet_ExecuteExternalTransfer(BetCtx ctx);

        /// <summary>
        /// (10) FINAL DB UPDATE
        ///
        /// Aggiorna lo stato finale del CMB e salva la response serializzata.
        ///
        /// Default implementation: best effort.
        /// </summary>
        protected virtual void Bet_PersistMovementFinalize(BetCtx ctx)
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
                // non fermiamo la response: la user-side deve comunque ricevere qualcosa
            }
        }

        /// <summary>
        /// (11) BUILD RESPONSE
        ///
        /// Garantisce che Response sia coerente con TargetStatus e aggiorna i campi standard.
        ///
        /// Default: usa TargetStatus + errorMessage se già impostati.
        /// </summary>
        protected virtual void Bet_BuildResponse(BetCtx ctx)
        {
            ctx.Response["responseCodeReason"] = ctx.TargetStatus;

            // Default: se ok, elimina errorMessage
            if (ctx.TargetStatus == "200")
            {
                ctx.Response.Remove("errorMessage");
            }
            else
            {
                if (!ctx.Response.ContainsKey("errorMessage"))
                    ctx.Response["errorMessage"] = "ERROR";
            }

            // Se ho un CMB, posso esporre un transferId stable
            if (ctx.NewMov != null)
                ctx.Response["casinoTransferId"] = ctx.NewMov.CMB_ID.ToString();
        }

        /// <summary>
        /// (14) RESEND
        ///
        /// Quando IdempotencyLookup trova un movimento esistente, la pipeline salta qui.
        ///
        /// Default implementation:
        /// - prova a deserializzare CMB_SENTTEXT e usarlo come response
        /// - se non disponibile, risponde con 200 e casinoTransferId.
        /// </summary>
        protected virtual void Bet_Resend(BetCtx ctx)
        {
            var op = ctx.IdempotencyMov;
            if (op == null)
                return;

            try
            {
                // CMB_SENTTEXT salva spesso una response serializzata (JSON di Hashtable o oggetto specifico).
                // Qui supportiamo il caso più comune: JSON di Hashtable.
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

                // Status
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
                // fallback
                ctx.TargetStatus = "200";
                ctx.Response["responseCodeReason"] = "200";
                ctx.Response["casinoTransferId"] = op.CMB_ID.ToString();
            }
            finally
            {
                // In resend chiudiamo sempre.
                ctx.Stop = true;
            }
        }
    }
}
