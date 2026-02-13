# Pipeline Architecture - Composition Pattern

## Overview

This document describes the **composition-based** pipeline architecture for wallet methods (Win, Bet, Cancel). 

**Key principle**: The pipeline is a **list of executable components** that an engine runs in sequence. This is NOT a nested architecture with virtual method chains.

---

## Architecture Type: COMPOSITION ✓

### What is Composition?

In composition architecture:
- **Order and selection** of components are decided upfront (build/compile time)
- **Execution** is a simple loop over the list
- **Customization** happens by patching the plan (replace/insert/remove/wrap)
- **NO virtual methods** calling `base.*()` to control flow

### What is NOT (Nested)?

Nested architecture (which we DO NOT use):
- Flow controlled by inheritance chain
- Virtual methods calling `base.Method()`
- Order determined at runtime through polymorphism
- Hard to test, debug, and understand

---

## Directory Structure

```
/Pipeline
  /Core                          # Core pipeline infrastructure
    IPipelineContext.cs          # Interface for all contexts
    PipelineEngine.cs            # Execution engine
    PipelineComponent.cs         # Single executable component
    PipelinePlan.cs              # Modifiable list of components
    PipelineDiagnostics.cs       # Audit and debugging tools

  /Methods                       # Method-specific implementations
    /Win                         # Win (Credit/Deposit) method
      WinContext.cs              # Input/state/output for Win
      WinPipeline.Standard.cs    # Standard component plan
      WinPipeline.Customization.cs  # Patches for integrations
      WinPipeline.Factory.cs     # Creates final pipeline
      /Components                # Atomic component implementations
        ResponseDefinitionComponent.cs
        ContextBaseGenerationComponent.cs
        IdempotencyLookupComponent.cs
        CreateMovementComponent.cs
        PersistMovementCreateComponent.cs
        PersistMovementFinalizeComponent.cs
        BuildResponseComponent.cs
        ResendComponent.cs

    /Bet                         # Bet (Debit/Withdraw) method
      BetContext.cs
      BetPipeline.Standard.cs
      BetPipeline.Customization.cs
      BetPipeline.Factory.cs
      /Components
        (9 components + Resend)

    /Cancel                      # Cancel (Rollback) method
      CancelContext.cs
      CancelPipeline.Standard.cs
      CancelPipeline.Customization.cs
      CancelPipeline.Factory.cs
      /Components
        (9 components + Resend)
```

---

## Key Concepts

### 1. Context

Each method has a context class that implements `IPipelineContext`:

```csharp
public sealed class WinContext : IPipelineContext
{
    // Input parameters
    public HashParams AuxPars { get; }
    public string TransactionId { get; set; }
    
    // State during execution
    public CasinoMovimentiBuffer NewMov { get; set; }
    public string TargetStatus { get; set; }
    
    // Output
    public SmartHash Response { get; }
    
    // Pipeline control (from IPipelineContext)
    public string JumpToKey { get; set; }
    public bool Stop { get; set; }
}
```

### 2. Components

Components are atomic, executable units with:
- A stable **Key** (for reference in patches, diagnostics, jumps)
- An **Execute** action (receives context, mutates it)
- An optional **Description**

```csharp
var component = new PipelineComponent<WinContext>(
    "IdempotencyLookup",
    ctx => {
        // Check for duplicate transactions
        if (FindDuplicate(ctx))
            ctx.JumpToKey = "Resend";
    },
    "Check for duplicate transactions"
);
```

### 3. Standard Plan

Defines the **superset** (maximum level) flow for a method:

```csharp
public static PipelinePlan<WinContext> CreateStandardPlan()
{
    var plan = new PipelinePlan<WinContext>();
    
    plan.Add(new PipelineComponent<WinContext>("ResponseDefinition", ...));
    plan.Add(new PipelineComponent<WinContext>("ContextBaseGeneration", ...));
    plan.Add(new PipelineComponent<WinContext>("RequestValidation", ...));
    plan.Add(new PipelineComponent<WinContext>("IdempotencyLookup", ...));
    // ... more components
    
    return plan;
}
```

### 4. Customizer

Applies **patches** to the standard plan for specific integrations:

