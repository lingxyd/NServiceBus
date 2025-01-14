namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    /// <summary>
    /// This class is used to define sagas containing data and handling a message.
    /// To handle more message types, implement <see cref="IHandleMessages{T}"/>
    /// for the relevant types.
    /// To signify that the receipt of a message should start this saga,
    /// implement <see cref="IAmStartedByMessages{T}"/> for the relevant message type.
    /// </summary>
    public abstract partial class Saga
    {
        /// <summary>
        /// The saga's typed data.
        /// </summary>
        public IContainSagaData Entity { get; set; }

        /// <summary>
        /// Override this method in order to configure how this saga's data should be found.
        /// </summary>
        internal protected abstract void ConfigureHowToFindSaga(IConfigureHowToFindSagaWithMessage sagaMessageFindingConfiguration);

        /// <summary>
        /// Bus object used for retrieving the sender endpoint which caused this saga to start.
        /// Necessary for <see cref="ReplyToOriginator" />.
        /// </summary>
        public IBus Bus
        {
            get
            {
                if (bus == null)
                    throw new InvalidOperationException("No IBus instance available, please configure one and also verify that you're not defining your own Bus property in your saga since that hides the one in the base class");

                return bus;
            }
            set { bus = value; }
        }

        IBus bus;

        /// <summary>
        /// Indicates that the saga is complete.
        /// In order to set this value, use the <see cref="MarkAsComplete" /> method.
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// Request for a timeout to occur at the given <see cref="DateTime"/>.
        /// </summary>
        /// <param name="at"><see cref="DateTime"/> to send timeout <typeparamref name="TTimeoutMessageType"/>.</param>
        protected Task RequestTimeoutAsync<TTimeoutMessageType>(DateTime at) where TTimeoutMessageType : new()
        {
            return RequestTimeoutAsync(at, new TTimeoutMessageType());
        }

        /// <summary>
        /// Request for a timeout to occur at the given <see cref="DateTime"/>.
        /// </summary>
        /// <param name="at"><see cref="DateTime"/> to send timeout <paramref name="timeoutMessage"/>.</param>
        /// <param name="timeoutMessage">The message to send after <paramref name="at"/> is reached.</param>
        protected Task RequestTimeoutAsync<TTimeoutMessageType>(DateTime at, TTimeoutMessageType timeoutMessage)
        {
            if (at.Kind == DateTimeKind.Unspecified)
            {
                throw new InvalidOperationException("Kind property of DateTime 'at' must be specified.");
            }

            VerifySagaCanHandleTimeout(timeoutMessage);

            var options = new SendOptions();

            options.DoNotDeliverBefore(at);
            options.RouteToLocalEndpointInstance();

            SetTimeoutHeaders(options);

            return Bus.SendAsync(timeoutMessage, options);
        }

        /// <summary>
        /// Request for a timeout to occur within the give <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="within">Given <see cref="TimeSpan"/> to delay timeout message by.</param>
        protected Task RequestTimeoutAsync<TTimeoutMessageType>(TimeSpan within) where TTimeoutMessageType : new()
        {
            return RequestTimeoutAsync(within, new TTimeoutMessageType());
        }

        /// <summary>
        /// Request for a timeout to occur within the given <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="within">Given <see cref="TimeSpan"/> to delay timeout message by.</param>
        /// <param name="timeoutMessage">The message to send after <paramref name="within"/> expires.</param>
        protected Task RequestTimeoutAsync<TTimeoutMessageType>(TimeSpan within, TTimeoutMessageType timeoutMessage)
        {
            VerifySagaCanHandleTimeout(timeoutMessage);

            var context = new SendOptions();

            context.DelayDeliveryWith(within);
            context.RouteToLocalEndpointInstance();

            SetTimeoutHeaders(context);

            return Bus.SendAsync(timeoutMessage, context);
        }

        /// <summary>
        /// Sends the <paramref name="message"/> using the bus to the endpoint that caused this saga to start.
        /// </summary>
        protected Task ReplyToOriginatorAsync(object message)
        {
            if (string.IsNullOrEmpty(Entity.Originator))
            {
                throw new Exception("Entity.Originator cannot be null. Perhaps the sender is a SendOnly endpoint.");
            }

            var options = new ReplyOptions();

            options.OverrideReplyToAddressOfIncomingMessage(Entity.Originator);
            options.SetCorrelationId(Entity.OriginalMessageId);

            //until we have metadata we just set this to null to avoid our own saga id being set on outgoing messages since
            //that would cause the saga that started us (if it was a saga) to not be found. When we have metadata available in the future we'll set the correct id and type
            // and get true auto correlation to work between sagas
            options.Context.Set(new PopulateAutoCorrelationHeadersForRepliesBehavior.State
            {
                SagaTypeToUse = null,
                SagaIdToUse = null
            });

            return Bus.ReplyAsync(message, options);
        }

        /// <summary>
        /// Marks the saga as complete.
        /// This may result in the sagas state being deleted by the persister.
        /// </summary>
        protected void MarkAsComplete()
        {
            Completed = true;
        }

        void VerifySagaCanHandleTimeout<TTimeoutMessageType>(TTimeoutMessageType timeoutMessage)
        {
            var canHandleTimeoutMessage = this is IHandleTimeouts<TTimeoutMessageType>;
            if (!canHandleTimeoutMessage)
            {
                var message = $"The type '{GetType().Name}' cannot request timeouts for '{timeoutMessage}' because it does not implement 'IHandleTimeouts<{typeof(TTimeoutMessageType).FullName}>'";
                throw new Exception(message);
            }
        }

        void SetTimeoutHeaders(ExtendableOptions options)
        {
            options.SetHeader(Headers.SagaId, Entity.Id.ToString());
            options.SetHeader(Headers.IsSagaTimeoutMessage, bool.TrueString);
            options.SetHeader(Headers.SagaType, GetType().AssemblyQualifiedName);
        }
    }
}
