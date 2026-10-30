using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;

namespace Sanctuary.Game;

public interface IChatCommandManager
{
    string Prefix { get; }

    ReadOnlyCollection<IChatCommandView> Commands { get; }

    bool Load();

    /// <summary>
    /// Attempts to handle a chat command.
    /// </summary>
    /// <param name="invoker">The player invoking the command.</param>
    /// <param name="command">The command string.</param>
    /// <returns>True if the command was handled; otherwise, false.</returns>
    bool TryHandle(Player invoker, string command);

    /// <summary>
    /// Runs a command for a referee who isn't in the world (in the admin panel) on a player who is, as if the player had
    /// typed it, so a command's [player] left out means them. Whether it may run is <paramref name="role"/>, the
    /// referee's, never the player's; what it answers goes to <paramref name="replies"/>, not to the player, and the
    /// audit log names the referee.
    /// </summary>
    /// <returns>True if there's such a command; otherwise, false.</returns>
    bool TryHandleFor(Player subject, ChatCommandRole role, string referee, string command, ConcurrentQueue<string> replies);

    void LogAction(IChatCommand command, Player invoker, string action, string? targetName, string? detail);
}