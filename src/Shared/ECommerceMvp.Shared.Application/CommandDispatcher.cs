using Microsoft.Extensions.DependencyInjection;

namespace ECommerceMvp.Shared.Application;

/// <summary>
/// Default command dispatcher implementation using dependency injection.
/// Dispatches commands to their registered handlers.
/// </summary>
public class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Dispatches a command to its appropriate handler.
    /// </summary>
    /// <typeparam name="TCommand">The command type</typeparam>
    /// <typeparam name="TResponse">The response type</typeparam>
    /// <param name="command">The command to dispatch</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>The handler response</returns>
    public async Task<TResponse> DispatchAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResponse>
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(typeof(TCommand), typeof(TResponse));
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
            throw new InvalidOperationException(
                $"No handler registered for command type {typeof(TCommand).Name}");

        var handleMethod = handlerType.GetMethod("HandleAsync");
        if (handleMethod == null)
            throw new InvalidOperationException(
                $"Handler for command type {typeof(TCommand).Name} does not have a HandleAsync method");

        var result = handleMethod.Invoke(handler, new object?[] { command, cancellationToken });
        if (result is Task<TResponse> task)
        {
            return await task.ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Handler for command type {typeof(TCommand).Name} did not return a Task<{typeof(TResponse).Name}>");
    }
}
