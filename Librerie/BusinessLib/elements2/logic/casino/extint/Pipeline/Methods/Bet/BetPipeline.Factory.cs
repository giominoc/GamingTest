using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core;
using it.capecod.util;
using System;
using System.Collections;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet
{
    /// <summary>
    /// Factory per creare e eseguire la pipeline Bet.
    /// Seleziona standard plan + customizer in base a integrazione/config,
    /// compila la pipeline finale e la esegue.
    /// </summary>
    public static class BetPipelineFactory
    {
        /// <summary>
        /// Crea la pipeline compilata per l'integrazione specificata.
        /// </summary>
        public static PipelineComponent<BetContext>[] CreatePipeline(string integration = null)
        {
            // 1. Crea piano standard
            var plan = BetPipelineStandard.CreateStandardPlan();

            // 2. Applica customizzazioni per integrazione
            BetPipelineCustomizer.ApplyCustomizations(plan, integration);

            // 3. Compila e valida
            var compiled = plan.Compile();
            PipelineDiagnostics.ValidatePipeline(compiled);

            return compiled;
        }

        /// <summary>
        /// Esegue la pipeline Bet completa.
        /// </summary>
        public static Hashtable Execute(int euId, HashParams auxPars, string integration = null)
        {
            // Crea pipeline
            var pipeline = CreatePipeline(integration);

            // Crea contesto
            var ctx = new BetContext(euId, auxPars);

            // Esegui pipeline
            PipelineEngine.Run(pipeline, ctx, c => c.Stop);

            // Restituisci response
            return new Hashtable(ctx.Response);
        }

        /// <summary>
        /// Ottiene la pipeline standard (senza customizzazioni) per diagnostica/testing.
        /// </summary>
        public static PipelineComponent<BetContext>[] GetStandardPipeline()
        {
            var plan = BetPipelineStandard.CreateStandardPlan();
            return plan.Compile();
        }

        /// <summary>
        /// Diagnostica: confronta standard vs customized per una integrazione.
        /// </summary>
        public static string GetDiagnostics(string integration = null)
        {
            var standard = GetStandardPipeline();
            var customized = CreatePipeline(integration);

            var diff = PipelineDiagnostics.ComparePipelines(
                standard,
                customized,
                "Standard Bet Pipeline",
                $"Bet Pipeline for {integration ?? "default"}");

            var finalPlan = PipelineDiagnostics.PrintPipeline(
                customized,
                $"Final Bet Pipeline ({integration ?? "default"})");

            return diff + "\n\n" + finalPlan;
        }
    }
}
