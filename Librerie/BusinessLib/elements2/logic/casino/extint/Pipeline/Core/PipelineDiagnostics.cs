using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core
{
    /// <summary>
    /// Utilità per diagnosticare e confrontare pipeline.
    /// Utile per audit, debug, e parity test.
    /// </summary>
    public static class PipelineDiagnostics
    {
        /// <summary>
        /// Crea un report testuale della pipeline compilata.
        /// </summary>
        public static string PrintPipeline<TCtx>(PipelineComponent<TCtx>[] components, string title = "Pipeline")
            where TCtx : IPipelineContext
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {title} ===");
            sb.AppendLine($"Total components: {components.Length}");
            sb.AppendLine();

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                sb.AppendLine($"{i + 1,3}. [{comp.Key}] {comp.Description}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Confronta due pipeline e mostra le differenze.
        /// Utile per vedere quali patch sono state applicate rispetto allo standard.
        /// </summary>
        public static string ComparePipelines<TCtx>(
            PipelineComponent<TCtx>[] standard,
            PipelineComponent<TCtx>[] customized,
            string standardLabel = "Standard",
            string customizedLabel = "Customized")
            where TCtx : IPipelineContext
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Pipeline Comparison: {standardLabel} vs {customizedLabel} ===");
            sb.AppendLine();

            var standardKeys = standard.Select(c => c.Key).ToList();
            var customizedKeys = customized.Select(c => c.Key).ToList();

            // Componenti aggiunti
            var added = customizedKeys.Except(standardKeys).ToList();
            if (added.Any())
            {
                sb.AppendLine("ADDED components:");
                foreach (var key in added)
                {
                    var comp = customized.First(c => c.Key == key);
                    sb.AppendLine($"  + [{key}] {comp.Description}");
                }
                sb.AppendLine();
            }

            // Componenti rimossi
            var removed = standardKeys.Except(customizedKeys).ToList();
            if (removed.Any())
            {
                sb.AppendLine("REMOVED components:");
                foreach (var key in removed)
                {
                    var comp = standard.First(c => c.Key == key);
                    sb.AppendLine($"  - [{key}] {comp.Description}");
                }
                sb.AppendLine();
            }

            // Ordine modificato
            var commonKeys = standardKeys.Intersect(customizedKeys).ToList();
            var orderChanged = false;
            for (int i = 0; i < commonKeys.Count; i++)
            {
                var stdIdx = standardKeys.IndexOf(commonKeys[i]);
                var custIdx = customizedKeys.IndexOf(commonKeys[i]);

                if (stdIdx != custIdx)
                {
                    if (!orderChanged)
                    {
                        sb.AppendLine("ORDER CHANGED:");
                        orderChanged = true;
                    }
                    sb.AppendLine($"  [{commonKeys[i]}] moved from position {stdIdx + 1} to {custIdx + 1}");
                }
            }

            if (added.Count == 0 && removed.Count == 0 && !orderChanged)
            {
                sb.AppendLine("No differences found. Pipelines are identical.");
            }

            sb.AppendLine();
            sb.AppendLine($"Standard: {standard.Length} components");
            sb.AppendLine($"Customized: {customized.Length} components");

            return sb.ToString();
        }

        /// <summary>
        /// Elenca tutte le chiavi di una pipeline (utile per testing).
        /// </summary>
        public static string[] GetKeys<TCtx>(PipelineComponent<TCtx>[] components)
            where TCtx : IPipelineContext
        {
            return components.Select(c => c.Key).ToArray();
        }

        /// <summary>
        /// Verifica che le chiavi siano uniche e non vuote.
        /// </summary>
        public static void ValidatePipeline<TCtx>(PipelineComponent<TCtx>[] components)
            where TCtx : IPipelineContext
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < components.Length; i++)
            {
                var key = components[i].Key;
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException($"Component at index {i} has empty key.");

                if (!keys.Add(key))
                    throw new InvalidOperationException($"Duplicate key '{key}' found at index {i}.");
            }
        }
    }
}
