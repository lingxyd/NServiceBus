namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a bus to be used with NServiceBus.
    /// </summary>
    public partial interface IBus : ISendOnlyBus
    {
        /// <summary>
        /// Sends the message to the endpoint which sent the message currently being handled on this thread.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="options">Options for this reply.</param>
        Task ReplyAsync(object message, ReplyOptions options);

        /// <summary>
        /// Instantiates a message of type T and performs a regular <see cref="ReplyAsync"/>.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="options">Options for this reply.</param>
        Task ReplyAsync<T>(Action<T> messageConstructor, ReplyOptions options);

        /// <summary>
        /// Moves the message being handled to the back of the list of available 
        /// messages so it can be handled later.
        /// </summary>
        Task HandleCurrentMessageLaterAsync();

        /// <summary>
        /// Forwards the current message being handled to the destination maintaining
        /// all of its transport-level properties and headers.
        /// </summary>
        Task ForwardCurrentMessageToAsync(string destination);

        /// <summary>
        /// Tells the bus to stop dispatching the current message to additional
        /// handlers.
        /// </summary>
        void DoNotContinueDispatchingCurrentMessageToHandlers();

        /// <summary>
        /// Gets the message context containing the Id, return address, and headers
        /// of the message currently being handled on this thread.
        /// </summary>
        IMessageContext CurrentMessageContext { get; }
    }
}
