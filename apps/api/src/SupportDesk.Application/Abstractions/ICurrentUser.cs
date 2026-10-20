namespace SupportDesk.Application.Abstractions;

/// <summary>The agent represented by the JWT on the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    int AgentId { get; }

    string FullName { get; }

    string Email { get; }
}