```csharp
public static void ApplyCustomizations(PipelinePlan<WinContext> plan, string integration)
{
    if (integration == "CasinoAM")
    {
        // Replace placeholder with real implementation
        plan.Replace("RequestValidation", new PipelineComponent<WinContext>(
            "RequestValidation",
            ctx => { /* CasinoAM validation logic */ },
            "Validate request for CasinoAM"
        ));
        
        // Insert additional component
        plan.InsertAfter("IdempotencyLookup", new PipelineComponent<WinContext>(
            "CasinoAMPreCheck",
            ctx => { /* Custom pre-check */ },
            "CasinoAM specific pre-check"
        ));
    }
}
```

Available operations:
- `Replace(key, component)` - Substitute a component
- `InsertAfter(key, component)` - Add after a component
- `InsertBefore(key, component)` - Add before a component
- `Remove(key)` - Remove a component
- `Wrap(key, wrapper)` - Wrap with additional logic (logging, telemetry, retry)

### 5. Factory

Creates the final pipeline by combining standard + customizations:

```csharp
public static PipelineComponent<WinContext>[] CreatePipeline(string integration)
{
    // 1. Create standard plan
    var plan = WinPipelineStandard.CreateStandardPlan();
    
    // 2. Apply customizations
    WinPipelineCustomizer.ApplyCustomizations(plan, integration);
    
    // 3. Compile and validate
    var compiled = plan.Compile();
    PipelineDiagnostics.ValidatePipeline(compiled);
    
    return compiled;
}
```

### 6. Engine

Executes the compiled pipeline:

```csharp
var pipeline = WinPipelineFactory.CreatePipeline("CasinoAM");
var ctx = new WinContext(euId, auxPars);

PipelineEngine.Run(pipeline, ctx, c => c.Stop);

return new Hashtable(ctx.Response);
```

---

## Complete Flow: Win Method Example

### Standard Components (11 total)

1. **ResponseDefinition** - Initialize default response
2. **ContextBaseGeneration** - Populate base fields
3. **RequestValidation** - Validate request (PLACEHOLDER → customized per integration)
4. **IdempotencyLookup** - Check for duplicates → Jump to Resend if found
5. **LoadSession** - Load session info (PLACEHOLDER → customized per integration)
6. **CreateMovement** - Create CasinoMovimentiBuffer object
7. **PersistMovementCreate** - Save to DB (initial state)
8. **ExecuteExternalTransfer** - Call external wallet (PLACEHOLDER → customized per integration)
9. **PersistMovementFinalize** - Update DB (final state)
10. **BuildResponse** - Construct final response
11. **Resend** - Handle idempotent retry (jump target)

### CasinoAM Customization

The customizer replaces the 3 placeholder components:

```csharp
private static void ApplyCasinoAMCustomizations(PipelinePlan<WinContext> plan)
{
    // Replace RequestValidation
    plan.Replace("RequestValidation", new PipelineComponent<WinContext>(
        "RequestValidation",
        ctx => {
            var transactionId = ctx.AuxPars.getTypedValue("transactionId", "", false);
            if (string.IsNullOrWhiteSpace(transactionId)) {
                ctx.TargetStatus = "409";
                ctx.Stop = true;
                return;
            }
            ctx.CmbType = CasinoMovimentiBuffer.Type.Win;
        },
        "Validate request for CasinoAM"
    ));
    
    // Replace LoadSession
    plan.Replace("LoadSession", ...);
    
    // Replace ExecuteExternalTransfer
    plan.Replace("ExecuteExternalTransfer", ...);
}
```

### Execution

```csharp
// Create instance
var core = new CasinoExtIntAMSWCorePipelineComposition();

// Execute Win
var result = core.ExecuteWin(euId, auxPars);

// Get diagnostics
var diagnostics = core.GetWinDiagnostics();
Console.WriteLine(diagnostics);
```

---

## Diagnostics

### Print Pipeline

```csharp
var pipeline = WinPipelineFactory.CreatePipeline("CasinoAM");
var report = PipelineDiagnostics.PrintPipeline(pipeline, "Win Pipeline for CasinoAM");
```

Output:
```
=== Win Pipeline for CasinoAM ===
Total components: 11

  1. [ResponseDefinition] Initialize default response
  2. [ContextBaseGeneration] Populate base context fields
  3. [RequestValidation] Validate request for CasinoAM
  4. [IdempotencyLookup] Check for duplicate transactions
  5. [LoadSession] Load session for CasinoAM
  ...
```

