using System.Threading.Channels;
using LifeAgent.Core;
using LifeAgent.Core.Events;
using LifeAgent.Core.Memory;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Orchestrator;

/// <summary>
/// The runtime host for the orchestrator. Responsibilities:
///   1. Replay events on startup to rebuild <see cref="LifeAgentState"/>
///   2. Listen for new events via <see cref="Channel{T}"/>
///   3. Process each event through <see cref="LifeAgentOrchestrator"/>
///   4. Persist emitted events to the event store
///   5. Execute emitted actions (delegate, notify, approve, retry)
///
/// The event channel is the single entry point for all task submissions —
/// CLI input, audio pipeline, scheduler, and webhooks all write to it.
/// </summary>
public sealed class OrchestratorService : BackgroundService
{
    private readonly LifeAgentOrchestrator _orchestrator;
    private readonly IEventStore _eventStore;
    private readonly LifeAgentState _state;
    private readonly Channel<LifeEvent> _eventChannel;
    private readonly IReadOnlyDictionary<string, IWorkerAgent> _workers;
    private readonly INotificationChannel _notificationChannel;
    private readonly ILogger<OrchestratorService> _logger;

    // Pending retries tracked for delayed re-enqueue
    private readonly PriorityQueue<(string TaskId, LifeEvent RetryEvent), DateTimeOffset> _retryQueue = new();

    public OrchestratorService(
        LifeAgentOrchestrator orchestrator,
        IEventStore eventStore,
        LifeAgentState state,
        Channel<LifeEvent> eventChannel,
        IEnumerable<IWorkerAgent> workers,
        INotificationChannel notificationChannel,
        ILogger<OrchestratorService> logger)
    {
        _orchestrator = orchestrator;
        _eventStore = eventStore;
        _state = state;
        _eventChannel = eventChannel;
        _workers = workers.ToDictionary(w => w.WorkerType, w => w);
        _notificationChannel = notificationChannel;
        _logger = logger;
    }

    /// <summary>
    /// Provides the channel writer for external components to submit events.
    /// </summary>
    public ChannelWriter<LifeEvent> EventWriter => _eventChannel.Writer;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Orchestrator starting — replaying event log...");

        // Phase 1: Rebuild state from event store
        await ReplayEventsAsync(ct);

        _logger.LogInformation(
            "State rebuilt: {TaskCount} active tasks, budget ${Spent:F2}/${Limit:F2}, sequence #{Seq}",
            _state.ActiveTaskCount, _state.Budget.SpentUsd, _state.Budget.LimitUsd,
            _state.LastAppliedSequence);

        // Phase 2: Process incoming events
        _logger.LogInformation("Orchestrator ready — listening for events");

        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessEventAsync(evt, ct);

