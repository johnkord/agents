using AgentAlpha.Models;

namespace AgentAlpha.Services;

/// <summary>
/// Service for parsing command-line arguments into task execution requests
/// </summary>
public class CommandLineParser
{
    /// <summary>
    /// Parses command-line arguments into a TaskExecutionRequest
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>Parsed TaskExecutionRequest</returns>
    public TaskExecutionRequest ParseArguments(string[] args)
    {
        if (args.Length == 0)
        {
            // Interactive mode
            var task = PromptForTask();
            return TaskExecutionRequest.FromTask(task);
        }

        return ParseArgumentsInternal(args);
    }

    private TaskExecutionRequest ParseArgumentsInternal(string[] args)
    {
        var request = new TaskExecutionRequest();
        var taskParts = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            switch (arg.ToLowerInvariant())
            {
                case "--model" or "-m":
                    request.Model = GetNextArgument(args, ref i);
                    break;
                    
                case "--temperature" or "-t":
                    if (double.TryParse(GetNextArgument(args, ref i), out var temp))
                    {
                        request.Temperature = Math.Clamp(temp, 0.0, 1.0);
                    }
                    break;
                    
                case "--max-iterations" or "--iterations":
                    if (int.TryParse(GetNextArgument(args, ref i), out var iterations))
                    {
                        request.MaxIterations = Math.Max(1, iterations);
                    }
                    break;
                    
                case "--priority":
                    if (Enum.TryParse<TaskPriority>(GetNextArgument(args, ref i), true, out var priority))
                    {
                        request.Priority = priority;
                    }
                    break;
                    
                case "--timeout":
                    if (int.TryParse(GetNextArgument(args, ref i), out var timeoutMinutes))
                    {
                        request.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
                    }
                    break;
                    
                case "--verbose" or "-v":
                    request.VerboseLogging = true;
                    break;
                    
                case "--system-prompt":
                    request.SystemPrompt = GetNextArgument(args, ref i);
                    break;
                    
                case "--session" or "--session-id":
                    request.SessionId = GetNextArgument(args, ref i);
                    break;
                    
                case "--session-name":
                    request.SessionName = GetNextArgument(args, ref i);
                    break;
                    
                default:
                    // Part of the task description
                    taskParts.Add(arg);
                    break;
            }
        }

        request.Task = string.Join(" ", taskParts);
        
        LogParsedRequest(request);
        
        return request;
    }

    private string GetNextArgument(string[] args, ref int currentIndex)
    {
        if (currentIndex + 1 < args.Length)
        {
            return args[++currentIndex];
        }
        return string.Empty;
    }

    private void LogParsedRequest(TaskExecutionRequest request)
    {
        if (request.VerboseLogging && !string.IsNullOrEmpty(request.Task))
        {
            Console.WriteLine($"🔍 Parsed request - Task: '{request.Task}', Model: {request.Model ?? "default"}, Temperature: {request.Temperature?.ToString() ?? "default"}");
        }
    }

    private string PromptForTask()
    {
        Console.Write("Enter a task for the agent: ");
        return Console.ReadLine() ?? "";
    }
}