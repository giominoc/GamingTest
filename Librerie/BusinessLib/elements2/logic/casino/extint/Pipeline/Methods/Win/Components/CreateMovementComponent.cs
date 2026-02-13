using it.capecod.gridgame.business.elements2.logic.casino;
using System;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Win.Components
{
    /// <summary>
    /// Componente standard: Create Movement.
    /// Crea un nuovo oggetto CasinoMovimentiBuffer con i dati del contesto.
    /// </summary>
    public static class CreateMovementComponent
    {
        public const string Key = "CreateMovement";

        public static void Execute(WinContext ctx)
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
    }
}
