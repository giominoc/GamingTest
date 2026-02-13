using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.account.ext;
using it.capecod.gridgame.business.elements2.logic.casino;
using it.capecod.gridgame.business.util.data;
using it.capecod.log;
using it.capecod.util;
using System;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Cancel
{
    public sealed class CancelCtx : CasinoExtIntPipelineBase.IBaseCtx
    {
        // ---- Identity / request ----
        public HashParams AuxPars { get; }
        public string RawRequest { get; set; }
        public string Author { get; set; }
        public string TransactionId { get; set; }
        public string Ticket { get; set; }
        public string RoundRef { get; set; }

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
        public CasinoExtIntPipelineBase.SessionBalancesHolder Counters { get; set; }

        // ---- DB movements ----
        public CasinoMovimentiBuffer IdempotencyMov { get; set; }
        public CasinoMovimentiBuffer NewMov { get; set; }
        public CasinoMovimentiBuffer RelatedBetMov { get; set; }

        // ---- External transfer (seamless/external wallet) ----
        public HashResult ExternalTransferResult { get; set; }

        // ---- Response ----
        public SmartHash Response { get; } = new SmartHash();

        // ---- Pipeline controls ----
        public string JumpToKey { get; set; }
        public bool Stop { get; set; }

        public CancelCtx(int euId, HashParams auxPars)
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
}
