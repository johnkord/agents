using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MCPServer.ToolApproval;

namespace MCPServer.Tests.ToolApproval
{
    /// <summary>
    /// Test approval provider that allows programmatic control over approval responses
    /// for testing purposes.
    /// </summary>
    public class TestApprovalProvider : IApprovalProvider
    {
        private readonly Queue<bool> _approvalResponses = new();
        private readonly Queue<TimeSpan> _responseDelays = new();
        private bool _shouldTimeout = false;
        private TimeSpan _timeoutDelay = TimeSpan.FromSeconds(30);

        public string ProviderName => "Test";

        public List<ApprovalInvocationToken> ReceivedTokens { get; } = new();

        /// <summary>
        /// Queue an approval response (true for approve, false for deny)
        /// </summary>
        public void QueueApprovalResponse(bool approved, TimeSpan delay = default)
        {
            _approvalResponses.Enqueue(approved);
            _responseDelays.Enqueue(delay);
        }

        /// <summary>
        /// Configure the provider to timeout instead of returning a response
        /// </summary>
        public void ConfigureTimeout(TimeSpan timeoutDelay)
        {
            _shouldTimeout = true;
            _timeoutDelay = timeoutDelay;
        }

        /// <summary>
        /// Reset the provider to default state
        /// </summary>
        public void Reset()
        {
            _approvalResponses.Clear();
            _responseDelays.Clear();
            ReceivedTokens.Clear();
            _shouldTimeout = false;
        }

        public async Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
        {
            ReceivedTokens.Add(token);

            if (_shouldTimeout)
            {
                await Task.Delay(_timeoutDelay, cancellationToken);
                throw new TimeoutException($"Test approval provider timed out for tool '{token.ToolName}'");
            }

            if (_approvalResponses.Count == 0)
            {
                throw new InvalidOperationException("No approval responses queued in test provider");
            }

            var delay = _responseDelays.Count > 0 ? _responseDelays.Dequeue() : TimeSpan.Zero;
            var approved = _approvalResponses.Dequeue();

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return approved;
        }
    }
}