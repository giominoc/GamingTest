namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet.Components
{
    /// <summary>
    /// Componente standard: Balance Check.
    /// Verifica che il saldo sia sufficiente per la scommessa.
    /// </summary>
    public static class BalanceCheckComponent
    {
        public const string Key = "BalanceCheck";

        public static void Execute(BetContext ctx)
        {
            // Default neutro - implementazione concreta in customizzazioni
        }
    }
}
