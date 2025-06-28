using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Configuration;
using AgentAlpha.Extensions;
using Common.Models.Session;
using Common.Services.Session;
using Common.Interfaces.Session;
using SessionService.Services;
using System.Text.Json;

namespace AgentAlpha.Tests;

/// <summary>
/// End-to-end integration test demonstrating sequential subtask execution
/// </summary>
public class SubtaskExecutionIntegrationTests
{
    [Fact]
    public async Task EndToEnd_SequentialSubtaskExecution_WithTaskStateMarkdown()
    {
        // Arrange - Setup a complete service container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var config = new AgentConfiguration
        {
            OpenAiApiKey = "test-key",
            Model = "gpt-4o",
            MaxIterations = 10
        };
        
        services.AddAgentAlphaServices(config);
        
        // Replace the session manager with a local one for testing
        services.RemoveAll<ISessionManager>();
        services.AddSingleton<ISessionManager>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SessionManager>>();
            return new SessionManager(logger);
        });

        var serviceProvider = services.BuildServiceProvider();
        var taskStateManager = serviceProvider.GetRequiredService<ITaskStateManager>();
        var sessionManager = serviceProvider.GetRequiredService<ISessionManager>();
        
        // Create a multi-step task plan
        var taskPlan = new TaskPlan
        {
            Task = "Create a comprehensive data analysis report",
            Strategy = "Gather data, clean it, analyze it, and format the final report",
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    StepNumber = 1,
                    Description = "Gather raw data from multiple sources",
                    IsMandatory = true,
                    PotentialTools = new List<string> { "file_read", "web_search", "api_call" },
                    ExpectedOutput = "Raw data files collected"
                },
                new PlanStep
                {
                    StepNumber = 2,
                    Description = "Clean and validate the collected data",
                    IsMandatory = true,
                    PotentialTools = new List<string> { "text_process", "data_validate", "file_write" },
                    ExpectedInput = "Raw data files",
                    ExpectedOutput = "Clean, validated dataset"
                },
                new PlanStep
                {
                    StepNumber = 3,
                    Description = "Perform statistical analysis on the clean data",
                    IsMandatory = true,
                    PotentialTools = new List<string> { "calculate", "statistical_analysis", "chart_generate" },
                    ExpectedInput = "Clean dataset",
                    ExpectedOutput = "Analysis results and charts"
                },
                new PlanStep
                {
                    StepNumber = 4,
                    Description = "Format and compile the final report",
                    IsMandatory = true,
                    PotentialTools = new List<string> { "file_write", "template_process", "pdf_generate" },
                    ExpectedInput = "Analysis results",
                    ExpectedOutput = "Final formatted report"
                }
            }
        };
        
        // Create a session for this task
        var session = await sessionManager.CreateSessionAsync("Data Analysis Project");

        // Act 1: Create task state from plan
        var taskState = taskStateManager.CreateTaskState(taskPlan);
        await taskStateManager.SaveTaskStateAsync(session.SessionId, taskState);

        // Verify initial state
        var retrievedTaskState = await taskStateManager.GetTaskStateAsync(session.SessionId);
        Assert.NotNull(retrievedTaskState);
        Assert.Equal(TaskCompletionStatus.InProgress, retrievedTaskState.Status);
        Assert.Equal(4, retrievedTaskState.Subtasks.Count);
        Assert.Equal(0, retrievedTaskState.GetCompletedCount());

        // Generate initial markdown
        var initialMarkdown = retrievedTaskState.ToMarkdown();
        Assert.Contains("# Task: Create a comprehensive data analysis report", initialMarkdown);
        Assert.Contains("**Progress:** 0/4 subtasks completed", initialMarkdown);
        Assert.Contains("- [ ] **Step 1:** Gather raw data from multiple sources", initialMarkdown);
        Assert.Contains("- [ ] **Step 2:** Clean and validate the collected data", initialMarkdown);
        Assert.Contains("- [ ] **Step 3:** Perform statistical analysis on the clean data", initialMarkdown);
        Assert.Contains("- [ ] **Step 4:** Format and compile the final report", initialMarkdown);

        // Act 2: Execute subtasks sequentially with context passing
        
        // Complete Step 1
        var step1Context = new Dictionary<string, object>
        {
            ["dataFiles"] = new[] { "sales_data.csv", "customer_data.json", "product_info.xml" },
            ["recordCount"] = 15420,
            ["dataQuality"] = "Good - minimal missing values",
            ["sources"] = "CRM system, web API, product database"
        };
        
        taskState = await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 
            1, 
            "Successfully gathered data from all sources", 
            "Collected 15,420 records from 3 different data sources",
            step1Context);

        // Verify Step 1 completion
        Assert.Equal(1, taskState.GetCompletedCount());
        var step1 = taskState.Subtasks.First(s => s.StepNumber == 1);
        Assert.Equal(SubtaskStatus.Completed, step1.Status);
        Assert.NotNull(step1.CompletedAt);
        
        // Check accumulated context
        var accumulatedContext = await taskStateManager.GetAccumulatedContextAsync(session.SessionId);
        Assert.Contains("Step1_dataFiles", accumulatedContext.Keys);
        Assert.Contains("Step1_recordCount", accumulatedContext.Keys);

        // Complete Step 2 using context from Step 1
        var step2Context = new Dictionary<string, object>
        {
            ["cleanedFile"] = "cleaned_data.csv",
            ["removedRecords"] = 124,
            ["validRecords"] = 15296,
            ["validationSummary"] = "Removed duplicates and invalid entries"
        };
        
        taskState = await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 
            2, 
            "Data cleaning completed successfully", 
            "Cleaned dataset contains 15,296 valid records",
            step2Context);

        // Verify Step 2 completion
        Assert.Equal(2, taskState.GetCompletedCount());
        var step2 = taskState.Subtasks.First(s => s.StepNumber == 2);
        Assert.Equal(SubtaskStatus.Completed, step2.Status);

        // Complete Step 3 using context from previous steps
        var step3Context = new Dictionary<string, object>
        {
            ["analysisResults"] = "analysis_summary.json",
            ["charts"] = new[] { "sales_trends.png", "customer_segments.png", "product_performance.png" },
            ["keyFindings"] = "Revenue increased 23% YoY, customer retention improved",
            ["correlations"] = "Strong correlation between product quality and customer satisfaction"
        };
        
        taskState = await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 
            3, 
            "Statistical analysis completed with key insights", 
            "Generated comprehensive analysis with 3 visualization charts",
            step3Context);

        // Verify Step 3 completion
        Assert.Equal(3, taskState.GetCompletedCount());
        var step3 = taskState.Subtasks.First(s => s.StepNumber == 3);
        Assert.Equal(SubtaskStatus.Completed, step3.Status);

        // Complete Step 4 - Final report
        var step4Context = new Dictionary<string, object>
        {
            ["finalReport"] = "data_analysis_report.pdf",
            ["reportPages"] = 24,
            ["appendices"] = 3,
            ["executiveSummary"] = "Executive summary highlighting 23% revenue growth and key recommendations"
        };
        
        taskState = await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 
            4, 
            "Final report compilation completed", 
            "Generated comprehensive 24-page report with executive summary",
            step4Context);

        // Assert: Verify complete task execution
        Assert.Equal(4, taskState.GetCompletedCount());
        Assert.Equal(TaskCompletionStatus.Completed, taskState.Status);
        
        // All subtasks should be completed
        Assert.All(taskState.Subtasks, subtask => 
            Assert.Equal(SubtaskStatus.Completed, subtask.Status));

        // Verify final markdown representation
        var finalMarkdown = taskState.ToMarkdown();
        Assert.Contains("**Status:** Completed", finalMarkdown);
        Assert.Contains("**Progress:** 4/4 subtasks completed", finalMarkdown);
        Assert.Contains("- [x] **Step 1:** Gather raw data from multiple sources", finalMarkdown);
        Assert.Contains("- [x] **Step 2:** Clean and validate the collected data", finalMarkdown);
        Assert.Contains("- [x] **Step 3:** Perform statistical analysis on the clean data", finalMarkdown);
        Assert.Contains("- [x] **Step 4:** Format and compile the final report", finalMarkdown);
        
        // Verify all completion summaries are included
        Assert.Contains("*Completed:* Successfully gathered data from all sources", finalMarkdown);
        Assert.Contains("*Completed:* Data cleaning completed successfully", finalMarkdown);
        Assert.Contains("*Completed:* Statistical analysis completed with key insights", finalMarkdown);
        Assert.Contains("*Completed:* Final report compilation completed", finalMarkdown);

        // Verify accumulated context section
        Assert.Contains("## Context from Completed Subtasks", finalMarkdown);
        Assert.Contains("**Step1_dataFiles:**", finalMarkdown);
        Assert.Contains("**Step1_recordCount:** 15420", finalMarkdown);
        Assert.Contains("**Step2_cleanedFile:** cleaned_data.csv", finalMarkdown);
        Assert.Contains("**Step3_keyFindings:**", finalMarkdown);
        Assert.Contains("**Step4_finalReport:** data_analysis_report.pdf", finalMarkdown);

        // Verify context continuity - each step should have access to previous step context
        var allAccumulatedContext = await taskStateManager.GetAccumulatedContextAsync(session.SessionId);
        Assert.Equal(16, allAccumulatedContext.Count); // 4 context items per step * 4 steps
        
        // Verify no current subtask (all completed)
        var currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.Null(currentSubtask);

        // Verify final task state shows completion properly
        var finalTaskState = await taskStateManager.GetTaskStateAsync(session.SessionId);
        Assert.NotNull(finalTaskState);
        Assert.Equal(TaskCompletionStatus.Completed, finalTaskState.Status);
        Assert.True(finalTaskState.Subtasks.All(s => s.IsCompleted));
    }

    [Fact]
    public async Task SubtaskExecution_WithPrerequisites_ExecutesInCorrectOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var config = new AgentConfiguration
        {
            OpenAiApiKey = "test-key",
            Model = "gpt-4o",
            MaxIterations = 10
        };
        
        services.AddAgentAlphaServices(config);
        
        // Replace the session manager with a local one for testing
        services.RemoveAll<ISessionManager>();
        services.AddSingleton<ISessionManager>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SessionManager>>();
            return new SessionManager(logger);
        });

        var serviceProvider = services.BuildServiceProvider();
        var taskStateManager = serviceProvider.GetRequiredService<ITaskStateManager>();
        var sessionManager = serviceProvider.GetRequiredService<ISessionManager>();
        
        var session = await sessionManager.CreateSessionAsync("Prerequisites Test");
        
        // Create a task state manually with prerequisites
        var taskState = new TaskState
        {
            Task = "Complex workflow with dependencies",
            Strategy = "Execute steps with proper dependency order",
            Subtasks = new List<SubtaskState>
            {
                new SubtaskState { StepNumber = 1, Description = "Setup infrastructure", Status = SubtaskStatus.Pending },
                new SubtaskState { StepNumber = 2, Description = "Install dependencies", Status = SubtaskStatus.Pending, Prerequisites = new List<int> { 1 } },
                new SubtaskState { StepNumber = 3, Description = "Configure application", Status = SubtaskStatus.Pending, Prerequisites = new List<int> { 1, 2 } },
                new SubtaskState { StepNumber = 4, Description = "Run tests", Status = SubtaskStatus.Pending, Prerequisites = new List<int> { 3 } },
                new SubtaskState { StepNumber = 5, Description = "Deploy to production", Status = SubtaskStatus.Pending, Prerequisites = new List<int> { 4 } }
            }
        };
        
        await taskStateManager.SaveTaskStateAsync(session.SessionId, taskState);

        // Act & Assert: Verify correct execution order

        // Initially, only step 1 should be available
        var currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.NotNull(currentSubtask);
        Assert.Equal(1, currentSubtask.StepNumber);

        // Complete step 1
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 1, "Infrastructure setup complete");
        
        // Now step 2 should be available
        currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.NotNull(currentSubtask);
        Assert.Equal(2, currentSubtask.StepNumber);

        // Complete step 2
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 2, "Dependencies installed");
        
        // Now step 3 should be available (requires both 1 and 2)
        currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.NotNull(currentSubtask);
        Assert.Equal(3, currentSubtask.StepNumber);

        // Complete step 3
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 3, "Application configured");
        
        // Now step 4 should be available
        currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.NotNull(currentSubtask);
        Assert.Equal(4, currentSubtask.StepNumber);

        // Complete step 4
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 4, "Tests passed");
        
        // Finally step 5 should be available
        currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.NotNull(currentSubtask);
        Assert.Equal(5, currentSubtask.StepNumber);

        // Complete step 5
        var finalTaskState = await taskStateManager.CompleteSubtaskAsync(session.SessionId, 5, "Deployment successful");
        
        // All should be complete now
        currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.Null(currentSubtask);
        Assert.Equal(TaskCompletionStatus.Completed, finalTaskState.Status);
    }

    [Fact]
    public async Task TaskStateMarkdown_ShowsCompleteWorkflowProgress()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var taskStateManager = new TaskStateManager(sessionManager, loggerFactory.CreateLogger<TaskStateManager>());
        
        var session = await sessionManager.CreateSessionAsync("Markdown Workflow Test");
        
        var taskPlan = new TaskPlan
        {
            Task = "Build and deploy a web application",
            Strategy = "Follow standard software development lifecycle",
            Steps = new List<PlanStep>
            {
                new PlanStep { StepNumber = 1, Description = "Design application architecture" },
                new PlanStep { StepNumber = 2, Description = "Implement core features" },
                new PlanStep { StepNumber = 3, Description = "Write comprehensive tests" },
                new PlanStep { StepNumber = 4, Description = "Deploy to staging environment" },
                new PlanStep { StepNumber = 5, Description = "Deploy to production" }
            }
        };

        var taskState = taskStateManager.CreateTaskState(taskPlan);
        await taskStateManager.SaveTaskStateAsync(session.SessionId, taskState);

        // Act: Execute workflow and verify markdown at each step
        
        // Initial state
        var markdown = taskState.ToMarkdown();
        Assert.Contains("**Progress:** 0/5 subtasks completed", markdown);
        Assert.Contains("- [ ] **Step 1:** Design application architecture", markdown);
        Assert.DoesNotContain("## Context from Completed Subtasks", markdown);

        // Complete Step 1
        var step1Context = new Dictionary<string, object>
        {
            ["architecture"] = "microservices",
            ["database"] = "postgresql",
            ["framework"] = "dotnet"
        };
        taskState = await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 1, "Architecture designed with microservices approach", 
            "Created detailed architecture diagrams", step1Context);
        
        markdown = taskState.ToMarkdown();
        Assert.Contains("**Progress:** 1/5 subtasks completed", markdown);
        Assert.Contains("- [x] **Step 1:** Design application architecture", markdown);
        Assert.Contains("*Completed:* Architecture designed with microservices approach", markdown);
        Assert.Contains("## Context from Completed Subtasks", markdown);
        Assert.Contains("**Step1_architecture:** microservices", markdown);

        // Complete Step 2
        var step2Context = new Dictionary<string, object>
        {
            ["features"] = new[] { "user_auth", "data_processing", "reporting" },
            ["linesOfCode"] = 2500,
            ["modules"] = 8
        };
        taskState = await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 2, "Core features implemented successfully", 
            "Implemented 3 main features across 8 modules", step2Context);
        
        markdown = taskState.ToMarkdown();
        Assert.Contains("**Progress:** 2/5 subtasks completed", markdown);
        Assert.Contains("- [x] **Step 2:** Implement core features", markdown);
        Assert.Contains("*Completed:* Core features implemented successfully", markdown);
        Assert.Contains("**Step2_linesOfCode:** 2500", markdown);

        // Complete remaining steps
        await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 3, "Comprehensive test suite completed", 
            "Achieved 95% code coverage", 
            new Dictionary<string, object> { ["coverage"] = "95%", ["tests"] = 147 });
            
        await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 4, "Staging deployment successful", 
            "Application running stable in staging", 
            new Dictionary<string, object> { ["stagingUrl"] = "https://staging.example.com", ["status"] = "healthy" });
            
        taskState = await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 5, "Production deployment completed", 
            "Application successfully deployed to production", 
            new Dictionary<string, object> { ["productionUrl"] = "https://app.example.com", ["users"] = 0 });

        // Final verification
        markdown = taskState.ToMarkdown();
        Assert.Contains("**Status:** Completed", markdown);
        Assert.Contains("**Progress:** 5/5 subtasks completed", markdown);
        Assert.DoesNotContain("- [ ]", markdown); // No unchecked boxes
        Assert.Contains("**Step5_productionUrl:** https://app.example.com", markdown);
        
        // Verify all completion summaries are present
        var completionCount = markdown.Split("*Completed:*").Length - 1;
        Assert.Equal(5, completionCount);
        
        // The markdown should tell a complete story of the workflow
        Assert.Contains("Build and deploy a web application", markdown);
        Assert.Contains("microservices", markdown);
        Assert.Contains("2500", markdown); // lines of code
        Assert.Contains("95%", markdown); // test coverage
        Assert.Contains("staging.example.com", markdown);
        Assert.Contains("app.example.com", markdown);
    }
}