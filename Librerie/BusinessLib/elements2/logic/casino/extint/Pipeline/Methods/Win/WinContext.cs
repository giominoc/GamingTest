using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core;
using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.account.ext;
using it.capecod.gridgame.business.elements2.logic.casino;
using it.capecod.gridgame.business.util.data;
using it.capecod.log;
using it.capecod.util;
using System;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Win
{
    /// <summary>
    /// Contesto per la pipeline Win (Credit/Deposit).
    /// Contiene tutti gli input, stato di esecuzione, e output del metodo.
    /// </summary>
    public sealed class WinContext : IPipelineContext
    {
        // ---- Identity / request ----
        public HashParams AuxPars { get; }
        public string RawRequest { get; set; }
        public string Author { get; set; }
        public string TransactionId { get; set; }
        public string Ticket { get; set; }

        // ---- Users (perif + central) ----
        public EndUser PerifEu { get; }
        public EndUser CentEu { get; }

        // ---- Flow / domain ----
        public FinMovExtern.Servizi Service { get; set; }
        public CasinoMovimentiBuffer.Type CmbType { get; set; }
        public CasinoMovimentiBuffer.States TargetState { get; set; }
        public CasinoMovimentiBuffer.States TargetStateFinal { get; set; }
        public string TargetStatus { get; set; } = "500";

        // ---- Session / match ----
        public CasinoSession CurrSession { get; set; }
        public CasinoSubscription CurrMatch { get; set; }
        public HashParams SessionInfos { get; set; }
        public SessionBalancesHolder Counters { get; set; }

        // ---- DB movements ----
        public CasinoMovimentiBuffer IdempotencyMov { get; set; }
        public CasinoMovimentiBuffer NewMov { get; set; }

        // ---- External transfer (seamless/external wallet) ----
        public HashResult ExternalTransferResult { get; set; }

        // ---- Response ----
        public SmartHash Response { get; } = new SmartHash();

        // ---- Pipeline controls (IPipelineContext) ----
        public string JumpToKey { get; set; }
        public bool Stop { get; set; }

        public WinContext(int euId, HashParams auxPars)
        {
            AuxPars = auxPars ?? new HashParams();

            // Perif EU
            var perif = new EndUser { EU_ID = euId };
            try { perif.getByUserId(); } catch (Exception ex) { Log.exc(ex); }

            // Central EU
            var cent = new EndUser { EU_ID = euId };
            try
            {
                using (new GGConnMgr.CentralCtxWrap("CASINO"))
                    cent.getByUserId();
            }
            catch (Exception ex) { Log.exc(ex); }

            PerifEu = perif;
            CentEu = cent;
        }
    }

    /// <summary>
    /// Helper per tracciare i balance di sessione in modo veloce.
    /// </summary>
    public class SessionBalancesHolder
    {
        public long amountTotal;
        public long amountCash;
        public long amountBonus;
        public long amountPlayBonus;
        public long amountWith;
        public long amountDepo;
        public int lastRemFr;
    }
}
