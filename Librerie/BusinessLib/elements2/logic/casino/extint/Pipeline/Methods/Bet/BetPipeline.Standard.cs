using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet.Components;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet
{
    /// <summary>
    /// Piano standard della pipeline Bet (Debit/Withdrawal).
    /// Definisce il superset "massimo livello" di componenti per il metodo Bet.
    /// 
    /// Questo piano rappresenta il flusso completo e può essere customizzato
    /// per provider specifici tramite BetPipelineCustomizer.
    /// </summary>
    public static class BetPipelineStandard
    {
        /// <summary>
        /// Crea il piano standard per Bet.
        /// Include tutti i componenti nel loro ordine canonico.
        /// 
        /// NOTA: Alcuni componenti sono placeholders e devono essere implementati
        /// per integrazione specifica (es. RequestValidation, LoadSession, ExecuteExternalTransfer).
        /// </summary>
        public static PipelinePlan<BetContext> CreateStandardPlan()
        {
            var plan = new PipelinePlan<BetContext>();

            // 1. Response Definition - inizializza response di default
            plan.Add(new PipelineComponent<BetContext>(
                ResponseDefinitionComponent.Key,
                ResponseDefinitionComponent.Execute,
                "Initialize default response"));

            // 2. Context Base Generation - popola campi base dal request
            plan.Add(new PipelineComponent<BetContext>(
                ContextBaseGenerationComponent.Key,
                ContextBaseGenerationComponent.Execute,
                "Populate base context fields"));

            // 3. Request Validation - DEVE essere implementato per integrazione
            plan.Add(new PipelineComponent<BetContext>(
                "RequestValidation",
                ctx => { /* PLACEHOLDER - implementare per integrazione */ },
                "Validate request parameters (PLACEHOLDER)"));

            // 4. Idempotency Lookup - controlla duplicati
            plan.Add(new PipelineComponent<BetContext>(
                IdempotencyLookupComponent.Key,
                IdempotencyLookupComponent.Execute,
                "Check for duplicate transactions"));

            // 5. Load Session - DEVE essere implementato per integrazione
            plan.Add(new PipelineComponent<BetContext>(
                "LoadSession",
                ctx => { /* PLACEHOLDER - implementare per integrazione */ },
                "Load session and match info (PLACEHOLDER)"));

            // 6. Balance Check - verifica saldo disponibile
            plan.Add(new PipelineComponent<BetContext>(
                BalanceCheckComponent.Key,
                BalanceCheckComponent.Execute,
                "Check available balance"));

            // 7. Create Movement - crea oggetto movimento
            plan.Add(new PipelineComponent<BetContext>(
                CreateMovementComponent.Key,
                CreateMovementComponent.Execute,
                "Create movement object"));

            // 8. Persist Movement Create - salva movimento (stato iniziale)
            plan.Add(new PipelineComponent<BetContext>(
                PersistMovementCreateComponent.Key,
                PersistMovementCreateComponent.Execute,
                "Persist movement to database"));

            // 9. Execute External Transfer - DEVE essere implementato per integrazione
            plan.Add(new PipelineComponent<BetContext>(
                "ExecuteExternalTransfer",
                ctx => { /* PLACEHOLDER - implementare per integrazione */ },
                "Execute external wallet transfer (PLACEHOLDER)"));

            // 10. Persist Movement Finalize - aggiorna movimento (stato finale)
            plan.Add(new PipelineComponent<BetContext>(
                PersistMovementFinalizeComponent.Key,
                PersistMovementFinalizeComponent.Execute,
                "Finalize movement in database"));

            // 11. Build Response - costruisce response finale
            plan.Add(new PipelineComponent<BetContext>(
                BuildResponseComponent.Key,
                BuildResponseComponent.Execute,
                "Build final response"));

            // 12. Resend - target per jump idempotency
            plan.Add(new PipelineComponent<BetContext>(
                ResendComponent.Key,
                ResendComponent.Execute,
                "Resend existing transaction response"));

            return plan;
        }
    }
}
