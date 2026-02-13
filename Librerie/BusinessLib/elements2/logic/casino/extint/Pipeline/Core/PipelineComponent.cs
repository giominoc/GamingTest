using System;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core
{
    /// <summary>
    /// Rappresenta un singolo componente eseguibile della pipeline.
    /// Un componente è atomico: ha una chiave stabile e un'azione da eseguire.
    /// </summary>
    public sealed class PipelineComponent<TCtx> where TCtx : IPipelineContext
    {
        /// <summary>
        /// Chiave logica stabile del componente.
        /// Utilizzata per: patch (replace/insert), parity test, diagnostica, jump.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Azione da eseguire sul contesto.
        /// </summary>
        public Action<TCtx> Execute { get; }

        /// <summary>
        /// Descrizione opzionale del componente (utile per diagnostica/logging).
        /// </summary>
        public string Description { get; }

        public PipelineComponent(string key, Action<TCtx> execute, string description = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));
            if (execute == null)
                throw new ArgumentNullException(nameof(execute));

            Key = key;
            Execute = execute;
            Description = description ?? key;
        }

        public override string ToString()
        {
            return $"[{Key}] {Description}";
        }
    }
}
