using System;
using GamingTests.Latest.Casino.ExtInt.Pipeline.Core;

namespace GamingTests.Latest.Casino.ExtInt.Pipeline.Examples
{
    /// <summary>
    /// Example demonstrating how to use the pipeline infrastructure with composition.
    /// 
    /// Usage Examples:
    /// 
    /// 1. Basic Pipeline Creation:
    ///    var builder = new PipelineBuilder<MyContext>();
    ///    builder.AddStep(new Step1())
    ///           .AddStep(new Step2())
    ///           .AddStep(new Step3());
    ///    var engine = builder.Build();
    ///    await engine.ExecuteAsync(context);
    ///
    /// 2. Pipeline with Precompiled Steps:
    ///    var precompiled = new PrecompiledSteps<MyContext>();
    ///    precompiled.Register("validation", new ValidationStep())
    ///               .Register("processing", new ProcessingStep());
    ///    
    ///    var builder = new PipelineBuilder<MyContext>();
    ///    builder.AddStep(precompiled.Get("validation"))
    ///           .AddStep(precompiled.Get("processing"));
    ///
    /// 3. Pipeline Customization:
    ///    var basePlan = new PipelinePlan<MyContext>();
    ///    basePlan.Add(new Step1())
    ///            .Add(new Step2())
    ///            .Add(new Step3());
    ///    
    ///    // Create a customized pipeline
    ///    var builder = new PipelineBuilder<MyContext>(basePlan);
    ///    builder.Customize(plan => {
    ///        // Insert a logging step before Step2
    ///        plan.InsertBefore<Step2>(new LoggingStep());
    ///        
    ///        // Replace Step3 with a custom implementation
    ///        plan.Replace<Step3>(new CustomStep3());
    ///        
    ///        // Remove a step
    ///        plan.Remove<UnwantedStep>();
    ///        
    ///        // Wrap a step with additional behavior
    ///        plan.Wrap<Step1>(original => new TimingWrapper(original));
    ///    });
    ///
    /// 4. Reusable Base Pipelines:
    ///    // Define a standard pipeline
    ///    public static PipelinePlan<WalletContext> CreateStandardPlan(
    ///        IProviderRules rules, 
    ///        IHelpers helpers)
    ///    {
    ///        return new PipelinePlan<WalletContext>()
    ///            .Add(new ValidateStep(rules))
    ///            .Add(new SessionStep(rules, helpers))
    ///            .Add(new IdempotencyStep(rules, helpers))
    ///            .Add(new ProcessStep(rules, helpers))
    ///            .Add(new FinalizeStep());
    ///    }
    ///    
    ///    // Customize for specific provider
    ///    var builder = new PipelineBuilder<WalletContext>(
    ///        CreateStandardPlan(amRules, helpers));
    ///    builder.Customize(plan => {
    ///        // AM-specific customization
    ///        plan.InsertAfter<SessionStep>(new AMSpecialCheckStep());
    ///    });
    ///
    /// 5. Multiple Pipeline Instances from Same Plan:
    ///    var basePlan = CreateStandardPlan(rules, helpers);
    ///    
    ///    // Create multiple customized versions
    ///    var betEngine = new PipelineBuilder<WalletContext>(basePlan)
    ///        .Customize(p => p.InsertBefore<ProcessStep>(new BalanceCheckStep()))
    ///        .Build();
    ///    
    ///    var winEngine = new PipelineBuilder<WalletContext>(basePlan)
    ///        .Customize(p => p.Remove<BalanceCheckStep>())
    ///        .Build();
    ///
    /// Key Benefits:
    /// - Composition over inheritance
    /// - Easy pipeline customization
    /// - Reusable step instances (precompiled)
    /// - Shared context across all steps
    /// - Type-safe step manipulation
    /// - Clean separation of concerns
    /// </summary>
    public static class PipelineUsageExamples
    {
        // This is a documentation class - no implementation needed
    }
}
