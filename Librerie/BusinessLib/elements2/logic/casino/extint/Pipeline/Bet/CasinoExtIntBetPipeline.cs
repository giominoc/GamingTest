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
                new Step<BetCtx>(BetHook.ResponseDefinition,      Bet_ResponseDefinition,      BetHook.ResponseDefinition.ToString()),
                new Step<BetCtx>(BetHook.ContextBaseGeneration,   Bet_ContextBaseGeneration,   BetHook.ContextBaseGeneration.ToString()),
                new Step<BetCtx>(BetHook.RequestValidation,       Bet_RequestValidation,       BetHook.RequestValidation.ToString()),
                new Step<BetCtx>(BetHook.IdempotencyLookup,       Bet_IdempotencyLookup,       BetHook.IdempotencyLookup.ToString()),
                new Step<BetCtx>(BetHook.LoadSession,             Bet_LoadSession,             BetHook.LoadSession.ToString()),
                new Step<BetCtx>(BetHook.BalanceCheck,            Bet_BalanceCheck,            BetHook.BalanceCheck.ToString()),
                new Step<BetCtx>(BetHook.CreateMovement,          Bet_CreateMovement,          BetHook.CreateMovement.ToString()),
                new Step<BetCtx>(BetHook.PersistMovementCreate,   Bet_PersistMovementCreate,   BetHook.PersistMovementCreate.ToString()),
                new Step<BetCtx>(BetHook.ExecuteExternalTransfer, Bet_ExecuteExternalTransfer, BetHook.ExecuteExternalTransfer.ToString()),
                new Step<BetCtx>(BetHook.PersistMovementFinalize, Bet_PersistMovementFinalize, BetHook.PersistMovementFinalize.ToString()),
                new Step<BetCtx>(BetHook.BuildResponse,           Bet_BuildResponse,           BetHook.BuildResponse.ToString()),

                // Target per Jump idempotency
                new Step<BetCtx>(BetHook.Resend,                  Bet_Resend,                  BetHook.Resend.ToString()),
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
