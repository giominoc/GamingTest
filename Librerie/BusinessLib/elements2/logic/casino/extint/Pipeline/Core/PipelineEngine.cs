using System;
using System.Collections.Generic;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core
{
    /// <summary>
    /// Core engine per eseguire una pipeline di componenti.
    /// Architettura: COMPOSITION (lista di componenti eseguibili), NON nested (virtual method chain).
    /// </summary>
    public static class PipelineEngine
    {
        /// <summary>
        /// Esegue i componenti in ordine sequenziale.
        /// Supporta:
        /// - Stop condizionale tramite shouldStop predicate
        /// - Branching tramite ctx.JumpToKey (salta a un componente specifico)
        /// </summary>
        public static void Run<TCtx>(PipelineComponent<TCtx>[] components, TCtx ctx, Func<TCtx, bool> shouldStop)
            where TCtx : IPipelineContext
        {
            if (components == null) throw new ArgumentNullException(nameof(components));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            // Crea index per Jump O(1)
            var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < components.Length; i++)
            {
                var key = components[i].Key;
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException($"Component[{i}] has empty Key.");
                
                if (indexByKey.ContainsKey(key))
                    throw new InvalidOperationException($"Duplicate component Key '{key}'. Keys must be unique.");
                
                indexByKey[key] = i;
            }

            // Esegui componenti in sequenza
            for (int i = 0; i < components.Length; i++)
            {
                // Check stop prima dell'esecuzione
                if (shouldStop != null && shouldStop(ctx))
                    break;

                // Esegui componente
                components[i].Execute(ctx);

                // Check stop dopo l'esecuzione
                if (shouldStop != null && shouldStop(ctx))
                    break;

                // Gestisci Jump
                string jumpKey = ctx.JumpToKey;
                if (!string.IsNullOrEmpty(jumpKey))
                {
                    ctx.JumpToKey = null; // consume
                    if (!indexByKey.TryGetValue(jumpKey, out var targetIndex))
                        throw new InvalidOperationException($"JumpToKey '{jumpKey}' not found in pipeline.");
                    i = targetIndex - 1; // -1 perché il for farà i++
                }
            }
        }
    }
}
