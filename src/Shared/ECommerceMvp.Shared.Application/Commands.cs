namespace ECommerceMvp.Shared.Application;

/// <summary>
/// Base interface for all commands.
/// </summary>
public interface ICommand<TResponse>
{
    // Marker interface for command type safety
}

/// <summary>
/// Base interface for command handlers.
/// </summary>
public interface ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata for command execution context.
/// </summary>
public class CommandContext
{
    public CommandContext(
        string correlationId,
        string requestId,
        string? tenantId = null,
        string? userId = null,
        string? causationId = null)
    {
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        RequestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
        TenantId = tenantId;
        UserId = userId;
        CausationId = causationId;
    }

    public string CorrelationId { get; }
    public string RequestId { get; }
    public string? TenantId { get; }
    public string? UserId { get; }
    public string? CausationId { get; }
    public DateTimeOffset IssuedAt { get; } = DateTimeOffset.UtcNow;
}
