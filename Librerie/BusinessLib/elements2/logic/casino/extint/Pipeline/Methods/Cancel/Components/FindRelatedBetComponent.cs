using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.casino;
using it.capecod.log;
using System;
using System.Linq;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Cancel.Components
{
    /// <summary>
    /// Componente standard: Find Related Bet.
    /// Trova il movimento di bet da annullare basandosi sul round reference o transaction ID.
    /// </summary>
    public static class FindRelatedBetComponent
    {
        public const string Key = "FindRelatedBet";

        public static void Execute(CancelContext ctx)
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
    }
}
