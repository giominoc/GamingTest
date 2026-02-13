using NUnit.Framework;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Win;
using it.capecod.util;
using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.casino;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GamingTests.Tests.Pipeline
{
    /// <summary>
    /// Test suite per la pipeline di Win (Credit/Deposit).
    /// </summary>
    [TestFixture]
    public class WinPipelineTests
    {
        private TestWinPipeline _pipeline;
        private List<string> _executionTrace;

        [SetUp]
        public void SetUp()
        {
            _executionTrace = new List<string>();
            _pipeline = new TestWinPipeline(_executionTrace);
        }

        [Test]
        [Description("Verifica che tutti gli step della pipeline siano eseguiti nell'ordine corretto")]
        public void WinPipeline_ExecutesAllSteps_InCorrectOrder()
        {
            // Arrange
            var auxPars = new HashParams(
                "author", "test",
                "rawRequest", "{test: 'request'}",
                "transactionId", "TX123",
                "ticket", "TK456",
                "amount", 200L
            );

            // Act
            var result = _pipeline.ExecuteWinPipeline(1, auxPars);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(_executionTrace, Has.Count.GreaterThan(0));
            
            var expectedOrder = new[]
            {
                "ResponseDefinition",
                "ContextBaseGeneration",
                "RequestValidation",
                "IdempotencyLookup",
                "LoadSession",
                "CreateMovement",
                "PersistMovementCreate",
                "ExecuteExternalTransfer",
                "PersistMovementFinalize",
                "BuildResponse"
            };

            for (int i = 0; i < expectedOrder.Length; i++)
            {
                Assert.That(_executionTrace[i], Is.EqualTo(expectedOrder[i]));
            }
        }

        [Test]
        [Description("Verifica che il jump idempotency salti correttamente allo step Resend")]
        public void WinPipeline_WithIdempotency_JumpsToResend()
        {
            // Arrange
            _pipeline.SimulateIdempotency = true;
            var auxPars = new HashParams(
                "author", "test",
                "transactionId", "TX123"
            );

            // Act
            var result = _pipeline.ExecuteWinPipeline(1, auxPars);

            // Assert
            var idempotencyIndex = _executionTrace.IndexOf("IdempotencyLookup");
            var resendIndex = _executionTrace.IndexOf("Resend");
            
            Assert.That(resendIndex, Is.GreaterThan(idempotencyIndex));
            Assert.That(_executionTrace, Has.No.Member("PersistMovementCreate"));
        }

        [Test]
        [Description("Verifica la response in caso di successo")]
        public void WinPipeline_SuccessfulExecution_ReturnsCorrectResponse()
        {
            // Arrange
            var auxPars = new HashParams(
                "author", "test",
                "transactionId", "TX123",
                "amount", 200L
            );

            // Act
            var result = _pipeline.ExecuteWinPipeline(1, auxPars);

            // Assert
            Assert.That(result["responseCodeReason"], Is.EqualTo("200"));
            Assert.That(result.ContainsKey("balance"), Is.True);
            Assert.That(result.ContainsKey("casinoTransferId"), Is.True);
        }

        private class TestWinPipeline : CasinoExtIntWinPipeline
        {
            private readonly List<string> _trace;
            public bool SimulateIdempotency { get; set; }

            public TestWinPipeline(List<string> trace)
            {
                _trace = trace;
            }

            public Hashtable ExecuteWinPipeline(int euId, HashParams auxPars)
            {
                var compiled = BuildWinPipeline();
                return RunWinPipeline(euId, auxPars, compiled);
            }

            protected override void Win_ResponseDefinition(WinCtx ctx)
            {
                _trace.Add("ResponseDefinition");
                base.Win_ResponseDefinition(ctx);
            }

            protected override void Win_RequestValidation(WinCtx ctx)
            {
                _trace.Add("RequestValidation");
                ctx.CmbType = CasinoMovimentiBuffer.Type.Win;
                ctx.TargetState = CasinoMovimentiBuffer.States.Dumped;
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
            }

            protected override void Win_LoadSession(WinCtx ctx)
            {
                _trace.Add("LoadSession");
                ctx.SessionInfos = new HashParams("MATCH_Id", 123);
                ctx.CurrMatch = new CasinoSubscription { MATCH_Id = 123 };
            }

            protected override void Win_IdempotencyLookup(WinCtx ctx)
            {
                _trace.Add("IdempotencyLookup");
                if (SimulateIdempotency)
                {
                    ctx.IdempotencyMov = new CasinoMovimentiBuffer
                    {
                        CMB_ID = 999,
                        CMB_RESULT = "200",
                        CMB_SENTTEXT = "{\"responseCodeReason\":\"200\",\"balance\":1200,\"casinoTransferId\":\"999\"}"
                    };
                    ctx.JumpToKey = WinHook.Resend.ToString();
                }
            }

            protected override void Win_CreateMovement(WinCtx ctx)
            {
                _trace.Add("CreateMovement");
                base.Win_CreateMovement(ctx);
            }

            protected override void Win_PersistMovementCreate(WinCtx ctx)
            {
                _trace.Add("PersistMovementCreate");
                if (ctx.NewMov != null)
                    ctx.NewMov.CMB_ID = 124;
            }

            protected override void Win_ExecuteExternalTransfer(WinCtx ctx)
            {
                _trace.Add("ExecuteExternalTransfer");
                ctx.TargetStatus = "200";
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
                ctx.Response["balance"] = 1200L;
            }

            protected override void Win_PersistMovementFinalize(WinCtx ctx)
            {
                _trace.Add("PersistMovementFinalize");
            }

            protected override void Win_BuildResponse(WinCtx ctx)
            {
                _trace.Add("BuildResponse");
                base.Win_BuildResponse(ctx);
            }

            protected override void Win_Resend(WinCtx ctx)
            {
                _trace.Add("Resend");
                base.Win_Resend(ctx);
            }
        }
    }
}
