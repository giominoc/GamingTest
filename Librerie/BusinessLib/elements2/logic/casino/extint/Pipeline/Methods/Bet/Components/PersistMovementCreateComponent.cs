using it.capecod.gridgame.business.elements2;
using it.capecod.log;
using System;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet.Components
{
    /// <summary>
    /// Componente standard: Persist Movement Create.
    /// Salva il movimento nel database (stato iniziale).
    /// </summary>
    public static class PersistMovementCreateComponent
    {
        public const string Key = "PersistMovementCreate";

        public static void Execute(BetContext ctx)
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
    }
}
