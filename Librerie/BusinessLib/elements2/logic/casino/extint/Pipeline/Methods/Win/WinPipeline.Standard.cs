using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Win.Components;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Win
{
    /// <summary>
    /// Piano standard della pipeline Win (Credit/Deposit).
    /// Definisce il superset "massimo livello" di componenti per il metodo Win.
    /// 
    /// Questo piano rappresenta il flusso completo e può essere customizzato
    /// per provider specifici tramite WinPipelineCustomizer.
    /// </summary>
    public static class WinPipelineStandard
    {
        /// <summary>
        /// Crea il piano standard per Win.
        /// Include tutti i componenti nel loro ordine canonico.
        /// 
        /// NOTA: Alcuni componenti sono placeholders e devono essere implementati
        /// per integrazione specifica (es. RequestValidation, LoadSession, ExecuteExternalTransfer).
        /// </summary>
        public static PipelinePlan<WinContext> CreateStandardPlan()
        {
            var plan = new PipelinePlan<WinContext>();

            // 1. Response Definition - inizializza response di default
            plan.Add(new PipelineComponent<WinContext>(
                ResponseDefinitionComponent.Key,
                ResponseDefinitionComponent.Execute,
                "Initialize default response"));

            // 2. Context Base Generation - popola campi base dal request
            plan.Add(new PipelineComponent<WinContext>(
                ContextBaseGenerationComponent.Key,
                ContextBaseGenerationComponent.Execute,
                "Populate base context fields"));

            // 3. Request Validation - DEVE essere implementato per integrazione
            plan.Add(new PipelineComponent<WinContext>(
                "RequestValidation",
                ctx => { /* PLACEHOLDER - implementare per integrazione */ },
                "Validate request parameters (PLACEHOLDER)"));

            // 4. Idempotency Lookup - controlla duplicati
            plan.Add(new PipelineComponent<WinContext>(
                IdempotencyLookupComponent.Key,
                IdempotencyLookupComponent.Execute,
                "Check for duplicate transactions"));

            // 5. Load Session - DEVE essere implementato per integrazione
            plan.Add(new PipelineComponent<WinContext>(
                "LoadSession",
                ctx => { /* PLACEHOLDER - implementare per integrazione */ },
                "Load session and match info (PLACEHOLDER)"));

            // 6. Create Movement - crea oggetto movimento
            plan.Add(new PipelineComponent<WinContext>(
                CreateMovementComponent.Key,
                CreateMovementComponent.Execute,
                "Create movement object"));

            // 7. Persist Movement Create - salva movimento (stato iniziale)
            plan.Add(new PipelineComponent<WinContext>(
                PersistMovementCreateComponent.Key,
                PersistMovementCreateComponent.Execute,
                "Persist movement to database"));

            // 8. Execute External Transfer - DEVE essere implementato per integrazione
            plan.Add(new PipelineComponent<WinContext>(
                "ExecuteExternalTransfer",
                ctx => { /* PLACEHOLDER - implementare per integrazione */ },
                "Execute external wallet transfer (PLACEHOLDER)"));

            // 9. Persist Movement Finalize - aggiorna movimento (stato finale)
            plan.Add(new PipelineComponent<WinContext>(
                PersistMovementFinalizeComponent.Key,
                PersistMovementFinalizeComponent.Execute,
                "Finalize movement in database"));

            // 10. Build Response - costruisce response finale
            plan.Add(new PipelineComponent<WinContext>(
                BuildResponseComponent.Key,
                BuildResponseComponent.Execute,
                "Build final response"));

            // 11. Resend - target per jump idempotency
            plan.Add(new PipelineComponent<WinContext>(
                ResendComponent.Key,
                ResendComponent.Execute,
                "Resend existing transaction response"));

            return plan;
        }
    }
}
