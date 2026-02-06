using it.capecod.util;
using System.Collections;
using System.Collections.Generic;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Bet
{
    /// <summary>
    /// Macro (Build/Run) della pipeline di Bet.
    /// </summary>
    public abstract class CasinoExtIntBetPipeline : BetHooks
    {
        protected virtual List<Step<BetCtx>> BuildStandardBetSteps()
        {
            return new List<Step<BetCtx>>
            {
                new Step<BetCtx>(BetHook.ResponseDefinition,      Bet_ResponseDefinition),
                new Step<BetCtx>(BetHook.ContextBaseGeneration,   Bet_ContextBaseGeneration),
                new Step<BetCtx>(BetHook.RequestValidation,       Bet_RequestValidation),
                new Step<BetCtx>(BetHook.IdempotencyLookup,       Bet_IdempotencyLookup),
                new Step<BetCtx>(BetHook.LoadSession,             Bet_LoadSession),
                new Step<BetCtx>(BetHook.BalanceCheck,            Bet_BalanceCheck),
                new Step<BetCtx>(BetHook.CreateMovement,          Bet_CreateMovement),
                new Step<BetCtx>(BetHook.PersistMovementCreate,   Bet_PersistMovementCreate),
                new Step<BetCtx>(BetHook.ExecuteExternalTransfer, Bet_ExecuteExternalTransfer),
                new Step<BetCtx>(BetHook.PersistMovementFinalize, Bet_PersistMovementFinalize),
                new Step<BetCtx>(BetHook.BuildResponse,           Bet_BuildResponse),

                // Target per Jump idempotency
                new Step<BetCtx>(BetHook.Resend,                  Bet_Resend)
            };
        }
        protected CompiledSteps<BetCtx> BuildBetPipeline()
        {
            var steps = BuildStandardBetSteps();
            CustomizeBetSteps(steps);
            return new CompiledSteps<BetCtx>(steps.ToArray());
        }

        protected Hashtable RunBetPipeline(int euId, HashParams auxPars, CompiledSteps<BetCtx> compiledSteps)
        {
            var ctx = new BetCtx(euId, auxPars);
            RunSteps(compiledSteps, ctx, c => c.Stop);
            return new Hashtable(ctx.Response);
        }
    }
}
