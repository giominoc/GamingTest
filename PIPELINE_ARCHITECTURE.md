# Pipeline Architecture for Casino External Integration

## Overview

This repository implements a pipeline-based architecture for handling Casino wallet operations (Bet, Win, Cancel). The architecture replaces the monolithic legacy methods with a modular, testable, and extensible design.

## Architecture Components

### Base Infrastructure

- **`CasinoExtIntPipelineBase`**: Base abstract class providing:
  - `Step<TCtx>`: Represents a single pipeline step with hook, key, and action
  - `CompiledSteps<TCtx>`: Compiled pipeline with indexed steps for O(1) jump operations
  - `RunSteps()`: Core engine for executing steps with support for jumps and early stops
  - Pipeline modifiers: `InsertAfter()`, `Replace()` for customization
  - `SessionBalancesHolder`: Helper for tracking session balances

### Pipeline Types

#### 1. Bet Pipeline (Debit/Withdraw)
**Location**: `Librerie/BusinessLib/elements2/logic/casino/extint/Pipeline/Bet/`

**Components**:
- `BetCtx`: Context object holding all bet-related state
- `BetHooks`: Abstract class defining 11 standard hooks + Resend
- `CasinoExtIntBetPipeline`: Build/Run macro for bet pipeline

**Steps**:
1. `ResponseDefinition` - Initialize default response
2. `ContextBaseGeneration` - Extract request parameters
3. `RequestValidation` *(abstract)* - Validate request
4. `IdempotencyLookup` - Check for duplicate transactions
5. `LoadSession` *(abstract)* - Load current session
6. `BalanceCheck` - Validate player balance
7. `CreateMovement` - Create movement object
8. `PersistMovementCreate` - Save to database
9. `ExecuteExternalTransfer` *(abstract)* - Call external wallet
10. `PersistMovementFinalize` - Update movement with final state
11. `BuildResponse` - Finalize response object
12. `Resend` *(branch target)* - Idempotency resend logic

#### 2. Win Pipeline (Credit/Deposit)
**Location**: `Librerie/BusinessLib/elements2/logic/casino/extint/Pipeline/Win/`

**Components**:
- `WinCtx`: Context object for win operations
- `WinHooks`: Abstract class with win-specific hooks
- `CasinoExtIntWinPipeline`: Win pipeline macro

**Steps**: Similar to Bet but without BalanceCheck

#### 3. Cancel Pipeline (RollBack)
**Location**: `Librerie/BusinessLib/elements2/logic/casino/extint/Pipeline/Cancel/`

**Components**:
- `CancelCtx`: Context with additional `RoundRef` and `RelatedBetMov`
- `CancelHooks`: Cancel-specific hooks
- `CasinoExtIntCancelPipeline`: Cancel pipeline macro

**Steps**: Similar to Win but with additional `FindRelatedBet` step

## Concrete Implementation

**`CasinoExtIntAMSWCorePipelined`**: Concrete implementation for CasinoAM provider
- Implements abstract validation, session loading, and transfer methods
- Compiles pipelines once for performance
- Provides `ExecuteBet()`, `ExecuteWin()`, `ExecuteCancel()` public APIs

## Test Suite

**Location**: `Tests/Pipeline/`

### BetPipelineTests
- Verifies step execution order
- Tests idempotency jump behavior
- Tests early stop mechanism
- Validates success/error responses

### WinPipelineTests
- Step order verification
- Idempotency behavior
- Success response validation

### CancelPipelineTests
- Step order verification
- Related bet linking
- Idempotency behavior
- Success response validation

## Usage

### Basic Usage

```csharp
var pipelined = new CasinoExtIntAMSWCorePipelined();

// Execute a bet
var betParams = new HashParams(
    "author", "system",
    "transactionId", "TX123",
    "ticket", "TK456",
    "amount", 100L
);
var betResult = pipelined.ExecuteBet(userId, betParams);

// Execute a win
var winParams = new HashParams(
    "author", "system",
    "transactionId", "TX124",
    "ticket", "TK456",
    "amount", 200L
);
var winResult = pipelined.ExecuteWin(userId, winParams);

// Execute a cancel
var cancelParams = new HashParams(
    "author", "system",
    "transactionId", "TX125",
    "roundRef", "ROUND_123"
);
var cancelResult = pipelined.ExecuteCancel(userId, cancelParams);
```

