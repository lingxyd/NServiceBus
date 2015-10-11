namespace NServiceBus.Transports.FileBased
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using NServiceBus.Routing;

    class Dispatcher:IDispatchMessages
    {
        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ReadOnlyContextBag context)
        {
            foreach (var transportOperation in outgoingMessages)
            {
                var addressTag = transportOperation.DispatchOptions.AddressTag as UnicastAddressTag;

                if (addressTag == null)
                {
                    throw new InvalidOperationException("The filebased transport only support unicast addressing");
                }

                var basePath = Path.Combine("c:\\bus", addressTag.Destination, transportOperation.Message.MessageId);
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

                if (transportOperation.DispatchOptions.RequiredDispatchConsistency != DispatchConsistency.Isolated &&
                    context.TryGet(out transaction))
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