                // Check for any retries that are now due
                await ProcessDueRetriesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventType} ({EventId})",
                    evt.GetType().Name, evt.EventId);
            }
        }

        _logger.LogInformation("Orchestrator shutting down");
    }

    private async Task ReplayEventsAsync(CancellationToken ct)
    {
        await foreach (var (sequence, evt) in _eventStore.ReadFromAsync(0, ct))
        {
            _state.Apply(evt, sequence);
        }
    }

    private async Task ProcessEventAsync(LifeEvent evt, CancellationToken ct)
    {
        // 1. Persist the event
        await _eventStore.AppendAsync(evt, ct);
        var sequence = await _eventStore.GetLatestSequenceAsync(ct);

        // 2. Apply to state
        _state.Apply(evt, sequence);

        // 3. Run through orchestrator
        var decision = await _orchestrator.ProcessAsync(_state, evt, ct);

        // 4. Persist any new events
        if (decision.Events.Count > 0)
        {
            await _eventStore.AppendBatchAsync(decision.Events, ct);
            var seq = await _eventStore.GetLatestSequenceAsync(ct);
            foreach (var newEvt in decision.Events)
            {
                _state.Apply(newEvt, seq++);
            }
        }

        // 5. Execute actions
        foreach (var action in decision.Actions)
        {
            await ExecuteActionAsync(action, ct);
        }
    }

    private async Task ExecuteActionAsync(AgentAction action, CancellationToken ct)
    {
        switch (action)
        {
            case DelegateToWorker d:
                _ = Task.Run(() => ExecuteWorkerAsync(d.TaskId, d.WorkerType, ct), ct);
                break;

            case NotifyUser n:
                await _notificationChannel.SendAsync(n.Message, n.Urgency, ct);
                break;

            case RequestHumanApproval r:
                _ = Task.Run(() => HandleApprovalRequestAsync(r, ct), ct);
                break;

            case RetryTask r:
                var retryAt = DateTimeOffset.UtcNow + r.Delay;
                var task = _state.GetTask(r.TaskId);
                if (task is not null)
                {
                    _retryQueue.Enqueue(
                        (r.TaskId, new TaskCreated(task)),
                        retryAt);
                    _logger.LogDebug("Scheduled retry for {TaskId} at {RetryAt}", r.TaskId, retryAt);
                }
                break;

            case CreateSubTasks s:
                foreach (var sub in s.SubTasks)
                {
                    await _eventChannel.Writer.WriteAsync(new TaskCreated(sub), ct);
                }
                break;

            case CancelTask c:
                _logger.LogInformation("Cancelling task {TaskId}: {Reason}", c.TaskId, c.Reason);
                break;

            case UpdateTrustLevel u:
                _state.UserProfile.DomainTrust[u.Domain] = u.NewLevel;
                _logger.LogInformation("Trust updated: {Domain} → {Level}", u.Domain, u.NewLevel);
                break;

            case ThrottleProactivity t:
                _logger.LogInformation("Proactivity throttled for {Duration}: {Reason}",
                    t.Duration, t.Reason);
                break;

            default:
                _logger.LogWarning("Unknown action type: {Type}", action.GetType().Name);
                break;
        }
    }

    private async Task ExecuteWorkerAsync(string taskId, string workerType, CancellationToken ct)
    {
        var task = _state.GetTask(taskId);
        if (task is null)
        {
            _logger.LogWarning("Task {TaskId} not found for worker execution", taskId);
            return;
        }

        if (!_workers.TryGetValue(workerType, out var worker))
        {
            _logger.LogWarning("Worker {WorkerType} not found", workerType);
            await _eventChannel.Writer.WriteAsync(
                new TaskFailed(taskId, $"Worker '{workerType}' not registered", task.RetryCount), ct);
            return;
        }

        _logger.LogInformation("Executing {TaskId} via {Worker}...", taskId, workerType);

        try
        {
            var result = await worker.ExecuteAsync(task, _state.UserProfile, ct);
            await _eventChannel.Writer.WriteAsync(new TaskCompleted(taskId, result), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker {Worker} failed on {TaskId}", workerType, taskId);
            await _eventChannel.Writer.WriteAsync(
                new TaskFailed(taskId, ex.Message, task.RetryCount + 1), ct);
        }
    }

    private async Task HandleApprovalRequestAsync(RequestHumanApproval request, CancellationToken ct)
    {
        try
        {
            var response = await _notificationChannel.RequestApprovalAsync(
                request.TaskId, request.Question, request.Timeout, ct);

            if (response is not null)
            {
                await _eventChannel.Writer.WriteAsync(
                    new HumanApprovalReceived(request.TaskId, response.Approved, response.Comment), ct);
            }
            else
            {
                await _eventChannel.Writer.WriteAsync(
                    new HumanApprovalTimedOut(request.TaskId, request.FallbackAction), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approval request failed for {TaskId}", request.TaskId);
            await _eventChannel.Writer.WriteAsync(
                new HumanApprovalTimedOut(request.TaskId, request.FallbackAction), ct);
        }
    }

    private async Task ProcessDueRetriesAsync(CancellationToken ct)
    {
        while (_retryQueue.Count > 0 && _retryQueue.TryPeek(out var item, out var dueAt))
        {
            if (dueAt > DateTimeOffset.UtcNow) break;

            _retryQueue.Dequeue();
            _logger.LogInformation("Retry due for task {TaskId}", item.TaskId);
            await _eventChannel.Writer.WriteAsync(item.RetryEvent, ct);
        }
    }
}
