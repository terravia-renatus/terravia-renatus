namespace Terravia.Domain;

/// <summary>
/// Standardized command gateway through which Players, Sitters, and Utility AI Bots dispatch all actions.
/// </summary>
public interface IGameCommandGateway
{
    Task<CommandResult> DispatchAsync<TCommand>(
        TCommand command,
        SitterPermissions? sitterPermissions = null,
        CancellationToken cancellationToken = default)
        where TCommand : notnull;
}