### Compare Pipelines

```csharp
var standard = WinPipelineFactory.GetStandardPipeline();
var customized = WinPipelineFactory.CreatePipeline("CasinoAM");
var diff = PipelineDiagnostics.ComparePipelines(standard, customized);
```

Output:
```
=== Pipeline Comparison: Standard vs CasinoAM ===

ORDER CHANGED:
No differences found in order.

REPLACED components:
  [RequestValidation] from "Validate request (PLACEHOLDER)" to "Validate request for CasinoAM"
  [LoadSession] from "Load session (PLACEHOLDER)" to "Load session for CasinoAM"
  [ExecuteExternalTransfer] from "Execute transfer (PLACEHOLDER)" to "Execute transfer for CasinoAM"
```

---

## Method-Specific Differences

### Win (Credit/Deposit)
- **Movement Type**: `CasinoMovimentiBuffer.Type.Win`
- **Components**: 8 standard + 3 placeholders + Resend = 11 total
- **Unique**: No special components

### Bet (Debit/Withdraw)
- **Movement Type**: `CasinoMovimentiBuffer.Type.Loose`
- **Components**: 9 standard + 3 placeholders + Resend = 12 total
- **Unique**: **BalanceCheckComponent** - Verifies sufficient funds before debit

### Cancel (Rollback)
- **Movement Type**: `CasinoMovimentiBuffer.Type.LooseCancel`
- **Components**: 9 standard + 3 placeholders + Resend = 12 total
- **Unique**: **FindRelatedBetComponent** - Locates the bet to cancel
- **Field**: `RoundRef` to identify the game round

---

## Benefits of This Architecture

### 1. **Clear Audit Trail**
- Exactly which components run in which order
- Easy to see what changed between standard and custom
- Diagnostics show applied patches

### 2. **Testable**
- Test standard plan independently
- Test customizations independently
- Test complete integration
- Mock individual components easily

### 3. **Maintainable**
- Each component is small and focused
- Changes isolated to specific components
- No inheritance chain confusion

### 4. **Extensible**
- Add new integrations without touching standard
- Add/remove/reorder components via plan operations
- Wrap components for cross-cutting concerns

### 5. **Runtime Configurable**
- Feature flags can enable/disable components
- Different integrations get different pipelines
- Same standard, different customizations

---

## Anti-Patterns: How to Recognize Nested Architecture

If you see this, it's **nested** (BAD):

```csharp
public class BasePipeline {
    protected virtual void Step1() { /* base impl */ }
    protected virtual void Step2() { /* base impl */ }
    
    public void Execute() {
        Step1();
        Step2();
    }
}

public class ProviderPipeline : BasePipeline {
    protected override void Step1() {
        base.Step1();  // ← Calls base!
        // Custom logic
    }
}
```

In composition (GOOD), we have:

```csharp
var plan = new PipelinePlan<Context>();
plan.Add(new PipelineComponent<Context>("Step1", Step1Impl));
plan.Add(new PipelineComponent<Context>("Step2", Step2Impl));

// Customization = patch the plan
plan.Replace("Step1", new PipelineComponent<Context>("Step1", CustomStep1Impl));

var pipeline = plan.Compile();
PipelineEngine.Run(pipeline, ctx, c => c.Stop);
```

---

## Integration Example: Adding a New Provider

### Step 1: Add Customization Method

```csharp
// In WinPipeline.Customization.cs
private static void ApplyProviderXCustomizations(PipelinePlan<WinContext> plan)
{
    // Replace validation
    plan.Replace("RequestValidation", new PipelineComponent<WinContext>(
        "RequestValidation",
        ctx => { /* ProviderX validation */ },
        "Validate for ProviderX"
    ));
    
    // Add fraud check
    plan.InsertAfter("IdempotencyLookup", new PipelineComponent<WinContext>(
        "ProviderXFraudCheck",
        ctx => { /* Fraud detection */ },
        "ProviderX fraud check"
    ));
    
    // Wrap transfer with telemetry
    plan.Wrap("ExecuteExternalTransfer", original => ctx => {
        var start = DateTime.UtcNow;
        try {
            original(ctx);
        } finally {
            var elapsed = DateTime.UtcNow - start;
            // Log telemetry
        }
    });
}
```

