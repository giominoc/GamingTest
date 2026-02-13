using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.casino;
using it.capecod.log;
using System;
using System.Linq;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet.Components
{
    /// <summary>
    /// Componente standard: Idempotency Lookup.
    /// Cerca movimenti esistenti con lo stesso transactionId per evitare duplicati.
    /// Se trovato, salta al componente Resend.
    /// </summary>
    public static class IdempotencyLookupComponent
    {
        public const string Key = "IdempotencyLookup";

        public static void Execute(BetContext ctx)
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
                ctx.JumpToKey = ResendComponent.Key;
            }
        }
    }
}
