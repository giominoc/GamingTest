using it.capecod.gridgame.business.elements2.logic.casino;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Win.Components
{
    /// <summary>
    /// Componente standard: Response Definition.
    /// Inizializza la response con valori di default (errore).
    /// </summary>
    public static class ResponseDefinitionComponent
    {
        public const string Key = "ResponseDefinition";

        public static void Execute(WinContext ctx)
        {
            ctx.TargetStatus = "500";
            ctx.TargetState = CasinoMovimentiBuffer.States.Deleted;
            ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Deleted;

            ctx.Response["responseCodeReason"] = "500";
            ctx.Response["errorMessage"] = "UNHANDLED";
            ctx.Response["balance"] = 0L;
            ctx.Response["casinoTransferId"] = string.Empty;
        }
    }
}
