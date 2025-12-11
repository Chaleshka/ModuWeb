using System.Reflection;
using System.Runtime.Loader;
using ModuWeb.ModuleMessenger;

namespace ModuWeb.Events
{
    public class ModuleMessageSentEventArgs : EventArgs
    {
        public ModuleMessageSentEventArgs(ModuleMessage msg)
        {
            Message = msg;
            To = Message.To;
            From = Message.From;
            MessageId = Message.MessageId;
            RespondTo = Message.RespondTo == 0 ? null : Message.RespondTo;
        }

        public string To { get; }
        public string From { get; }
        public ModuleMessage Message { get; }
        public ulong MessageId { get; }
        public ulong? RespondTo { get; }
    }
}
