using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.account.ext;
using it.capecod.gridgame.business.elements2.logic.casino;
using it.capecod.gridgame.business.util.data;
using it.capecod.log;
using it.capecod.util;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline
{
    /// <summary>
    /// Base astratta per implementare i metodi wallet tramite una pipeline di hook (step).
    ///
    /// Obiettivi:
    /// - Forzare un macro-flow consistente (hook ordinati).
    /// - Consentire customizzazioni provider-specific senza riscrivere il flow:
    ///   - InsertAfter(hook, step)
    ///   - Replace(hook, step)
    /// - Supportare branching controllato tramite JumpToKey (es. idempotency/resend).
    ///
    /// Nota: questa base NON vuole essere "un framework" completo, ma un modo pratico per
    /// estrarre i blocchi ricorrenti dai metodi wallet reali.
    /// </summary>
    public abstract class CasinoExtIntPipelineBase : CasinoExtIntFaceTest
    {
        // =====================================================================
        //  Steps (hook) + compiled pipeline
        // =====================================================================

        /// <summary>
        /// Rappresenta uno step della pipeline.
        ///
        /// - Hook: enum value (utile per Replace/InsertAfter e logging).
        /// - Key: chiave univoca nello stesso compiled pipeline (serve per Jump).
        /// - Run: azione che muta il contesto.
        ///
        /// Esempio:
        /// <code>
        /// new Step&lt;DepositCtx&gt;(DepositHook.IdempotencyLookup, Deposit_IdempotencyLookup)
        /// </code>
        /// </summary>
        protected readonly struct Step<TCtx>
        {
            public readonly Enum Hook;
            public readonly string Key;
            public readonly Action<TCtx> Run;

            public Step(Enum hook, Action<TCtx> run, string key = null)
            {
                Hook = hook ?? throw new ArgumentNullException(nameof(hook));
                Key = string.IsNullOrWhiteSpace(key) ? hook.ToString() : key;
                Run = run ?? throw new ArgumentNullException(nameof(run));
            }
        }

        /// <summary>
        /// Pipeline compilata.
        ///
        /// IndexByKey è costruito una volta per permettere Jump O(1).
        /// </summary>
        protected readonly struct CompiledSteps<TCtx>
        {
            public readonly Step<TCtx>[] Steps;
            public readonly Dictionary<string, int> IndexByKey;

            public CompiledSteps(Step<TCtx>[] steps)
            {
                Steps = steps ?? throw new ArgumentNullException(nameof(steps));
                var map = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < Steps.Length; i++)
                {
                    var k = Steps[i].Key;
                    if (string.IsNullOrWhiteSpace(k))
                        throw new InvalidOperationException($"Step[{i}] has empty Key.");

                    if (map.ContainsKey(k))
                        throw new InvalidOperationException($"Duplicate step Key '{k}'. Keys must be unique.");

                    map[k] = i;
                }
                IndexByKey = map;
            }
        }

        /// <summary>
        /// Esegue i passi in ordine.
        ///
        /// Supporta branching via ctx.JumpToKey:
        /// - ogni step può impostare ctx.JumpToKey = "SomeStepKey".
        /// - l'esecuzione salta a quello step (se presente nel compiled pipeline).
        ///
        /// Esempio idempotency:
        /// - durante IdempotencyLookup trovi un movimento esistente -> JumpToKey = DepositHook.Resend
        /// - la pipeline salta subito allo step Resend e poi termina.
        /// </summary>
        protected static void RunSteps<TCtx>(CompiledSteps<TCtx> compiled, TCtx ctx, Func<TCtx, bool> shouldStop)
            where TCtx : IBaseCtx
        {
            var steps = compiled.Steps;

            for (int i = 0; i < steps.Length; i++)
            {
                if (shouldStop != null && shouldStop(ctx))
                    break;

                steps[i].Run(ctx);

                if (shouldStop != null && shouldStop(ctx))
                    break;

                // Jump
                string jump = ctx.JumpToKey;
                if (!string.IsNullOrEmpty(jump))
                {
                    ctx.JumpToKey = null; // consume
                    if (!compiled.IndexByKey.TryGetValue(jump, out var target))
                        throw new InvalidOperationException($"JumpToKey '{jump}' not found in compiled pipeline.");
                    i = target - 1; // perché poi il for farà i++
                }
            }
        }

        // =====================================================================
        //  Pipeline modifiers
        // =====================================================================

        /// <summary>
        /// Inserisce uno step subito dopo un hook esistente.
        ///
        /// Esempio: aggiungere un hook provider-specific dopo la validazione:
        /// <code>
        /// InsertAfter(steps, DepositHook.RequestValidation,
        ///     new Step&lt;DepositCtx&gt;(MyHooks.ProviderPrecheck, Provider_Precheck));
        /// </code>
        /// </summary>
        protected static void InsertAfter<TCtx>(List<Step<TCtx>> steps, Enum hook, Step<TCtx> toInsert)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            if (hook == null) throw new ArgumentNullException(nameof(hook));

            string key = hook.ToString();
            int idx = steps.FindIndex(s => string.Equals(s.Key, key, StringComparison.Ordinal));
            if (idx < 0) throw new InvalidOperationException($"Hook '{hook}' (Key '{key}') not found");
            steps.Insert(idx + 1, toInsert);
        }

        /// <summary>
        /// Sostituisce uno step esistente.
        ///
        /// Esempio: rimpiazzare l'idempotency di base con una custom:
        /// <code>
        /// Replace(steps, DepositHook.IdempotencyLookup,
        ///     new Step&lt;DepositCtx&gt;(DepositHook.IdempotencyLookup, My_Idempotency));
        /// </code>
        /// </summary>
        protected static void Replace<TCtx>(List<Step<TCtx>> steps, Enum hook, Step<TCtx> replacement)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            if (hook == null) throw new ArgumentNullException(nameof(hook));

            string key = hook.ToString();
            int idx = steps.FindIndex(s => string.Equals(s.Key, key, StringComparison.Ordinal));
            if (idx < 0) throw new InvalidOperationException($"Hook '{hook}' (Key '{key}') not found");
            steps[idx] = replacement;
        }

        // =====================================================================
        //  Contexts
        // =====================================================================

        /// <summary>
        /// Interfaccia minima necessaria per:
        /// - Jump (JumpToKey)
        /// - Stop (Stop)
        ///
        /// Tutto il resto è specifico del tipo di pipeline.
        /// </summary>
        public interface IBaseCtx
        {
            string JumpToKey { get; set; }
            bool Stop { get; set; }
        }

        /// <summary>
        /// Holder "leggero" dei balances/counters sessione.
        ///
        /// Nota: viene popolato da hook dedicati (es. BalanceCheck), così non carichi tutto subito.
        /// </summary>
        public sealed class SessionBalancesHolder
        {
            public long amountTotal;
            public long bonus;
            public int funBonus;
            public int lastProgr;
            public int lastRemFr;
            public long maxId;
            public string currentRound = string.Empty; // empty = no round or round closed
            public long amountTotalWin;
            public long accountBalance;
        }

    }
}
