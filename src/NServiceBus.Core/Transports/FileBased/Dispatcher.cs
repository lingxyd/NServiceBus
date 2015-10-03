namespace NServiceBus.Transports.FileBased
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Extensibility;
    using NServiceBus.Routing;

    class Dispatcher:IDispatchMessages
    {
        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ReadOnlyContextBag context)
        {
            foreach (var transportOperation in outgoingMessages)
            {
                var routing = transportOperation.DispatchOptions.RoutingStrategy as DirectToTargetDestination;

                if (routing == null)
                {
                    throw new InvalidOperationException("The filebased transport does not support native pub sub");
                }

                var path = Path.Combine(routing.Destination, transportOperation.Message.MessageId); 
                File.WriteAllBytes(path,transportOperation.Message.Body);

            }

            return TaskEx.Completed;
        }
    }
}