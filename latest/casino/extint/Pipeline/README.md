# Pipeline Architecture - Composition-Based Design

## Overview

This implementation provides a flexible, composition-based pipeline architecture for handling wallet method operations with steps, pre-compiled steps, and shared context.

## Core Components

### 1. IPipelineStep<TContext>
The fundamental interface for pipeline steps. Each step receives a shared context and returns a result indicating whether the pipeline should continue.

```csharp
public interface IPipelineStep<TContext>
{
    Task<PipelineStepResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);
}
```

### 2. PipelinePlan<TContext>
A modifiable collection of steps that supports customization through:
- **Add**: Append a step to the pipeline
- **InsertBefore/InsertAfter**: Insert steps relative to existing steps
- **Replace**: Swap out a step with a new implementation
- **Remove**: Remove steps from the pipeline
- **Wrap**: Wrap a step with additional behavior
- **Clone**: Create a copy for customization

### 3. PipelineBuilder<TContext>
Fluent builder for creating pipelines with easy customization:
- Start with an empty pipeline or base plan
- Add steps incrementally
- Apply customizations via lambda functions
- Build the final executable engine

### 4. PipelineEngine<TContext>
Executes the pipeline steps in order. Stops execution when a step returns `Stop()` or all steps complete.

### 5. PrecompiledSteps<TContext>
Registry for reusable, pre-configured step instances:
- Register steps by name
- Retrieve and reuse steps across multiple pipelines
- Centralize step configuration

## Key Benefits

✅ **Composition over Inheritance**: Build pipelines by composing steps, not inheriting from base classes
✅ **Easy Customization**: Modify pipelines using Insert, Replace, Remove, and Wrap operations
✅ **Reusable Steps**: Pre-compile and register common steps for reuse
✅ **Shared Context**: All steps work with a common context object
✅ **Type-Safe**: Leverage generics for compile-time type safety
✅ **Clean Separation**: Each step has a single responsibility

## Usage Examples

### Basic Pipeline Creation

```csharp
var builder = new PipelineBuilder<WalletContext>();
builder.AddStep(new ValidateRequestStep())
       .AddStep(new SessionCheckStep())
       .AddStep(new ProcessStep())
       .AddStep(new FinalizeStep());

var engine = builder.Build();
await engine.ExecuteAsync(context);
```

### Using Pre-compiled Steps

```csharp
// Register reusable steps
var precompiled = new PrecompiledSteps<WalletContext>();
precompiled.Register("validate", new ValidateRequestStep())
           .Register("session", new SessionCheckStep())
           .Register("process", new ProcessStep());

// Build pipeline using pre-compiled steps
var builder = new PipelineBuilder<WalletContext>();
builder.AddStep(precompiled.Get("validate"))
       .AddStep(precompiled.Get("session"))
       .AddStep(precompiled.Get("process"));
```

### Pipeline Customization

```csharp
// Create a base plan
var basePlan = new PipelinePlan<WalletContext>()
    .Add(new ValidateStep())
    .Add(new SessionStep())
    .Add(new ProcessStep())
    .Add(new FinalizeStep());

// Customize for a specific provider
var builder = new PipelineBuilder<WalletContext>(basePlan);
builder.Customize(plan => {
    // Add provider-specific validation
    plan.InsertBefore<SessionStep>(new ProviderValidationStep());
    
    // Replace standard processing with custom logic
    plan.Replace<ProcessStep>(new CustomProcessStep());
    
    // Remove unnecessary steps
    plan.Remove<OptionalStep>();
    
    // Wrap a step with timing/logging
    plan.Wrap<SessionStep>(original => new LoggingWrapper(original));
});

var engine = builder.Build();
```

### Reusable Base Pipelines

```csharp
// Define a standard wallet pipeline factory
public static PipelinePlan<WalletContext> CreateStandardPlan(
    IProviderRules rules, 
    IWalletHelpers helpers)
{
    return new PipelinePlan<WalletContext>()
        .Add(new ValidateRequestStep(rules))
        .Add(new SessionStep(rules, helpers))
        .Add(new IdempotencyStep(rules, helpers))
        .Add(new MovementStep(rules, helpers))
        .Add(new ExternalWalletStep(rules, helpers))
        .Add(new FinalizeStep());
}

// Provider A - uses standard pipeline
var engineA = new PipelineBuilder<WalletContext>(
    CreateStandardPlan(rulesA, helpers)
).Build();

// Provider B - customizes the standard pipeline
var engineB = new PipelineBuilder<WalletContext>(
    CreateStandardPlan(rulesB, helpers))
    .Customize(plan => {
        plan.InsertAfter<SessionStep>(new ProviderBSpecialCheckStep());
    })
    .Build();
```

### Multiple Pipelines from Same Base

```csharp
var basePlan = CreateStandardPlan(rules, helpers);

// Bet pipeline - adds balance check
var betEngine = new PipelineBuilder<WalletContext>(basePlan)
    .Customize(p => p.InsertBefore<MovementStep>(new BalanceCheckStep()))
    .Build();

// Win pipeline - removes balance check
var winEngine = new PipelineBuilder<WalletContext>(basePlan)
    .Build();

// Cancel pipeline - adds cancellation logic
var cancelEngine = new PipelineBuilder<WalletContext>(basePlan)
    .Customize(p => p.InsertBefore<FinalizeStep>(new MarkCancelledStep()))
    .Build();
```

## Architecture Patterns

### Shared Context
All steps share a common context object that flows through the pipeline:

```csharp
public class WalletContext
{
    public WalletOperation Operation { get; init; }
    public WalletRequest Request { get; init; }
    public WalletMovement? Movement { get; set; }
    public WalletResult? Result { get; set; }
    public IDictionary<string, object> Bag { get; } = new Dictionary<string, object>();
}
```

Steps can read from and write to the context, enabling data flow between steps.

### Step Results
Each step returns a `PipelineStepResult` that controls pipeline flow:

```csharp
// Continue to next step
return PipelineStepResult.Next();

// Stop pipeline execution
return PipelineStepResult.Stop();
```

### Dependency Injection
Steps can receive dependencies via constructor injection:

```csharp
public class SessionStep : IPipelineStep<WalletContext>
{
    private readonly IProviderRules _rules;
    private readonly IWalletHelpers _helpers;
    
    public SessionStep(IProviderRules rules, IWalletHelpers helpers)
    {
        _rules = rules;
        _helpers = helpers;
    }
    
    public async Task<PipelineStepResult> ExecuteAsync(
        WalletContext context, 
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

## Design Principles

1. **Composition**: Build complex pipelines by composing simple, focused steps
2. **Immutability**: Plans are cloned before customization to prevent side effects
3. **Flexibility**: Easy to add, remove, replace, or wrap steps
4. **Reusability**: Steps and plans can be shared across multiple pipelines
5. **Testability**: Each step can be tested independently
6. **Type Safety**: Generic types ensure compile-time correctness

## Files Structure

```
Pipeline/
├── Core/
│   ├── IPipelineStep.cs        # Step interface and result types
│   ├── PipelinePlan.cs         # Step collection with customization
│   ├── PipelineBuilder.cs      # Fluent builder for pipelines
│   ├── PipelineEngine.cs       # Pipeline executor
│   └── PrecompiledSteps.cs     # Step registry
└── Examples/
    └── PipelineUsageExamples.cs # Documentation and examples
```

## Summary

This pipeline architecture provides a clean, flexible, and maintainable way to handle wallet operations through composition. It allows easy customization per provider while maintaining a standard base implementation, all without relying on inheritance hierarchies.
