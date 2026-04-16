using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using ResearchAgent.Core.Memory;

namespace ResearchAgent.App;

/// <summary>
/// An <see cref="AIContextProvider"/> that surfaces the current research state
/// (question, plan, findings, reflections, progress) as transient
/// <see cref="AIContext.Instructions"/> on every sub-agent invocation (P2.3).
///
/// <para><b>Why this exists.</b> Before P2.3 the orchestrator duplicated the Planner's
/// output into every Researcher / Analyst user-message payload, and downstream agents
/// had to call the <c>GetAllResearchContext</c> tool to see findings. Moving the state
/// into <see cref="AIContext.Instructions"/> means:</para>
///
/// <list type="bullet">
/// <item>All sub-agents that receive the provider see a consistent view without extra
/// tool calls — the state is available on the *first* model turn, not after a
/// round-trip through a no-op tool.</item>
/// <item>The payload is <b>transient</b>: it's merged into the system prompt for the
/// current invocation only and never persisted into <see cref="AIContext.Messages"/>,
/// so compaction strategies (P2.2) never need to shrink it.</item>
/// <item>The injection is <b>pull-model</b>: the provider calls back into
/// <see cref="ResearchMemory"/> at invocation time, so agents always see the latest
/// findings from prior iterations without orchestrator-level marshaling.</item>
/// </list>
///
/// <para><b>Opt-in.</b> Controlled by <c>AI:ResearchContextProvider:Enabled</c> (default false).
/// Ships dark so we can A/B against the current user-message payload shape before removing
/// the redundancy.</para>
///
/// <para><b>Safety.</b> <see cref="ProvideAIContextAsync"/> never throws on memory access;
/// any exception is logged at warning level and an empty context is returned. This keeps
/// a memory-side bug from bringing down the whole pipeline.</para>
/// </summary>
public sealed class ResearchStateContextProvider : AIContextProvider
{
    private readonly ResearchMemory _memory;
    private readonly ILogger _logger;
    private readonly bool _includeFindings;

    /// <param name="memory">The live session memory; state is pulled lazily at invocation time.</param>
    /// <param name="logger">Logger for provider-side diagnostics. Unexpected errors are logged at Warning.</param>
    /// <param name="includeFindings">
    /// When true (default) the provider includes the full findings/sources/reflections summary
    /// in the instructions. Set false for agents whose scope is narrow enough that the tool-based
    /// pull (<c>GetAllResearchContext</c>) suffices — leaving space for the agent's own reasoning.
    /// </param>
    public ResearchStateContextProvider(ResearchMemory memory, ILogger logger, bool includeFindings = true)
    {
        _memory = memory;
        _logger = logger;
        _includeFindings = includeFindings;
    }

    /// <inheritdoc />
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var instructions = Render();
            if (string.IsNullOrWhiteSpace(instructions))
                return new ValueTask<AIContext>(new AIContext());

            return new ValueTask<AIContext>(new AIContext
            {
                Instructions = instructions,
            });
        }
        catch (Exception ex)
        {
            // Never fail the invocation because of context assembly — log and emit empty.
            _logger.LogWarning(ex, "ResearchStateContextProvider failed to assemble instructions; emitting empty context.");
            return new ValueTask<AIContext>(new AIContext());
        }
    }

    /// <summary>
    /// Build the instructions block. Exposed <c>internal</c> so tests can assert the exact
    /// rendered shape without constructing the MAF invocation plumbing.
    /// </summary>
    internal string Render()
    {
        var sections = new List<string>();

        var question = _memory.ResearchQuestion;
        if (!string.IsNullOrWhiteSpace(question))
        {
            sections.Add("# Active Research Session");
            sections.Add("## Research Question");
            sections.Add(question.Trim());
        }

        var plan = _memory.PlannerOutput;
        if (!string.IsNullOrWhiteSpace(plan))
        {
            sections.Add("## Research Plan (from Planner)");
            sections.Add(plan.Trim());
        }

        if (_includeFindings)
        {
            var summary = _memory.BuildContextSummary();
            if (!string.IsNullOrWhiteSpace(summary))
            {
                sections.Add("## Accumulated Research State");
                sections.Add(summary.Trim());
            }
        }

        if (sections.Count == 0) return string.Empty;

        // Final footer — keep the agent honest about the *source* of this context.
        sections.Add("_(The above was injected by ResearchStateContextProvider and reflects the latest session state. It is transient context — do not cite it as an external source.)_");

        return string.Join("\n\n", sections);
    }
}
