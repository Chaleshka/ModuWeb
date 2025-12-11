namespace ModuWeb.ModuleMessenger
{
    /// <summary>
    /// Provides a message for inter-module communication.
    /// </summary>
    public sealed class ModuleMessage
    {
        private static ulong _messageCounter = 0;
        internal ulong MessageId { get; } = Interlocked.Increment(ref _messageCounter);
        internal ulong RespondTo { get; } = 0;

        /// <summary>
        /// The name of the target module that should receive this message.
        /// </summary>
        public readonly string To;

        /// <summary>
        /// The name of the source module that sent this message.
        /// </summary>
        public readonly string From;

        /// <summary>
        /// Arbitrary key-value data carried by the message.
        /// </summary>
        public readonly IReadOnlyDictionary<string, object> Data;

        /// <summary>
        /// Creates a new outbound message from one module to another.
        /// </summary>
        /// <param name="to">Target module name.</param>
        /// <param name="from">Sender module name.</param>
        /// <param name="data">Message data payload.</param>
        public ModuleMessage(string to, string from, Dictionary<string, object> data)
        {
            To = to;
            From = from;
            Data = data;
        }
        internal ModuleMessage(string to, string from, Dictionary<string, object> data, ulong respondTo)
        {
            To = to;
            From = from;
            Data = data;
            RespondTo = respondTo;
        }

        /// <summary>
        /// Sends a response message back to the original sender.
        /// </summary>
        /// <param name="data">Data payload to include in the response.</param>
        public void Reply(Dictionary<string, object> data)
        {
            var msg = new ModuleMessage(this.From, this.To, data, this.MessageId);
            ModuleMessenger.SendMessage(msg);
        }
    }
}
