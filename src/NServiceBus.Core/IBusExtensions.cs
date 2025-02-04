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

        /// <summary>
        /// Sends the message back to the current bus.
        /// </summary>
        /// <param name="bus">Object being extended.</param>
        /// <param name="message">The message to send.</param>
        public static Task SendLocalAsync(this IBus bus, object message)
        {
            Guard.AgainstNull("bus", bus);
            Guard.AgainstNull("message", message);

            var options = new SendOptions();

            options.RouteToLocalEndpointInstance();

            return bus.SendAsync(message, options);
        }

        /// <summary>
        /// Instantiates a message of type T and sends it back to the current bus.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="bus">Object being extended.</param>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        public static Task SendLocalAsync<T>(this IBus bus, Action<T> messageConstructor)
        {
            Guard.AgainstNull("bus", bus);
            Guard.AgainstNull("messageConstructor", messageConstructor);

            var options = new SendOptions();

            options.RouteToLocalEndpointInstance();

            return bus.SendAsync(messageConstructor, options);
        }

        /// <summary>
        /// Subscribes to receive published messages of the specified type.
        /// This method is only necessary if you turned off auto-subscribe.
        /// </summary>
        /// <param name="bus">Object being extended.</param>
        /// <param name="messageType">The type of message to subscribe to.</param>
        public static Task SubscribeAsync(this IBus bus, Type messageType)
        {
            Guard.AgainstNull("bus", bus);
            Guard.AgainstNull("messageType", messageType);

            return bus.SubscribeAsync(messageType, new SubscribeOptions());
        }

        /// <summary>
        /// Subscribes to receive published messages of type T.
        /// This method is only necessary if you turned off auto-subscribe.
        /// </summary>
        /// <param name="bus">Object being extended.</param>
        /// <typeparam name="T">The type of message to subscribe to.</typeparam>
        public static Task SubscribeAsync<T>(this IBus bus)
        {
            Guard.AgainstNull("bus", bus);

            return bus.SubscribeAsync(typeof(T), new SubscribeOptions());
        }

        /// <summary>
        /// Unsubscribes from receiving published messages of the specified type.
        /// </summary>
        /// <param name="bus">Object being extended.</param>
        /// <param name="messageType">The type of message to subscribe to.</param>
        public static Task UnsubscribeAsync(this IBus bus, Type messageType)
        {
            Guard.AgainstNull("bus", bus);
            Guard.AgainstNull("messageType", messageType);

            return bus.UnsubscribeAsync(messageType, new UnsubscribeOptions());
        }

        /// <summary>
        /// Unsubscribes from receiving published messages of the specified type.
        /// </summary>
        /// <param name="bus">Object being extended.</param>
        /// <typeparam name="T">The type of message to unsubscribe from.</typeparam>
        public static Task UnsubscribeAsync<T>(this IBus bus)
        {
            Guard.AgainstNull("bus", bus);

            return bus.UnsubscribeAsync(typeof(T), new UnsubscribeOptions());
        }
    }
}