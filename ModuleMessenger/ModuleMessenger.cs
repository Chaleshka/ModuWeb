using System.Collections.Concurrent;
using System.Runtime.Loader;
using ModuWeb.Events;

namespace ModuWeb.ModuleMessenger;

/// <summary>
/// Handles inter-module messaging and removes registrations with their owning load context.
/// </summary>
public class ModuleMessenger
{
    private sealed record HandlerRegistration(Action<ModuleMessage> Handler, AssemblyLoadContext Context);

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<AssemblyLoadContext, HandlerRegistration>> Handlers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<ulong, TaskCompletionSource<ModuleMessage>> PendingResponses = new();

    /// <summary>
    /// Registers a handler owned by the module instance that supplied it.
    /// </summary>
    public static void Subscribe(Action<ModuleMessage> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var module = handler.Target as ModuleBase
            ?? throw new ArgumentException("A module message handler must be an instance method of ModuleBase.", nameof(handler));
        var context = AssemblyLoadContext.GetLoadContext(module.GetType().Assembly)
            ?? throw new InvalidOperationException("The module handler assembly has no load context.");

        var moduleHandlers = Handlers.GetOrAdd(module.ModuleName, static _ => new());
        moduleHandlers[context] = new HandlerRegistration(handler, context);
    }

    public static void SendMessage(ModuleMessage msg)
    {
        ArgumentNullException.ThrowIfNull(msg);
        var handled = false;

        if (msg.RespondTo != 0 && PendingResponses.TryRemove(msg.RespondTo, out var pending))
        {
            pending.TrySetResult(msg);
            handled = true;
        }

        var moduleHandlers = Handlers
            .Where(entry => msg.To.Equals(entry.Key, StringComparison.OrdinalIgnoreCase) ||
                            msg.To.StartsWith(entry.Key + ".", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Key.Length)
            .Select(entry => entry.Value)
            .FirstOrDefault();
        if (!handled && moduleHandlers is not null)
        {
            var registration = moduleHandlers.Values.FirstOrDefault(candidate =>
                ModuleManager.Instance.IsModuleContextActive(candidate.Context));
            if (registration is not null)
            {
                registration.Handler(msg);
                handled = true;
            }
        }

        Events.Events.ModuleMessageSentSafeEvent.Invoke(new ModuleMessageSentEventArgs(msg));
        if (!handled)
            Logger.Warn($"Module '{msg.To}' not found. Message from '{msg.From}' was dropped.");
    }

    public static async Task<ModuleMessage> SendAndWaitAsync(ModuleMessage msg, int timeoutInS = 2)
    {
        ArgumentNullException.ThrowIfNull(msg);
        if (timeoutInS <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutInS));

        var tcs = new TaskCompletionSource<ModuleMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingResponses[msg.MessageId] = tcs;
        try
        {
            SendMessage(msg);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInS));
            using var registration = cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));
            try
            {
                return await tcs.Task;
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException($"No response to message {msg.MessageId} within {timeoutInS} seconds.");
            }
        }
        finally
        {
            PendingResponses.TryRemove(msg.MessageId, out _);
        }
    }

    internal static void RemoveModuleHandlers(AssemblyLoadContext context)
    {
        foreach (var moduleHandlers in Handlers.ToArray())
        {
            moduleHandlers.Value.TryRemove(context, out _);
            if (moduleHandlers.Value.IsEmpty)
                Handlers.TryRemove(new KeyValuePair<string, ConcurrentDictionary<AssemblyLoadContext, HandlerRegistration>>(moduleHandlers.Key, moduleHandlers.Value));
        }
    }
}
