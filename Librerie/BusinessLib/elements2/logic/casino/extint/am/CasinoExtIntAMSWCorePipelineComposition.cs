using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Win;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Cancel;
using it.capecod.util;
using System.Collections;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.am
{
    /// <summary>
    /// Implementazione CasinoAM usando architettura pipeline COMPOSITION.
    /// 
    /// Questa classe fornisce l'interfaccia pubblica per eseguire le tre operazioni wallet:
    /// - Bet (Withdraw/Debit)
    /// - Win (Deposit/Credit)
    /// - Cancel (RollBack)
    /// 
    /// Ogni metodo usa la propria pipeline configurata tramite Factory.
    /// </summary>
    public class CasinoExtIntAMSWCorePipelineComposition
    {
        private const string IntegrationName = "CasinoAM";

        /// <summary>
        /// Esegue un Bet (Withdraw/Debit) tramite pipeline.
        /// </summary>
        public Hashtable ExecuteBet(int euId, HashParams auxPars)
        {
            return BetPipelineFactory.Execute(euId, auxPars, IntegrationName);
        }

        /// <summary>
        /// Esegue un Win (Deposit/Credit) tramite pipeline.
        /// </summary>
        public Hashtable ExecuteWin(int euId, HashParams auxPars)
        {
            return WinPipelineFactory.Execute(euId, auxPars, IntegrationName);
        }

        /// <summary>
        /// Esegue un Cancel (RollBack) tramite pipeline.
        /// </summary>
        public Hashtable ExecuteCancel(int euId, HashParams auxPars)
        {
            return CancelPipelineFactory.Execute(euId, auxPars, IntegrationName);
        }

        /// <summary>
        /// Ottiene diagnostica per la pipeline Bet.
        /// Mostra: standard plan, customizzazioni applicate, pipeline finale.
        /// </summary>
        public string GetBetDiagnostics()
        {
            return BetPipelineFactory.GetDiagnostics(IntegrationName);
        }

        /// <summary>
        /// Ottiene diagnostica per la pipeline Win.
        /// Mostra: standard plan, customizzazioni applicate, pipeline finale.
        /// </summary>
        public string GetWinDiagnostics()
        {
            return WinPipelineFactory.GetDiagnostics(IntegrationName);
        }

        /// <summary>
        /// Ottiene diagnostica per la pipeline Cancel.
        /// Mostra: standard plan, customizzazioni applicate, pipeline finale.
        /// </summary>
        public string GetCancelDiagnostics()
        {
            return CancelPipelineFactory.GetDiagnostics(IntegrationName);
        }
    }
}
