using NUnit.Framework;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline;
using GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Cancel;
using it.capecod.util;
using it.capecod.gridgame.business.elements2;
using it.capecod.gridgame.business.elements2.logic.casino;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GamingTests.Tests.Pipeline
{
    /// <summary>
    /// Test suite per la pipeline di Cancel (RollBack).
    /// </summary>
    [TestFixture]
    public class CancelPipelineTests
    {
        private TestCancelPipeline _pipeline;
        private List<string> _executionTrace;

        [SetUp]
        public void SetUp()
        {
            _executionTrace = new List<string>();
            _pipeline = new TestCancelPipeline(_executionTrace);
        }

        [Test]
        [Description("Verifica che tutti gli step della pipeline siano eseguiti nell'ordine corretto")]
        public void CancelPipeline_ExecutesAllSteps_InCorrectOrder()
        {
            // Arrange
            var auxPars = new HashParams(
                "author", "test",
                "rawRequest", "{test: 'request'}",
                "transactionId", "TX_CANCEL_123",
                "roundRef", "ROUND_123",
                "ticket", "TK456"
            );

            // Act
            var result = _pipeline.ExecuteCancelPipeline(1, auxPars);

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
                "FindRelatedBet",
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
        [Description("Verifica che FindRelatedBet trovi la bet da cancellare")]
        public void CancelPipeline_FindRelatedBet_LinksToOriginalBet()
        {
            // Arrange
            _pipeline.SimulateRelatedBet = true;
            var auxPars = new HashParams(
                "author", "test",
                "transactionId", "TX_CANCEL_123",
                "roundRef", "ROUND_123"
            );

            // Act
            var result = _pipeline.ExecuteCancelPipeline(1, auxPars);

            // Assert
            Assert.That(_executionTrace, Has.Member("FindRelatedBet"));
            // Verifica che sia stato trovato il movimento correlato
            Assert.That(result.ContainsKey("casinoTransferId"), Is.True);
        }

        [Test]
        [Description("Verifica che il jump idempotency salti correttamente allo step Resend")]
        public void CancelPipeline_WithIdempotency_JumpsToResend()
        {
            // Arrange
            _pipeline.SimulateIdempotency = true;
            var auxPars = new HashParams(
                "author", "test",
                "transactionId", "TX123"
            );

            // Act
            var result = _pipeline.ExecuteCancelPipeline(1, auxPars);

            // Assert
            var idempotencyIndex = _executionTrace.IndexOf("IdempotencyLookup");
            var resendIndex = _executionTrace.IndexOf("Resend");
            
            Assert.That(resendIndex, Is.GreaterThan(idempotencyIndex));
            Assert.That(_executionTrace, Has.No.Member("PersistMovementCreate"));
        }

        [Test]
        [Description("Verifica la response in caso di successo")]
        public void CancelPipeline_SuccessfulExecution_ReturnsCorrectResponse()
        {
            // Arrange
            var auxPars = new HashParams(
                "author", "test",
                "transactionId", "TX_CANCEL_123",
                "roundRef", "ROUND_123"
            );

            // Act
            var result = _pipeline.ExecuteCancelPipeline(1, auxPars);

            // Assert
            Assert.That(result["responseCodeReason"], Is.EqualTo("200"));
            Assert.That(result.ContainsKey("balance"), Is.True);
            Assert.That(result.ContainsKey("casinoTransferId"), Is.True);
        }

        private class TestCancelPipeline : CasinoExtIntCancelPipeline
        {
            private readonly List<string> _trace;
            public bool SimulateIdempotency { get; set; }
            public bool SimulateRelatedBet { get; set; }

            public TestCancelPipeline(List<string> trace)
            {
                _trace = trace;
            }

            public Hashtable ExecuteCancelPipeline(int euId, HashParams auxPars)
            {
                var compiled = BuildCancelPipeline();
                return RunCancelPipeline(euId, auxPars, compiled);
            }

            protected override void Cancel_ResponseDefinition(CancelCtx ctx)
            {
                _trace.Add("ResponseDefinition");
                base.Cancel_ResponseDefinition(ctx);
            }

            protected override void Cancel_RequestValidation(CancelCtx ctx)
            {
                _trace.Add("RequestValidation");
                ctx.CmbType = CasinoMovimentiBuffer.Type.LooseCancel;
                ctx.TargetState = CasinoMovimentiBuffer.States.PreDumped;
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
            }

            protected override void Cancel_LoadSession(CancelCtx ctx)
            {
                _trace.Add("LoadSession");
                ctx.SessionInfos = new HashParams("MATCH_Id", 123);
                ctx.CurrMatch = new CasinoSubscription { MATCH_Id = 123 };
            }

            protected override void Cancel_IdempotencyLookup(CancelCtx ctx)
            {
                _trace.Add("IdempotencyLookup");
                if (SimulateIdempotency)
                {
                    ctx.IdempotencyMov = new CasinoMovimentiBuffer
                    {
                        CMB_ID = 999,
                        CMB_RESULT = "200",
                        CMB_SENTTEXT = "{\"responseCodeReason\":\"200\",\"balance\":1100,\"casinoTransferId\":\"999\"}"
                    };
                    ctx.JumpToKey = CancelHook.Resend.ToString();
                }
            }

            protected override void Cancel_FindRelatedBet(CancelCtx ctx)
            {
                _trace.Add("FindRelatedBet");
                if (SimulateRelatedBet)
                {
                    ctx.RelatedBetMov = new CasinoMovimentiBuffer
                    {
                        CMB_ID = 100,
                        CMB_EXTROUNDREF = ctx.RoundRef
                    };
                }
            }

            protected override void Cancel_CreateMovement(CancelCtx ctx)
            {
                _trace.Add("CreateMovement");
                base.Cancel_CreateMovement(ctx);
            }

            protected override void Cancel_PersistMovementCreate(CancelCtx ctx)
            {
                _trace.Add("PersistMovementCreate");
                if (ctx.NewMov != null)
                    ctx.NewMov.CMB_ID = 125;
            }

            protected override void Cancel_ExecuteExternalTransfer(CancelCtx ctx)
            {
                _trace.Add("ExecuteExternalTransfer");
                ctx.TargetStatus = "200";
                ctx.TargetStateFinal = CasinoMovimentiBuffer.States.Committed;
                ctx.Response["balance"] = 1100L;
            }

            protected override void Cancel_PersistMovementFinalize(CancelCtx ctx)
            {
                _trace.Add("PersistMovementFinalize");
            }

            protected override void Cancel_BuildResponse(CancelCtx ctx)
            {
                _trace.Add("BuildResponse");
                base.Cancel_BuildResponse(ctx);
            }

            protected override void Cancel_Resend(CancelCtx ctx)
            {
                _trace.Add("Resend");
                base.Cancel_Resend(ctx);
            }
        }
    }
}
