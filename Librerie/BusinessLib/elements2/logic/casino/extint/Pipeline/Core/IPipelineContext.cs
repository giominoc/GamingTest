namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core
{
    /// <summary>
    /// Interfaccia base per tutti i contesti di pipeline.
    /// Definisce le funzionalità minime necessarie per il controllo del flusso.
    /// </summary>
    public interface IPipelineContext
    {
        /// <summary>
        /// Chiave del componente a cui saltare.
        /// Quando impostato, il PipelineEngine salterà a quel componente.
        /// Viene automaticamente consumato (resettato a null) dopo il salto.
        /// </summary>
        string JumpToKey { get; set; }

        /// <summary>
        /// Flag per fermare l'esecuzione della pipeline.
        /// Quando true, il PipelineEngine interromperà l'esecuzione.
        /// </summary>
        bool Stop { get; set; }
    }
}