### Customization

```csharp
public class CustomBetPipeline : CasinoExtIntBetPipeline
{
    protected override void CustomizeBetSteps(List<Step<BetCtx>> steps)
    {
        // Add custom validation after RequestValidation
        InsertAfter(steps, BetHook.RequestValidation,
            new Step<BetCtx>(
                (Enum)999, // Custom hook enum
                CustomValidation,
                "CustomValidation"
            ));
    }

    private void CustomValidation(BetCtx ctx)
    {
        // Your custom logic
    }

    // Override abstract methods
    protected override void Bet_RequestValidation(BetCtx ctx) { ... }
    protected override void Bet_LoadSession(BetCtx ctx) { ... }
    protected override void Bet_ExecuteExternalTransfer(BetCtx ctx) { ... }
}
```

## Key Features

### 1. Idempotency Support
- Automatic duplicate transaction detection
- Jump to `Resend` step when idempotent request found
- Cached response replay

### 2. Jump/Branch Control
- Set `ctx.JumpToKey` to jump to any step
- Useful for error handling and idempotency

### 3. Early Stop
- Set `ctx.Stop = true` to halt pipeline execution
- Useful for validation failures

### 4. Trace Support
- Each step can be traced for debugging and testing
- Essential for behavioral testing

### 5. Provider Extensibility
- Abstract methods for provider-specific logic
- `CustomizeSteps()` hook for adding/replacing steps
- `InsertAfter()` and `Replace()` modifiers

## Migration from Legacy Code

The legacy methods in `CasinoExtIntAMSWCoreTest`:
- `_CasinoAM_Withdraw()` → `ExecuteBet()`
- `_CasinoAM_Deposit()` → `ExecuteWin()`
- `_CasinoAM_RollBack()` → `ExecuteCancel()`

**Migration steps**:
1. Extract validation logic → implement `*_RequestValidation()`
2. Extract session loading → implement `*_LoadSession()`
3. Extract wallet calls → implement `*_ExecuteExternalTransfer()`
4. Keep idempotency, persistence, response building as-is (provided by base)

## Testing Strategy

### Unit Tests (Current)
- Mock all external dependencies (DB, wallet, cache)
- Focus on pipeline flow and step execution
- Verify jump/stop behaviors

### Integration Tests (Configuration Required)
To enable end-to-end testing:

```csharp
// In test setup
var config = new TestConfig
{
    UseRealDatabase = true,
    UseRealCache = true,
    UseRealWallet = false, // Start with mock wallet
    ConnectionString = "..."
};

var pipeline = new CasinoExtIntAMSWCorePipelined(config);
```

Configuration toggles allow gradual integration:
1. Start with all mocks
2. Enable real database
3. Enable real cache
4. Enable real wallet (last)

## Building the Project

**Note**: This project targets .NET Framework 4.8, which requires a Windows environment or appropriate runtime.

```bash
# On Windows
dotnet build

# On Linux (requires Mono or appropriate runtime)
# Not directly supported - use Windows build agent
```

## Security Considerations

- All database operations use parameterized queries (via ORM)
- Transaction IDs are validated before use
- External wallet calls should use HTTPS and authentication
- Response data is sanitized before serialization
- CodeQL scanning recommended before deployment

## Future Enhancements

1. **Metrics/Telemetry**: Add step-level timing and success metrics
2. **Async Support**: Make steps async for better scalability
3. **Retry Logic**: Add automatic retry for transient failures
4. **Circuit Breaker**: Protect external wallet calls
5. **Distributed Tracing**: Add correlation IDs for multi-service flows

## Contributing

When adding new steps or customizations:
1. Follow the naming convention: `{Pipeline}_{StepName}`
2. Add corresponding test coverage
3. Update this README with new hooks/steps
4. Run CodeQL security scan

## License

[Project License Information]
