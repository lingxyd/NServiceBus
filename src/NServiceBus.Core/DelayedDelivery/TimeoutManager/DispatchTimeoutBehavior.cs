namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Routing;
    using Timeout.Core;
    using Transports;

    class DispatchTimeoutBehavior : SatelliteBehavior
    {
        public DispatchTimeoutBehavior(IDispatchMessages dispatcher, IPersistTimeouts persister)
        {
            this.dispatcher = dispatcher;
            this.persister = persister;
        }

        protected override async Task Terminate(PhysicalMessageProcessingContext context)
        {
            var message = context.Message;
            var timeoutId = message.Headers["Timeout.Id"];

            var timeoutData = await persister.Remove(timeoutId, context).ConfigureAwait(false);

            if (timeoutData == null)
            {
                return;
            }

            var sendOptions = new DispatchOptions(new UnicastAddressTag(timeoutData.Destination), DispatchConsistency.Default);

            timeoutData.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
            timeoutData.Headers["NServiceBus.RelatedToTimeoutId"] = timeoutData.Id;

            await dispatcher.Dispatch(new[] { new TransportOperation(new OutgoingMessage(message.MessageId, timeoutData.Headers, timeoutData.State), sendOptions) }, context).ConfigureAwait(false);
        }

        IDispatchMessages dispatcher;
        IPersistTimeouts persister;
    }
}