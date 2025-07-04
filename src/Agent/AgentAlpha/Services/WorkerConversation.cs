using System;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;

namespace AgentAlpha.Services;

/// <summary>
/// Light-weight ConversationManager variant used for P5 workers.
/// No markdown tracking, small context window.
/// </summary>
public class WorkerConversation : ConversationManager
{
    public WorkerConversation(IOpenAIResponsesService openAi,
                              ILogger<ConversationManager> log,
                              AgentConfiguration cfg,
                              ISessionActivityLogger activityLogger, // <-- ADD
                              IServiceProvider serviceProvider)
        : base(openAi, log, cfg, activityLogger, serviceProvider) // <-- FIX
    {
        // Clamp the context size to minimise token usage
        cfg.MaxConversationMessages = Math.Min(cfg.MaxConversationMessages, 16);
    }

    /* override markdown behaviour – workers do not persist any */
    public override string GetTaskMarkdown() => string.Empty;
    public override Task UpdateMarkdownAsync(string _, bool __ = false) => Task.CompletedTask;
}
