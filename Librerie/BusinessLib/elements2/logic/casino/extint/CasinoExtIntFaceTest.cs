using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.account.ext;
using it.capecod.gridgame.business.elements2.logic.general;
using it.capecod.util;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint
{
    public abstract class CasinoExtIntFaceTest
    {
        public enum TransferType
        {
            Cash = 10,
            UnWithDr = 12,
            WithDr = 15,
            RealBonus = 20,
            FunBonus = 30
        }

        /// <summary>
        /// Controlla se eu è gia registrato nel sistema
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult checkUserRegistered(EndUser eu, Games game, HashParams extPars);

        /// <summary>
        /// Registra eu su wm
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <param name="author"></param>
        /// <returns></returns>
        public abstract HashResult registerUser(EndUser eu, Games game, HashParams extPars, string author);

        /// <summary>
        /// Ritorna la lista dei giochi disponibili
        /// </summary>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult getAvailableGames(HashParams extPars);

        /// <summary>
        /// ritorna i balances dell'utente
        /// valori presenti se esito positivo CASH, BONUS (int)
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult getCurrentBalances(EndUser eu, Games game, HashParams extPars);

        /// <summary>
        /// Esegue il deposito sul sistema remoto
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="amount"></param>
        /// <param name="transferType"></param>
        /// <param name="extPars"></param>
        /// <param name="author"></param>
        /// <returns></returns>
        public abstract HashResult sitIn(EndUser eu, Games game, long amount, TransferType transferType, HashParams extPars, string author);

        /// <summary>
        /// Esegue il prelievo sul sistema remoto
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="amount"></param>
        /// <param name="transferType"></param>
        /// <param name="extPars"></param>
        /// <param name="author"></param>
        /// <returns></returns>
        public abstract HashResult sitOut(EndUser eu, Games game, long amount, TransferType transferType, HashParams extPars, string author);

        /// <summary>
        /// Esegue il login sul sistema remoto
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult logIn(EndUser eu, Games game, HashParams extPars);

        /// <summary>
        /// Esegue il logout sul sistema remoto
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult logOut(EndUser eu, Games game, HashParams extPars);

        /// <summary>
        /// Ritorna le bet dell ultima sessione o della sessione specificata
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="specificSessionToken"></param>
        /// <param name="extPars"></param>
        /// <returns>nel risultato contiene 
        /// BonusBetAmount, BonusWinAmount, CashBetAmount, CashWinAmount (totali di sessione) e SessionToken<br/>
        /// BETS che è ArrayList di item-Hashtable, <br/>
        /// ogni item contiene: BetID, BonusBetAmount, BonusWinAmount, CashBetAmount, CashWinAmount, EndDate<br/>
        /// </returns>
        public abstract HashResult getSessionBalances(EndUser eu, Games game, string specificSessionToken, HashParams extPars);

        /// <summary>
        /// Ritorna url di start del gioco
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult getStartUrl(EndUser eu, Games game, HashParams extPars);

        /// <summary>
        /// Ritorna url "storico" mani per la specifica BetID
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="wmBetId"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult getHistoryUrl(EndUser eu, Games game, string wmBetId, HashParams extPars);

        /// <summary>
        /// Ritorna la lista di sessioni pendenti<br/>
        /// PENDING:arraylist di token delle sess pendenti
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult getPendingSessions(EndUser eu, Games game, HashParams extPars);

        /// <summary>
        /// termina le sessioni pendenti
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult completePendingSessions(EndUser eu, Games game, HashParams extPars);

        /// <summary>
        /// Indica se in caso di incongruenza il sitout viene ritentato successivamente invece che andare subito in errore
        /// </summary>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract bool delayedRetrySitoutOnInconsistency(HashParams extPars);

        /// <summary>
        /// Servizio per il recupero del dettaglio giocate
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="seqPlay"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract HashResult getPlayDetail(EndUser eu, Games game, string seqPlay, HashParams extPars);

        /// <summary>
        /// ritorna le info estese dell utente
        /// </summary>
        /// <param name="eu"></param>
        /// <param name="game"></param>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract EndUserExtInt getUserExtension(EndUser eu, Games game, HashParams extPars);

        /// <summary>
        /// ritorna integrazione specifica
        /// </summary>
        /// <param name="extPars"></param>
        /// <returns></returns>
        public abstract ExternalIntegration getExtIntegration(HashParams extPars);

        /// <summary>
        /// ritorna info ausiliarie
        /// </summary>
        /// <param name="auxInfo"></param>
        /// <param name="auxPar"></param>
        /// <returns></returns>
        public abstract HashResult getAuxInfos(string auxInfo, HashParams auxPar);
    }
}
