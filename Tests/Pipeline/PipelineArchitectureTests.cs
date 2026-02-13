using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Core;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Win;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Bet;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Cancel;
using System;

namespace GamingTests.Tests.Pipeline
{
    /// <summary>
    /// Test della nuova architettura pipeline composition.
    /// Verifica che la struttura sia corretta e che la diagnostica funzioni.
    /// </summary>
    public class PipelineArchitectureTests
    {
        /// <summary>
        /// Test: verifica che la pipeline standard Win sia creata correttamente.
        /// </summary>
        public static void TestWinStandardPipeline()
        {
            var pipeline = WinPipelineFactory.GetStandardPipeline();
            
            Console.WriteLine($"Win Standard Pipeline: {pipeline.Length} components");
            
            var keys = PipelineDiagnostics.GetKeys(pipeline);
            Console.WriteLine("Keys: " + string.Join(", ", keys));
            
            // Verifica che ci siano almeno i componenti base
            if (pipeline.Length < 10)
                throw new Exception($"Win pipeline too short: {pipeline.Length} components");
                
            Console.WriteLine("✓ Win standard pipeline OK");
        }

        /// <summary>
        /// Test: verifica che la pipeline Bet sia creata correttamente.
        /// </summary>
        public static void TestBetStandardPipeline()
        {
            var pipeline = BetPipelineFactory.GetStandardPipeline();
            
            Console.WriteLine($"Bet Standard Pipeline: {pipeline.Length} components");
            
            var keys = PipelineDiagnostics.GetKeys(pipeline);
            Console.WriteLine("Keys: " + string.Join(", ", keys));
            
            // Verifica che ci siano almeno i componenti base
            if (pipeline.Length < 10)
                throw new Exception($"Bet pipeline too short: {pipeline.Length} components");
                
            Console.WriteLine("✓ Bet standard pipeline OK");
        }

        /// <summary>
        /// Test: verifica che la pipeline Cancel sia creata correttamente.
        /// </summary>
        public static void TestCancelStandardPipeline()
        {
            var pipeline = CancelPipelineFactory.GetStandardPipeline();
            
            Console.WriteLine($"Cancel Standard Pipeline: {pipeline.Length} components");
            
            var keys = PipelineDiagnostics.GetKeys(pipeline);
            Console.WriteLine("Keys: " + string.Join(", ", keys));
            
            // Verifica che ci siano almeno i componenti base
            if (pipeline.Length < 10)
                throw new Exception($"Cancel pipeline too short: {pipeline.Length} components");
                
            Console.WriteLine("✓ Cancel standard pipeline OK");
        }

        /// <summary>
        /// Test: verifica che le customizzazioni CasinoAM siano applicate correttamente.
        /// </summary>
        public static void TestCasinoAMCustomizations()
        {
            var standardWin = WinPipelineFactory.GetStandardPipeline();
            var customizedWin = WinPipelineFactory.CreatePipeline("CasinoAM");
            
            Console.WriteLine($"Standard: {standardWin.Length} components");
            Console.WriteLine($"Customized: {customizedWin.Length} components");
            
            // Le customizzazioni CasinoAM sostituiscono componenti ma non ne aggiungono/rimuovono
            if (standardWin.Length != customizedWin.Length)
                throw new Exception($"Unexpected component count change: {standardWin.Length} -> {customizedWin.Length}");
                
            Console.WriteLine("✓ CasinoAM customizations OK");
        }

        /// <summary>
        /// Test: verifica che la diagnostica funzioni.
        /// </summary>
        public static void TestDiagnostics()
        {
            var diagnostics = WinPipelineFactory.GetDiagnostics("CasinoAM");
            
            Console.WriteLine("=== Win Pipeline Diagnostics ===");
            Console.WriteLine(diagnostics);
            
            if (string.IsNullOrWhiteSpace(diagnostics))
                throw new Exception("Diagnostics output is empty");
                
            Console.WriteLine("✓ Diagnostics OK");
        }

        /// <summary>
        /// Test: verifica che il piano supporti le operazioni di customizzazione.
        /// </summary>
        public static void TestPlanOperations()
        {
            var plan = new PipelinePlan<WinContext>();
            
            // Add
            plan.Add(new PipelineComponent<WinContext>("Step1", ctx => { }, "Step 1"));
            plan.Add(new PipelineComponent<WinContext>("Step2", ctx => { }, "Step 2"));
            plan.Add(new PipelineComponent<WinContext>("Step3", ctx => { }, "Step 3"));
            
            if (!plan.Contains("Step2"))
                throw new Exception("Contains failed");
            
            // InsertAfter
            plan.InsertAfter("Step1", new PipelineComponent<WinContext>("Step1.5", ctx => { }, "Step 1.5"));
            
            // InsertBefore
            plan.InsertBefore("Step3", new PipelineComponent<WinContext>("Step2.5", ctx => { }, "Step 2.5"));
            
            // Replace
            plan.Replace("Step2", new PipelineComponent<WinContext>("Step2", ctx => { }, "Step 2 (replaced)"));
            
            var compiled = plan.Compile();
            
            if (compiled.Length != 5)
                throw new Exception($"Expected 5 components, got {compiled.Length}");
                
            var keys = PipelineDiagnostics.GetKeys(compiled);
            var expectedKeys = new[] { "Step1", "Step1.5", "Step2", "Step2.5", "Step3" };
            
            for (int i = 0; i < expectedKeys.Length; i++)
            {
                if (keys[i] != expectedKeys[i])
                    throw new Exception($"Key mismatch at {i}: expected {expectedKeys[i]}, got {keys[i]}");
            }
            
            Console.WriteLine("✓ Plan operations OK");
        }

        /// <summary>
        /// Esegue tutti i test.
        /// </summary>
        public static void RunAll()
        {
            Console.WriteLine("=== Pipeline Architecture Tests ===\n");
            
            TestWinStandardPipeline();
            Console.WriteLine();
            
            TestBetStandardPipeline();
            Console.WriteLine();
            
            TestCancelStandardPipeline();
            Console.WriteLine();
            
            TestCasinoAMCustomizations();
            Console.WriteLine();
            
            TestPlanOperations();
            Console.WriteLine();
            
            TestDiagnostics();
            Console.WriteLine();
            
            Console.WriteLine("=== All tests passed! ===");
        }
    }
}
