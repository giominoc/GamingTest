using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core;
using System;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Cancel
{
    /// <summary>
    /// Customizer per la pipeline Cancel.
    /// Applica patch al piano standard per integrazioni/provider specifici.
    /// 
    /// Operazioni disponibili:
    /// - Replace: sostituisce un componente
    /// - InsertAfter/InsertBefore: aggiunge un componente
    /// - Remove: rimuove un componente
    /// - Wrap: avvolge un componente con logica aggiuntiva
    /// </summary>
    public static class CancelPipelineCustomizer
    {
        /// <summary>
        /// Applica customizzazioni al piano Cancel basandosi sull'integrazione.
        /// 
        /// Esempio: per CasinoAM, sostituisci i placeholder con implementazioni concrete.
        /// Esempio: per un provider specifico, aggiungi validazioni extra.
        /// </summary>
        public static void ApplyCustomizations(PipelinePlan<CancelContext> plan, string integration)
        {
            if (string.IsNullOrWhiteSpace(integration))
                return;

            switch (integration.ToUpperInvariant())
            {
                case "CASINOAM":
                    ApplyCasinoAMCustomizations(plan);
                    break;

                // Altri provider possono essere aggiunti qui
                // case "PROVIDER_X":
                //     ApplyProviderXCustomizations(plan);
                //     break;

                default:
                    // Nessuna customizzazione per integrazioni sconosciute
                    break;
            }
        }

        /// <summary>
        /// Customizzazioni specifiche per CasinoAM.
        /// ESEMPIO: implementazione placeholder per dimostrare il pattern.
        /// </summary>
        private static void ApplyCasinoAMCustomizations(PipelinePlan<CancelContext> plan)
        {
            // ESEMPIO 1: Replace del componente RequestValidation con implementazione CasinoAM
            plan.Replace("RequestValidation", new PipelineComponent<CancelContext>(
                "RequestValidation",
                ctx => {
                    // Implementazione placeholder - in produzione chiamare logica reale
                    var transactionId = ctx.AuxPars.getTypedValue("transactionId", string.Empty, false);
                    var roundRef = ctx.AuxPars.getTypedValue("roundRef", string.Empty, false);

                    if (string.IsNullOrWhiteSpace(transactionId) && string.IsNullOrWhiteSpace(roundRef))
                    {
                        ctx.TargetStatus = "409";
                        ctx.Response["responseCodeReason"] = "409";
                        ctx.Response["errorMessage"] = "BAD_REQUEST";
                        ctx.Stop = true;
                        return;
                    }

                    ctx.CmbType = it.capecod.gridgame.business.elements2.logic.casino.CasinoMovimentiBuffer.Type.LooseCancel;
                    ctx.TargetState = it.capecod.gridgame.business.elements2.logic.casino.CasinoMovimentiBuffer.States.PreDumped;
                    ctx.TargetStateFinal = it.capecod.gridgame.business.elements2.logic.casino.CasinoMovimentiBuffer.States.Deleted;
                },
                "Validate request for CasinoAM"));

            // ESEMPIO 2: Replace del componente LoadSession
            plan.Replace("LoadSession", new PipelineComponent<CancelContext>(
                "LoadSession",
                ctx => {
                    try
                    {
                        var ticket = ctx.AuxPars.getTypedValue("ticket", string.Empty, false);
                        if (string.IsNullOrWhiteSpace(ticket))
                        {
                            ctx.TargetStatus = "401";
                            ctx.Response["responseCodeReason"] = "401";
                            ctx.Response["errorMessage"] = "INVALID_SESSION";
                            ctx.Stop = true;
                            return;
                        }

                        // Placeholder: in produzione chiamare metodi reali
                        ctx.SessionInfos = new it.capecod.gridgame.business.util.data.HashParams(
                            "MATCH_Id", -1,
                            "centEU_Id", ctx.CentEu.EU_ID
                        );
                    }
                    catch (Exception ex)
                    {
                        it.capecod.log.Log.exc(ex);
                        ctx.TargetStatus = "500";
                        ctx.Response["errorMessage"] = "SESSION_ERROR";
                        ctx.Stop = true;
                    }
                },
                "Load session for CasinoAM"));

            // ESEMPIO 3: Replace del componente ExecuteExternalTransfer
            plan.Replace("ExecuteExternalTransfer", new PipelineComponent<CancelContext>(
                "ExecuteExternalTransfer",
                ctx => {
                    try
                    {
                        // Placeholder: in produzione chiamare API wallet esterna
                        ctx.TargetStatus = "200";
                        ctx.TargetStateFinal = it.capecod.gridgame.business.elements2.logic.casino.CasinoMovimentiBuffer.States.Deleted;
                        ctx.Response["balance"] = 1200L;
                    }
                    catch (Exception ex)
                    {
                        it.capecod.log.Log.exc(ex);
                        ctx.TargetStatus = "500";
                        ctx.TargetStateFinal = it.capecod.gridgame.business.elements2.logic.casino.CasinoMovimentiBuffer.States.Deleted;
                        ctx.Response["errorMessage"] = "TRANSFER_FAILED";
                    }
                },
                "Execute external transfer for CasinoAM"));

            // ESEMPIO 4: InsertAfter - aggiungere un componente custom dopo RequestValidation
            // (questo è solo un esempio per dimostrare il pattern)
            /*
            plan.InsertAfter("RequestValidation", new PipelineComponent<CancelContext>(
                "CasinoAMPreCheck",
                ctx => {
                    // Custom pre-check specifico per CasinoAM
                    // Es: validazioni aggiuntive, logging, metriche
                },
                "CasinoAM specific pre-check"));
            */

            // ESEMPIO 5: Wrap - avvolgere un componente con logging/telemetry
            // (questo è solo un esempio per dimostrare il pattern)
            /*
            plan.Wrap("ExecuteExternalTransfer", originalAction => ctx => {
                var startTime = DateTime.UtcNow;
                try
                {
                    originalAction(ctx);
                }
                finally
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    // Log telemetry: "ExecuteExternalTransfer took {elapsed}ms"
                }
            }, "ExecuteExternalTransfer with telemetry");
            */
        }

        /// <summary>
        /// ESEMPIO: Customizzazioni per un altro provider (dimostra estensibilità).
        /// </summary>
        private static void ApplyExampleProviderCustomizations(PipelinePlan<CancelContext> plan)
        {
            // Esempio: un provider potrebbe avere bisogno di validazioni diverse
            plan.Replace("RequestValidation", new PipelineComponent<CancelContext>(
                "RequestValidation",
                ctx => {
                    // Logica di validazione custom per questo provider
                    // ...
                },
                "Validate request for Example Provider"));

            // Esempio: potrebbe aver bisogno di un componente aggiuntivo
            plan.InsertAfter("IdempotencyLookup", new PipelineComponent<CancelContext>(
                "ExampleProviderFraudCheck",
                ctx => {
                    // Check antifrode specifico del provider
                    // ...
                },
                "Example Provider fraud check"));
        }
    }
}
