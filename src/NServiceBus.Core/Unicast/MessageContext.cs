namespace NServiceBus.Unicast
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Transports;

    /// <summary>
    /// Implementation of IMessageContext.
    /// </summary>
    public class MessageContext : IMessageContext
    {
        IncomingMessage incomingMessage;

        /// <summary>
        /// Initializes message context from the transport message.
        /// </summary>
        [ObsoleteEx(RemoveInVersion = "7.0", TreatAsErrorFromVersion = "6.0", ReplacementTypeOrMember = "MessageContext(IncomingMessage incomingMessage)")]
        public MessageContext(TransportMessage incomingMessage)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Initializes message context from the incoming message.
        /// </summary>
        public MessageContext(IncomingMessage incomingMessage)
        {
            this.incomingMessage = incomingMessage;
        }

        IDictionary<string, string> IMessageContext.Headers => incomingMessage.Headers;

        /// <summary>
        /// The time at which the incoming message was sent.
        /// </summary>
        public DateTime TimeSent
        {
            get
            {
                string timeSent;
                if (incomingMessage.Headers.TryGetValue(Headers.TimeSent, out timeSent))
                {
                    return DateTimeExtensions.ToUtcDateTime(timeSent);
                }

                return DateTime.MinValue;
            }
        }

        string IMessageContext.Id => incomingMessage.MessageId;

        string IMessageContext.ReplyToAddress => incomingMessage.GetReplyToAddress();
    }
}
