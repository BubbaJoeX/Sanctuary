using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game;

public class ChatCommandManager : IChatCommandManager
{
    private readonly ILogger _logger;
    private readonly ILogger _auditLogger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, IChatCommand> _commands = [];

    // The admin panel referee a command runs for (TryHandleFor), for the audit log.
    private readonly AsyncLocal<string?> _panelReferee = new();

    public ChatCommandManager(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger<ChatCommandManager>();
        _auditLogger = loggerFactory.CreateLogger("Audit");
        _serviceProvider = serviceProvider;
    }

    public string Prefix { get; } = "!";

    public ReadOnlyCollection<IChatCommandView> Commands => _commands.Values.Cast<IChatCommandView>().ToList().AsReadOnly();

    public bool Load()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();

        foreach (var interactionType in executingAssembly.GetTypes())
        {
            if (interactionType.IsAbstract || interactionType.IsInterface)
                continue;

            if (!typeof(IChatCommand).IsAssignableFrom(interactionType))
                continue;

            var commandInstance = ActivatorUtilities.CreateInstance(_serviceProvider, interactionType) as IChatCommand;

            ArgumentNullException.ThrowIfNull(commandInstance);

            if (!_commands.TryAdd(commandInstance.KeyWord.ToLowerInvariant(), commandInstance))
            {
                _logger.LogWarning("Failed to add chat command: {prefix}{command}", Prefix, commandInstance.KeyWord);
                return false;
            }
        }

        _logger.LogInformation("Loaded {count} chat command(s).", _commands.Count);

        return true;
    }

    public bool TryHandle(Player invoker, string command)
    {
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
            return false;

        var keyWord = tokens[0][Prefix.Length..];

        if (!_commands.TryGetValue(keyWord.ToLowerInvariant(), out var cmd))
            return false;

        if (invoker.ChatCommandRole < cmd.RequiredRole)
        {
            ChatHelper.SendSystemMessage(invoker, $"You do not have permission to use this command.");
            return true;
        }

        var args = tokens[1..];

        var handled = cmd is IRawChatCommand rawCommand
            ? rawCommand.Handle(invoker, args, ChatCommandText.Remainder(command, 1))
            : cmd.Handle(invoker, args);

        if (!handled)
            ChatHelper.SendSystemMessage(invoker, $"Usage: {Prefix}{cmd.KeyWord} {cmd.Usage}");

        return true;
    }

    public bool TryHandleFor(Player subject, ChatCommandRole role, string referee, string command, ConcurrentQueue<string> replies)
    {
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0 || !_commands.TryGetValue(tokens[0].TrimStart(Prefix[0]).ToLowerInvariant(), out var cmd))
            return false;

        if (role < cmd.RequiredRole)
        {
            replies.Enqueue($"Your account can't use {Prefix}{cmd.KeyWord}.");
            return true;
        }

        var text = Prefix + command.TrimStart(Prefix[0]);
        var args = tokens[1..];

        // The admin API runs these one at a time, so the capture is this command's alone.
        subject.SystemMessageCapture = replies;
        subject.SystemMessageCaptureOnly = true;
        _panelReferee.Value = referee;

        try
        {
            var handled = cmd is IRawChatCommand rawCommand
                ? rawCommand.Handle(subject, args, ChatCommandText.Remainder(text, 1))
                : cmd.Handle(subject, args);

            if (!handled)
                replies.Enqueue($"Usage: {Prefix}{cmd.KeyWord} {cmd.Usage}");
        }
        finally
        {
            _panelReferee.Value = null;
            subject.SystemMessageCaptureOnly = false;
            subject.SystemMessageCapture = null;
        }

        return true;
    }

    public void LogAction(IChatCommand command, Player invoker, string action, string? targetName, string? detail)
    {
        if (_panelReferee.Value is { } referee)
        {
            _auditLogger.LogInformation("{Command}|{Action}|Actor: \"{Referee}\" (admin panel), For: \"{ForName}\" ({ForGuid}){Target}{Detail}",
                command.KeyWord,
                action,
                referee,
                invoker.Name,
                invoker.Guid,
                targetName is null ? string.Empty : $", Target: \"{targetName}\"",
                detail is null ? string.Empty : $", {detail}");
            return;
        }

        _auditLogger.LogInformation("{Command}|{Action}|Actor: \"{ActorName}\" ({ActorGuid}){Target}{Detail}",
            command.KeyWord,
            action,
            invoker.Name,
            invoker.Guid,
            targetName is null ? string.Empty : $", Target: \"{targetName}\"",
            detail is null ? string.Empty : $", {detail}"
        );
    }
}