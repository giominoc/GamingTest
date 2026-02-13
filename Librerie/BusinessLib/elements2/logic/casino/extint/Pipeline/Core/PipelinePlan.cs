using System;
using System.Collections.Generic;
using System.Linq;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core
{
    /// <summary>
    /// Rappresenta un piano di pipeline: una lista modificabile di componenti.
    /// Il piano può essere customizzato prima di essere compilato in un array eseguibile.
    /// </summary>
    public class PipelinePlan<TCtx> where TCtx : IPipelineContext
    {
        private readonly List<PipelineComponent<TCtx>> _components;

        public PipelinePlan(List<PipelineComponent<TCtx>> components = null)
        {
            _components = components ?? new List<PipelineComponent<TCtx>>();
        }

        /// <summary>
        /// Aggiunge un componente alla fine del piano.
        /// </summary>
        public void Add(PipelineComponent<TCtx> component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _components.Add(component);
        }

        /// <summary>
        /// Sostituisce un componente esistente identificato dalla chiave.
        /// </summary>
        public void Replace(string key, PipelineComponent<TCtx> replacement)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));

            int index = _components.FindIndex(c => c.Key == key);
            if (index < 0)
                throw new InvalidOperationException($"Component with key '{key}' not found.");

            _components[index] = replacement;
        }

        /// <summary>
        /// Inserisce un componente subito dopo un componente esistente identificato dalla chiave.
        /// </summary>
        public void InsertAfter(string key, PipelineComponent<TCtx> component)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (component == null) throw new ArgumentNullException(nameof(component));

            int index = _components.FindIndex(c => c.Key == key);
            if (index < 0)
                throw new InvalidOperationException($"Component with key '{key}' not found.");

            _components.Insert(index + 1, component);
        }

        /// <summary>
        /// Inserisce un componente subito prima di un componente esistente identificato dalla chiave.
        /// </summary>
        public void InsertBefore(string key, PipelineComponent<TCtx> component)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (component == null) throw new ArgumentNullException(nameof(component));

            int index = _components.FindIndex(c => c.Key == key);
            if (index < 0)
                throw new InvalidOperationException($"Component with key '{key}' not found.");

            _components.Insert(index, component);
        }

        /// <summary>
        /// Rimuove un componente identificato dalla chiave.
        /// </summary>
        public void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

            int index = _components.FindIndex(c => c.Key == key);
            if (index < 0)
                throw new InvalidOperationException($"Component with key '{key}' not found.");

            _components.RemoveAt(index);
        }

        /// <summary>
        /// Wrappa un componente esistente con logica aggiuntiva (es. logging, retry, telemetry).
        /// </summary>
        public void Wrap(string key, Func<Action<TCtx>, Action<TCtx>> wrapper, string newDescription = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (wrapper == null) throw new ArgumentNullException(nameof(wrapper));

            int index = _components.FindIndex(c => c.Key == key);
            if (index < 0)
                throw new InvalidOperationException($"Component with key '{key}' not found.");

            var original = _components[index];
            var wrappedAction = wrapper(original.Execute);
            var description = newDescription ?? $"Wrapped({original.Description})";

            _components[index] = new PipelineComponent<TCtx>(original.Key, wrappedAction, description);
        }

        /// <summary>
        /// Compila il piano in un array immutabile pronto per l'esecuzione.
        /// </summary>
        public PipelineComponent<TCtx>[] Compile()
        {
            return _components.ToArray();
        }

        /// <summary>
        /// Restituisce le chiavi di tutti i componenti nel piano (utile per diagnostica).
        /// </summary>
        public string[] GetComponentKeys()
        {
            return _components.Select(c => c.Key).ToArray();
        }

        /// <summary>
        /// Verifica se esiste un componente con la chiave specificata.
        /// </summary>
        public bool Contains(string key)
        {
            return _components.Any(c => c.Key == key);
        }
    }
}
