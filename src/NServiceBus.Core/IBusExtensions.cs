namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Syntactic sugar for <see cref="IBus"/>.
    /// </summary>
    public static partial class IBusExtensions
    {
        /// <summary>
        /// Sends the message to the endpoint which sent the message currently being handled on this thread.
        /// </summary>
        /// <param name="bus">Object being extended.</param>
        /// <param name="message">The message to send.</param>
        public static Task ReplyAsync(this IBus bus, object message)
        {
            Guard.AgainstNull("bus", bus);
            Guard.AgainstNull("message", message);

            return bus.ReplyAsync(message, new ReplyOptions());
        }

        /// <summary>
        /// Instantiates a message of type T and performs a regular ReplyAsync.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="bus">Object being extended.</param>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        public static Task ReplyAsync<T>(this IBus bus, Action<T> messageConstructor)
        {
            Guard.AgainstNull("bus", bus);
            Guard.AgainstNull("messageConstructor", messageConstructor);

            return bus.ReplyAsync(messageConstructor, new ReplyOptions());
        }
    }
}