### Step 2: Register in Customizer Switch

```csharp
public static void ApplyCustomizations(PipelinePlan<WinContext> plan, string integration)
{
    switch (integration.ToUpperInvariant())
    {
        case "CASINOAM":
            ApplyCasinoAMCustomizations(plan);
            break;
        case "PROVIDERX":
            ApplyProviderXCustomizations(plan);
            break;
    }
}
```

### Step 3: Use It

```csharp
var result = WinPipelineFactory.Execute(euId, auxPars, "ProviderX");
```

---

## Testing Strategy

### Unit Tests: Components

```csharp
[Test]
public void TestIdempotencyLookup_FindsDuplicate_JumpsToResend()
{
    var ctx = new WinContext(euId, auxPars);
    ctx.TransactionId = "existing-tx-123";
    
    IdempotencyLookupComponent.Execute(ctx);
    
    Assert.AreEqual("Resend", ctx.JumpToKey);
}
```

### Integration Tests: Full Pipeline

```csharp
[Test]
public void TestWinPipeline_CasinoAM_Success()
{
    var auxPars = new HashParams(
        "transactionId", "tx-456",
        "amount", 100L,
        "ticket", "valid-ticket"
    );
    
    var result = WinPipelineFactory.Execute(euId, auxPars, "CasinoAM");
    
    Assert.AreEqual("200", result["responseCodeReason"]);
}
```

### Parity Tests: Standard vs Custom

```csharp
[Test]
public void TestWinPipeline_AllIntegrations_HaveSameComponentKeys()
{
    var standard = WinPipelineFactory.GetStandardPipeline();
    var casinoAM = WinPipelineFactory.CreatePipeline("CasinoAM");
    
    var standardKeys = PipelineDiagnostics.GetKeys(standard);
    var casinoAMKeys = PipelineDiagnostics.GetKeys(casinoAM);
    
    CollectionAssert.AreEqual(standardKeys, casinoAMKeys);
}
```

---

## Migration from Old Architecture

### Old Files (Deprecated)

These files are part of the OLD nested architecture and should be deprecated:

```
/Pipeline
  CasinoExtIntBasePipeline.cs     # OLD: Base with Step<T> and CompiledSteps<T>
  /Win
    WinHooks.cs                     # OLD: Hook implementations
    CasinoExtIntWinPipeline.cs      # OLD: Build/Run macros
    WinCtx.cs                       # OLD: Context (now WinContext)
  /Bet
    BetHooks.cs                     # OLD
    CasinoExtIntBetPipeline.cs      # OLD
    BetCtx.cs                       # OLD
  /Cancel
    CancelHooks.cs                  # OLD
    CasinoExtIntCancelPipeline.cs   # OLD
    CancelCtx.cs                    # OLD
```

### New Files (Composition)

```
/Pipeline
  /Core
    IPipelineContext.cs             # NEW: Interface
    PipelineEngine.cs               # NEW: Execution engine
    PipelineComponent.cs            # NEW: Component model
    PipelinePlan.cs                 # NEW: Modifiable plan
    PipelineDiagnostics.cs          # NEW: Audit tools
  /Methods
    /Win
      WinContext.cs                 # NEW: Context
      WinPipeline.Standard.cs       # NEW: Standard plan
      WinPipeline.Customization.cs  # NEW: Patches
      WinPipeline.Factory.cs        # NEW: Factory
      /Components                   # NEW: Atomic components
    /Bet
      (similar structure)
    /Cancel
      (similar structure)
```

---

## Summary

This composition-based pipeline architecture provides:

✓ **Clear separation of concerns** - Core, Standard, Customization, Factory
✓ **Explicit component list** - No hidden virtual method chains
✓ **Declarative plan** - Order defined upfront
✓ **Patch-based customization** - Replace/Insert/Remove/Wrap
✓ **Full diagnostics** - Audit trail, diff, validation
✓ **Testable** - Unit test components, integration test pipelines
✓ **Maintainable** - Small focused pieces
✓ **Extensible** - Easy to add new integrations

The architecture is **pure composition**, NOT nested inheritance.
