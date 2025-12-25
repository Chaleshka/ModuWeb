using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using ModuWeb.Events;

namespace ModuWeb.ModuleMessenger
{
    /// <summary>
    /// Handles inter-module messaging, including one-way messages and request-response communication.
    /// </summary>
    public class ModuleMessenger
    {
        private static readonly ConcurrentDictionary<string, Action<ModuleMessage>> Handlers = new();
        private static readonly ConcurrentDictionary<ulong, TaskCompletionSource<ModuleMessage>> PendingResponses = new();

        /// <summary>
        /// Registers a message handler for the current module (using ModuleName).
        /// </summary>
        /// <param name="handler">The handler function that will process messages for this module.</param>
        public static void Subscribe(Action<ModuleMessage> handler)
        {
            Handlers[GetModuleDataByHandler(handler).Value.Module.ModuleName] = handler;
        }

        /// <summary>
        /// Sends a message to the specified module.
        /// If module not found, then message will be dropped.
        /// </summary>
        /// <param name="msg">The message instance to send.</param>
        public static void SendMessage(ModuleMessage msg)
        {
            bool handled = false;

            if (msg.RespondTo != 0)
            {
                if (PendingResponses.TryRemove(msg.RespondTo, out var pending))
                {
                    pending.TrySetResult(msg);
                    handled = true;
                }
            }

            if (!handled)
            {
                if (Handlers.TryGetValue(msg.To.Split('.')[0], out var handler) && InspectHandler(handler))
                {
                    handler.Invoke(msg);
                    handled = true;
                }
            }

            CallEvent(msg);

            if (!handled)
            {
                Logger.Warn($"Module '{msg.To}' not found. Message from '{msg.From}' was dropped.");
            }
        }

        private static void CallEvent(ModuleMessage msg)
        {  
            Events.Events.ModuleMessageSentSafeEvent.Invoke(new ModuleMessageSentEventArgs(msg));
        }

        /// <summary>
        /// Sends a message to the specified module and asynchronously waits for a response.
        /// </summary>
        /// <param name="msg">The message instance to send.</param>
        /// <param name="timeoutInS">The maximum time in seconds to wait for a response.</param>
        /// <returns>The response message from the target module.</returns>
        /// <exception cref="TimeoutException">Throw when the response was not received within the timeout period.</exception>
        public static async Task<ModuleMessage> SendAndWaitAsync(ModuleMessage msg, int timeoutInS = 2)
        {
            var tcs = new TaskCompletionSource<ModuleMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            PendingResponses[msg.MessageId] = tcs;
            SendMessage(msg);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInS));
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    return await tcs.Task;
                }
                catch (TaskCanceledException)
                {
                    PendingResponses.TryRemove(msg.MessageId, out _);
                    throw new TimeoutException($"No response to message {msg.MessageId} within {timeoutInS} seconds.");
                }
            }
        }

        private static KeyValuePair<string, (ModuleBase Module, AssemblyLoadContext Context)> GetModuleDataByHandler(Action<ModuleMessage> handler)
        {
            var method = handler.Method;
            var asm = method.Module.Assembly;
            var alc = AssemblyLoadContext.GetLoadContext(asm);
            var res = ModuleManager.Instance.modules.FirstOrDefault(f => f.Value.Context.Equals(alc));
            return res;
        }

        private static bool InspectHandler(Action<ModuleMessage> handler)
        {
            var res = GetModuleDataByHandler(handler);
            if (res.Key == default)
                return false;
            return true;
        }
    }
}
