using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Body of POST /api/tickets.</summary>
public sealed record CreateTicketRequest(
    string Title,
    string Description,
    int CustomerId,
    int CategoryId,
    TicketPriority? RequestedPriority);

/// <summary>Body of PATCH /api/tickets/{id}/status.</summary>
public sealed record UpdateTicketStatusRequest(TicketStatus Status);

/// <summary>Body of PATCH /api/tickets/{id}/assignment. A null agent unassigns the ticket.</summary>
public sealed record AssignTicketRequest(int? AgentId);

/// <summary>Body of POST /api/tickets/{id}/escalate. The actor comes from the JWT, not this body.</summary>
public sealed record EscalateTicketRequest(string Reason);

/// <summary>
/// The outcome of triage: what priority was assigned, the SLA window, and why each
/// decision was made. Returned as part of the ticket detail after creation.
/// </summary>
public sealed class TriageResultDto
{
    public TicketPriority Priority { get; set; }
    public string PriorityReason { get; set; } = string.Empty;
    public double SlaWindowHours { get; set; }
    public string SlaWindowReason { get; set; } = string.Empty;
    public string AssignmentReason { get; set; } = string.Empty;
}

/// <summary>One row in a ticket's escalation history.</summary>
public sealed class EscalationDto
{
    public int Id { get; set; }
    public TicketPriority FromPriority { get; set; }
    public TicketPriority ToPriority { get; set; }
    public AgentSummaryDto? FromAgent { get; set; }
    public AgentSummaryDto? ToAgent { get; set; }
    public DateTime? FromDueAtUtc { get; set; }
    public DateTime? ToDueAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string EscalatedBy { get; set; } = string.Empty;
    public DateTime EscalatedAtUtc { get; set; }
}

