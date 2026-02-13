using it.capecod.gridgame.business.elements2.logic.account.ext;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet.Components
{
    /// <summary>
    /// Componente standard: Context Base Generation.
    /// Popola i campi base del contesto dai parametri.
    /// </summary>
    public static class ContextBaseGenerationComponent
    {
        public const string Key = "ContextBaseGeneration";

        public static void Execute(BetContext ctx)
        {
            ctx.Author = ctx.AuxPars.getTypedValue("author", "system", false);
            ctx.RawRequest = ctx.AuxPars.getTypedValue("rawRequest", string.Empty, false);
            ctx.TransactionId = ctx.AuxPars.getTypedValue("transactionId", string.Empty, false);
            ctx.Ticket = ctx.AuxPars.getTypedValue("ticket", string.Empty, false);
            ctx.Service = FinMovExtern.Servizi.CasinoAM;
        }
    }
}
