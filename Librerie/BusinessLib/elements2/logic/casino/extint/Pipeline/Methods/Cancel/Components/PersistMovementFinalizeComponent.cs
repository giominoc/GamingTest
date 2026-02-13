using it.capecod.gridgame.business.elements2;
using it.capecod.log;
using Newtonsoft.Json;
using System;
using System.Collections;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Cancel.Components
{
    /// <summary>
    /// Componente standard: Persist Movement Finalize.
    /// Aggiorna il movimento nel database con lo stato finale e la response.
    /// </summary>
    public static class PersistMovementFinalizeComponent
    {
        public const string Key = "PersistMovementFinalize";

        public static void Execute(CancelContext ctx)
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
    }
}
