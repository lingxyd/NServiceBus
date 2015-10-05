namespace NServiceBus.Transports.FileBased
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Routing;

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

                var basePath = Path.Combine("c:\\bus", routing.Destination, transportOperation.Message.MessageId);
                var bodyPath = basePath + ".xml"; //TODO: pick the correct ending based on the serialized type
                File.WriteAllBytes(bodyPath,transportOperation.Message.Body);

                var messageContents = new List<string>
                {
                    bodyPath
                };

                //todo: handle new lines in headers
                messageContents.AddRange(transportOperation.Message.Headers.SelectMany(kvp => new[] { kvp.Key, kvp.Value }));

                DirectoryBasedTransaction transaction;

                var messagePath = basePath + ".txt";

                if (context.TryGet(out transaction))
                {
                    //store the original destination
                    messageContents.Add(messagePath);
                    
                    transaction.Enlist(messagePath,messageContents);
                 }
                else
                {
                    File.WriteAllLines(messagePath, messageContents);
                }
            }

            return TaskEx.Completed;
        }
    }
}