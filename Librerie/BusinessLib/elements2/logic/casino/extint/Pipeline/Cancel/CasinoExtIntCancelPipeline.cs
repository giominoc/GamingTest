using it.capecod.util;
using System.Collections;
using System.Collections.Generic;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Cancel
{
    /// <summary>
    /// Macro (Build/Run) della pipeline di Cancel (RollBack).
    /// </summary>
    public abstract class CasinoExtIntCancelPipeline : CancelHooks
    {
        protected virtual List<Step<CancelCtx>> BuildStandardCancelSteps()
        {
            return new List<Step<CancelCtx>>
            {
                new Step<CancelCtx>(CancelHook.ResponseDefinition,      Cancel_ResponseDefinition,      CancelHook.ResponseDefinition.ToString()),
                new Step<CancelCtx>(CancelHook.ContextBaseGeneration,   Cancel_ContextBaseGeneration,   CancelHook.ContextBaseGeneration.ToString()),
                new Step<CancelCtx>(CancelHook.RequestValidation,       Cancel_RequestValidation,       CancelHook.RequestValidation.ToString()),
                new Step<CancelCtx>(CancelHook.IdempotencyLookup,       Cancel_IdempotencyLookup,       CancelHook.IdempotencyLookup.ToString()),
                new Step<CancelCtx>(CancelHook.LoadSession,             Cancel_LoadSession,             CancelHook.LoadSession.ToString()),
                new Step<CancelCtx>(CancelHook.FindRelatedBet,          Cancel_FindRelatedBet,          CancelHook.FindRelatedBet.ToString()),
                new Step<CancelCtx>(CancelHook.CreateMovement,          Cancel_CreateMovement,          CancelHook.CreateMovement.ToString()),
                new Step<CancelCtx>(CancelHook.PersistMovementCreate,   Cancel_PersistMovementCreate,   CancelHook.PersistMovementCreate.ToString()),
                new Step<CancelCtx>(CancelHook.ExecuteExternalTransfer, Cancel_ExecuteExternalTransfer, CancelHook.ExecuteExternalTransfer.ToString()),
                new Step<CancelCtx>(CancelHook.PersistMovementFinalize, Cancel_PersistMovementFinalize, CancelHook.PersistMovementFinalize.ToString()),
                new Step<CancelCtx>(CancelHook.BuildResponse,           Cancel_BuildResponse,           CancelHook.BuildResponse.ToString()),

                // Target per Jump idempotency
                new Step<CancelCtx>(CancelHook.Resend,                  Cancel_Resend,                  CancelHook.Resend.ToString()),
            };
        }

        protected CompiledSteps<CancelCtx> BuildCancelPipeline()
        {
            var steps = BuildStandardCancelSteps();
            CustomizeCancelSteps(steps);
            return new CompiledSteps<CancelCtx>(steps.ToArray());
        }

        protected Hashtable RunCancelPipeline(int euId, HashParams auxPars, CompiledSteps<CancelCtx> compiledSteps)
        {
            var ctx = new CancelCtx(euId, auxPars);
            RunSteps(compiledSteps, ctx, c => c.Stop);
            return new Hashtable(ctx.Response);
        }
    }
}
