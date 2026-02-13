using it.capecod.util;
using System.Collections;
using System.Collections.Generic;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Win
{
    /// <summary>
    /// Macro (Build/Run) della pipeline di Win (Credit/Deposit).
    /// </summary>
    public abstract class CasinoExtIntWinPipeline : WinHooks
    {
        protected virtual List<Step<WinCtx>> BuildStandardWinSteps()
        {
            return new List<Step<WinCtx>>
            {
                new Step<WinCtx>(WinHook.ResponseDefinition,      Win_ResponseDefinition,      WinHook.ResponseDefinition.ToString()),
                new Step<WinCtx>(WinHook.ContextBaseGeneration,   Win_ContextBaseGeneration,   WinHook.ContextBaseGeneration.ToString()),
                new Step<WinCtx>(WinHook.RequestValidation,       Win_RequestValidation,       WinHook.RequestValidation.ToString()),
                new Step<WinCtx>(WinHook.IdempotencyLookup,       Win_IdempotencyLookup,       WinHook.IdempotencyLookup.ToString()),
                new Step<WinCtx>(WinHook.LoadSession,             Win_LoadSession,             WinHook.LoadSession.ToString()),
                new Step<WinCtx>(WinHook.CreateMovement,          Win_CreateMovement,          WinHook.CreateMovement.ToString()),
                new Step<WinCtx>(WinHook.PersistMovementCreate,   Win_PersistMovementCreate,   WinHook.PersistMovementCreate.ToString()),
                new Step<WinCtx>(WinHook.ExecuteExternalTransfer, Win_ExecuteExternalTransfer, WinHook.ExecuteExternalTransfer.ToString()),
                new Step<WinCtx>(WinHook.PersistMovementFinalize, Win_PersistMovementFinalize, WinHook.PersistMovementFinalize.ToString()),
                new Step<WinCtx>(WinHook.BuildResponse,           Win_BuildResponse,           WinHook.BuildResponse.ToString()),

                // Target per Jump idempotency
                new Step<WinCtx>(WinHook.Resend,                  Win_Resend,                  WinHook.Resend.ToString()),
            };
        }

        protected CompiledSteps<WinCtx> BuildWinPipeline()
        {
            var steps = BuildStandardWinSteps();
            CustomizeWinSteps(steps);
            return new CompiledSteps<WinCtx>(steps.ToArray());
        }

        protected Hashtable RunWinPipeline(int euId, HashParams auxPars, CompiledSteps<WinCtx> compiledSteps)
        {
            var ctx = new WinCtx(euId, auxPars);
            RunSteps(compiledSteps, ctx, c => c.Stop);
            return new Hashtable(ctx.Response);
        }
    }
}
