using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

/// <summary>
/// An agent candidate produced by the infrastructure query used during triage.
/// Contains just enough data to evaluate eligibility and open-ticket load.
/// </summary>
public sealed record AgentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlyList<int> SpecializationCategoryIds);

/// <summary>
/// The triage decision: what priority, due date, and agent the ticket should be given.
/// </summary>
public sealed record TriageDecision(
    TicketPriority Priority,
    string PriorityReason,
    TimeSpan SlaWindow,
    string SlaWindowReason,
    int? AssignedAgentId,
    string AssignmentReason);

/// <summary>
/// Applies BR-1 through BR-5:
/// priority override for forcing categories (BR-1), SLA window computation for tier and
/// priority (BR-2, BR-3), and agent selection by eligibility and load (BR-4, BR-5).
/// </summary>
public sealed class TriageService(SlaOptions options)
{
    /// <summary>
    /// Decides what priority, due date, and agent to give a ticket based on its category and
    /// customer tier. No I/O: everything that matters is passed in.
    /// </summary>
    public TriageDecision Decide(
        TicketPriority requestedPriority,
        Category category,
        Customer customer,
        IReadOnlyList<AgentCandidate> candidates)
    {
        // BR-1: categories that force Critical priority override the requested priority.
        var priority = category.ForcesCriticalPriority ? TicketPriority.Critical : requestedPriority;
        var priorityReason = category.ForcesCriticalPriority
            ? $"Category '{category.Name}' forces Critical priority."
            : $"Priority set to {priority} as requested.";

        // BR-2 + BR-3: SLA window for this priority / tier combination.
        var window = options.GetWindow(priority, customer.Tier);
        var windowReason = customer.Tier == CustomerTier.Premium
            ? $"{priority} SLA for Premium customer: {window.TotalHours:0.#} hours (half of the base window)."
            : $"{priority} SLA: {window.TotalHours:0.#} hours.";

        // BR-4: when the category requires a specialist, only agents with that specialization
        //        are eligible.
        // BR-5: only active agents strictly below their MaxOpenTickets cap are eligible.
        //        Select the one with the fewest open tickets; break ties deterministically by id.
        var eligible = candidates
            .Where(a => a.IsActive)
            .Where(a => a.OpenTicketCount < a.MaxOpenTickets)
            .Where(a => !category.RequiresSpecialist || a.SpecializationCategoryIds.Contains(category.Id));

        var selected = eligible
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.Id)
            .FirstOrDefault();

        string assignmentReason;
        if (selected is not null)
        {
            assignmentReason = $"Assigned to {selected.FullName} ({selected.OpenTicketCount} open tickets).";
        }
        else if (category.RequiresSpecialist)
        {
            assignmentReason = $"No active specialist for '{category.Name}' with available capacity. Ticket left unassigned.";
        }
        else
        {
            assignmentReason = "No active agents with available capacity. Ticket left unassigned.";
        }

        return new TriageDecision(priority, priorityReason, window, windowReason, selected?.Id, assignmentReason);
    }
}
