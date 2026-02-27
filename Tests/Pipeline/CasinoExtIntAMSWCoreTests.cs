using NUnit.Framework;
using GamingTest.BusinessLib.elements2.logic.casino.extint.am;
using it.capecod.util;
using System;
using System.Collections;

namespace GamingTests.Tests.Pipeline
{
    /// <summary>
    /// Test suite for CasinoExtIntAMSWCore implementation.
    /// Validates integration with pipeline architecture and dispatcher requirements.
    /// </summary>
    [TestFixture]
    public class CasinoExtIntAMSWCoreTests
    {
        private CasinoExtIntAMSWCore _core;

        [SetUp]
        public void SetUp()
        {
            _core = new CasinoExtIntAMSWCore();
        }

        #region Wallet Operation Tests

        [Test]
        [Description("ExecuteBet calls Bet pipeline and returns result")]
        public void ExecuteBet_CallsPipeline_ReturnsResult()
        {
            // Arrange
            var auxPars = new HashParams(
                "transactionId", "TX123",
                "amount", 100L,
                "ticket", "TK456"
            );

            // Act
            var result = _core.ExecuteBet(1, auxPars);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<Hashtable>());
            Assert.That(result.ContainsKey("responseCodeReason"), Is.True);
        }

        [Test]
        [Description("ExecuteWin calls Win pipeline and returns result")]
        public void ExecuteWin_CallsPipeline_ReturnsResult()
        {
            // Arrange
            var auxPars = new HashParams(
                "transactionId", "TX456",
                "amount", 200L,
                "ticket", "TK789"
            );

            // Act
            var result = _core.ExecuteWin(1, auxPars);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<Hashtable>());
            Assert.That(result.ContainsKey("responseCodeReason"), Is.True);
        }

        [Test]
        [Description("ExecuteCancel calls Cancel pipeline and returns result")]
        public void ExecuteCancel_CallsPipeline_ReturnsResult()
        {
            // Arrange
            var auxPars = new HashParams(
                "transactionId", "TX789",
                "roundRef", "ROUND123",
                "ticket", "TK111"
            );

            // Act
            var result = _core.ExecuteCancel(1, auxPars);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<Hashtable>());
            Assert.That(result.ContainsKey("responseCodeReason"), Is.True);
        }

        #endregion

        #region Dispatcher Integration Tests

        [Test]
        [Description("getAuxInfos routes 'bet' method to Bet pipeline")]
        public void GetAuxInfos_BetMethod_RoutesBetPipeline()
        {
            // Arrange
            var callPars = new HashParams(
                "transactionId", "TX123",
                "amount", 100L
            );
            var auxPar = new HashParams(
                "euId", 1,
                "callPars", callPars
            );

            // Act
            var result = _core.getAuxInfos("bet", auxPar);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.ContainsKey("MSGRESULT"), Is.True);
        }

        [Test]
        [Description("getAuxInfos routes 'win' method to Win pipeline")]
        public void GetAuxInfos_WinMethod_RoutesWinPipeline()
        {
            // Arrange
            var callPars = new HashParams(
                "transactionId", "TX456",
                "amount", 200L
            );
            var auxPar = new HashParams(
                "euId", 1,
                "callPars", callPars
            );

            // Act
            var result = _core.getAuxInfos("win", auxPar);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.ContainsKey("MSGRESULT"), Is.True);
        }

        [Test]
        [Description("getAuxInfos routes 'cancel' method to Cancel pipeline")]
        public void GetAuxInfos_CancelMethod_RoutesCancelPipeline()
        {
            // Arrange
            var callPars = new HashParams(
                "transactionId", "TX789",
                "roundRef", "ROUND123"
            );
            var auxPar = new HashParams(
                "euId", 1,
                "callPars", callPars
            );

            // Act
            var result = _core.getAuxInfos("cancel", auxPar);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.ContainsKey("MSGRESULT"), Is.True);
        }

        [Test]
        [Description("getAuxInfos handles unknown method")]
        public void GetAuxInfos_UnknownMethod_ReturnsError()
        {
            // Arrange
            var auxPar = new HashParams(
                "euId", 1,
                "callPars", new HashParams()
            );

            // Act
            var result = _core.getAuxInfos("unknown", auxPar);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsOk, Is.False);
        }

        [Test]
        [Description("getAuxInfos accepts alternative method names")]
        public void GetAuxInfos_AlternativeMethodNames_WorkCorrectly()
        {
            // Arrange
            var callPars = new HashParams("transactionId", "TX123");
            var auxPar = new HashParams(
                "euId", 1,
                "callPars", callPars
            );

            // Act & Assert - test withdraw alias for bet
            var withdrawResult = _core.getAuxInfos("withdraw", auxPar);
            Assert.That(withdrawResult.IsOk, Is.True);

            // test deposit alias for win
            var depositResult = _core.getAuxInfos("deposit", auxPar);
            Assert.That(depositResult.IsOk, Is.True);

            // test rollback alias for cancel
            var rollbackResult = _core.getAuxInfos("rollback", auxPar);
            Assert.That(rollbackResult.IsOk, Is.True);
        }

        #endregion

        #region Diagnostics Tests

        [Test]
        [Description("GetBetDiagnostics returns diagnostic information")]
        public void GetBetDiagnostics_ReturnsInformation()
        {
            // Act
            var diagnostics = _core.GetBetDiagnostics();

            // Assert
            Assert.That(diagnostics, Is.Not.Null);
            Assert.That(diagnostics, Is.Not.Empty);
            Assert.That(diagnostics, Does.Contain("Bet"));
        }

        [Test]
        [Description("GetWinDiagnostics returns diagnostic information")]
        public void GetWinDiagnostics_ReturnsInformation()
        {
            // Act
            var diagnostics = _core.GetWinDiagnostics();

            // Assert
            Assert.That(diagnostics, Is.Not.Null);
            Assert.That(diagnostics, Is.Not.Empty);
            Assert.That(diagnostics, Does.Contain("Win"));
        }

        [Test]
        [Description("GetCancelDiagnostics returns diagnostic information")]
        public void GetCancelDiagnostics_ReturnsInformation()
        {
            // Act
            var diagnostics = _core.GetCancelDiagnostics();

            // Assert
            Assert.That(diagnostics, Is.Not.Null);
            Assert.That(diagnostics, Is.Not.Empty);
            Assert.That(diagnostics, Does.Contain("Cancel"));
        }

        #endregion

        #region Singleton Tests

        [Test]
        [Description("Singleton instance is created properly")]
        public void Singleton_CreatesInstance()
        {
            // Act
            var instance = CasinoExtIntAMSWCore.def;

            // Assert
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance, Is.InstanceOf<CasinoExtIntAMSWCore>());
        }

        [Test]
        [Description("Singleton returns same instance")]
        public void Singleton_ReturnsSameInstance()
        {
            // Act
            var instance1 = CasinoExtIntAMSWCore.def;
            var instance2 = CasinoExtIntAMSWCore.def;

            // Assert
            Assert.That(instance1, Is.SameAs(instance2));
        }

        #endregion
    }
}
