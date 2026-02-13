using NUnit.Framework;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Bet;
using it.capecod.util;
using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.casino;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GamingTests.Tests.Pipeline
{
    /// <summary>
    /// Test suite per la pipeline di Bet.
    /// 
    /// Obiettivi:
    /// - Verificare l'ordine e l'esecuzione degli step tramite Trace
    /// - Verificare il comportamento di jump/resend path
    /// - Essere predisposti per integrazione reale (configurazione, toggle per test E2E)
    /// </summary>
    [TestFixture]
    public class BetPipelineTests
    {
        private TestBetPipeline _pipeline;
        private List<string> _executionTrace;

        [SetUp]
        public void SetUp()
        {
            _executionTrace = new List<string>();
            _pipeline = new TestBetPipeline(_executionTrace);
        }

        [Test]
        [Description("Verifica che tutti gli step della pipeline siano eseguiti nell'ordine corretto")]
        public void BetPipeline_ExecutesAllSteps_InCorrectOrder()
        {
            // Arrange
            var auxPars = new HashParams(
                "author", "test",
                "rawRequest", "{test: 'request'}",
                "transactionId", "TX123",
                "ticket", "TK456",
                "amount", 100L
            );

            // Act
            var result = _pipeline.ExecuteBetPipeline(1, auxPars);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(_executionTrace, Has.Count.GreaterThan(0));
            
            // Verifica l'ordine degli step
            var expectedOrder = new[]
            {
                "ResponseDefinition",
                "ContextBaseGeneration",
                "RequestValidation",
                "IdempotencyLookup",
                "LoadSession",
                "BalanceCheck",
                "CreateMovement",
                "PersistMovementCreate",
                "ExecuteExternalTransfer",
                "PersistMovementFinalize",
                "BuildResponse"
            };

            for (int i = 0; i < expectedOrder.Length; i++)
            {
                Assert.That(_executionTrace[i], Is.EqualTo(expectedOrder[i]), 
                    $"Step {i} should be {expectedOrder[i]} but was {_executionTrace[i]}");
            }
        }

        [Test]
        [Description("Verifica che il jump idempotency salti correttamente allo step Resend")]
        public void BetPipeline_WithIdempotency_JumpsToResend()
        {
            // Arrange
            _pipeline.SimulateIdempotency = true;
            var auxPars = new HashParams(
                "author", "test",
                "transactionId", "TX123",
                "ticket", "TK456"
            );

            // Act
            var result = _pipeline.ExecuteBetPipeline(1, auxPars);

            // Assert
            Assert.IsNotNull(result);
            
            // Verifica che dopo IdempotencyLookup ci sia Resend
            var idempotencyIndex = _executionTrace.IndexOf("IdempotencyLookup");
            var resendIndex = _executionTrace.IndexOf("Resend");
            
            Assert.That(idempotencyIndex, Is.GreaterThanOrEqualTo(0), "IdempotencyLookup should be executed");
            Assert.That(resendIndex, Is.GreaterThan(idempotencyIndex), "Resend should be executed after IdempotencyLookup");
            
            // Verifica che gli step successivi non siano eseguiti dopo Resend
            Assert.That(_executionTrace, Has.No.Member("PersistMovementCreate"), 
                "PersistMovementCreate should not be executed in idempotency path");
        }

        [Test]
        [Description("Verifica che uno stop anticipato blocchi l'esecuzione")]
        public void BetPipeline_WithEarlyStop_StopsExecution()
        {
            // Arrange
            _pipeline.StopAtValidation = true;
            var auxPars = new HashParams(
                "author", "test",
                "transactionId", "TX123"
            );

            // Act
            var result = _pipeline.ExecuteBetPipeline(1, auxPars);

            // Assert
            Assert.IsNotNull(result);
            
            // Verifica che gli step dopo validation non siano eseguiti
            Assert.That(_executionTrace, Has.Member("RequestValidation"), "RequestValidation should be executed");
            Assert.That(_executionTrace, Has.No.Member("IdempotencyLookup"), 
                "IdempotencyLookup should not be executed after early stop");
        }

        [Test]
        [Description("Verifica la response in caso di successo")]
        public void BetPipeline_SuccessfulExecution_ReturnsCorrectResponse()
        {
            // Arrange
            var auxPars = new HashParams(
                "author", "test",
                "transactionId", "TX123",
                "amount", 100L
            );

            // Act
            var result = _pipeline.ExecuteBetPipeline(1, auxPars);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result["responseCodeReason"], Is.EqualTo("200"));
            Assert.That(result.ContainsKey("balance"), Is.True);
            Assert.That(result.ContainsKey("casinoTransferId"), Is.True);
        }

        [Test]
        [Description("Verifica la response in caso di errore di validazione")]
        public void BetPipeline_InvalidRequest_ReturnsErrorResponse()
        {
            // Arrange
            _pipeline.InvalidRequest = true;
            var auxPars = new HashParams(
                "author", "test"
                // transactionId mancante
            );

            // Act
            var result = _pipeline.ExecuteBetPipeline(1, auxPars);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result["responseCodeReason"], Is.Not.EqualTo("200"));
            Assert.That(result.ContainsKey("errorMessage"), Is.True);
        }

        /// <summary>
        /// Implementazione di test della pipeline Bet.
        /// Traccia l'esecuzione di ogni step e consente simulazioni.
        /// </summary>
        private class TestBetPipeline : CasinoExtIntBetPipeline
        {
            private readonly List<string> _trace;
            public bool SimulateIdempotency { get; set; }
            public bool StopAtValidation { get; set; }
            public bool InvalidRequest { get; set; }

            public TestBetPipeline(List<string> trace)
            {
                _trace = trace;
            }

            public Hashtable ExecuteBetPipeline(int euId, HashParams auxPars)
            {
                var compiled = BuildBetPipeline();
                return RunBetPipeline(euId, auxPars, compiled);
            }

            protected override void Bet_ResponseDefinition(BetCtx ctx)
            {
                _trace.Add("ResponseDefinition");
                base.Bet_ResponseDefinition(ctx);
            }

            protected override void Bet_RequestValidation(BetCtx ctx)
            {
                _trace.Add("RequestValidation");
                
                if (InvalidRequest)
                {
                    ctx.TargetStatus = "409";
                    ctx.Response["responseCodeReason"] = "409";
                    ctx.Response["errorMessage"] = "INVALID_REQUEST";
                    ctx.Stop = true;
                    return;
                }

                // Validazione base
                ctx.CmbType = CasinoMovimentiBuffer.Type.Loose;
                ctx.TargetState = CasinoMovimentiBuffer.States.Dumped;
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;

                if (StopAtValidation)
                {
                    ctx.Stop = true;
                }
            }

            protected override void Bet_LoadSession(BetCtx ctx)
            {
                _trace.Add("LoadSession");
                
                // Simula sessione valida
                ctx.SessionInfos = new HashParams(
                    "MATCH_Id", 123,
                    "centEU_Id", 1
                );
                ctx.CurrMatch = new CasinoSubscription { MATCH_Id = 123 };
            }

            protected override void Bet_IdempotencyLookup(BetCtx ctx)
            {
                _trace.Add("IdempotencyLookup");

                if (SimulateIdempotency)
                {
                    // Simula movimento esistente
                    ctx.IdempotencyMov = new CasinoMovimentiBuffer
                    {
                        CMB_ID = 999,
                        CMB_RESULT = "200",
                        CMB_SENTTEXT = "{\"responseCodeReason\":\"200\",\"balance\":1000,\"casinoTransferId\":\"999\"}"
                    };
                    ctx.JumpToKey = BetHook.Resend.ToString();
                }
            }

            protected override void Bet_BalanceCheck(BetCtx ctx)
            {
                _trace.Add("BalanceCheck");
                base.Bet_BalanceCheck(ctx);
            }

            protected override void Bet_CreateMovement(BetCtx ctx)
            {
                _trace.Add("CreateMovement");
                base.Bet_CreateMovement(ctx);
            }

            protected override void Bet_PersistMovementCreate(BetCtx ctx)
            {
                _trace.Add("PersistMovementCreate");
                
                // Mock persistence - non salvare realmente
                if (ctx.NewMov != null)
                {
                    ctx.NewMov.CMB_ID = 123; // Simula ID generato
                }
            }

            protected override void Bet_ExecuteExternalTransfer(BetCtx ctx)
            {
                _trace.Add("ExecuteExternalTransfer");
                
                // Simula trasferimento riuscito
                ctx.TargetStatus = "200";
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
                ctx.Response["balance"] = 1000L;
            }

            protected override void Bet_PersistMovementFinalize(BetCtx ctx)
            {
                _trace.Add("PersistMovementFinalize");
                // Mock persistence
            }

            protected override void Bet_BuildResponse(BetCtx ctx)
            {
                _trace.Add("BuildResponse");
                base.Bet_BuildResponse(ctx);
            }

            protected override void Bet_Resend(BetCtx ctx)
            {
                _trace.Add("Resend");
                base.Bet_Resend(ctx);
            }
        }
    }
}
