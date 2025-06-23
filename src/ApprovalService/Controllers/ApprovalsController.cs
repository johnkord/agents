using Microsoft.AspNetCore.Mvc;
using MCPServer.ToolApproval;

namespace ApprovalService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalStore _approvalStore;

    public ApprovalsController(IApprovalStore approvalStore)
    {
        _approvalStore = approvalStore;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitApprovalRequest([FromBody] ApprovalRequest request)
    {
        await _approvalStore.StoreApprovalRequestAsync(request);
        return Ok(new { message = "Approval request submitted", id = request.Id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetApprovalStatus(Guid id)
    {
        var request = await _approvalStore.GetApprovalRequestAsync(id);
        if (request == null)
        {
            return NotFound();
        }

        return Ok(new { 
            id = request.Id,
            toolName = request.ToolName,
            arguments = request.Arguments,
            createdAt = request.CreatedAt,
            status = request.Status.ToString()
        });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid id)
    {
        var success = await _approvalStore.UpdateApprovalStatusAsync(id, ApprovalStatus.Approved);
        if (!success)
        {
            return NotFound();
        }

        return Ok(new { message = "Request approved" });
    }

    [HttpPost("{id}/deny")]
    public async Task<IActionResult> DenyRequest(Guid id)
    {
        var success = await _approvalStore.UpdateApprovalStatusAsync(id, ApprovalStatus.Denied);
        if (!success)
        {
            return NotFound();
        }

        return Ok(new { message = "Request denied" });
    }

    [HttpGet]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var pendingRequests = await _approvalStore.GetPendingApprovalsAsync();
        return Ok(pendingRequests.Select(r => new {
            id = r.Id,
            toolName = r.ToolName,
            arguments = r.Arguments,
            createdAt = r.CreatedAt,
            status = r.Status.ToString()
        }));
    }
}

public class ApprovalRequest
{
    public Guid Id { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object?> Arguments { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
}