using System;
using NServiceBus.Features;

namespace NServiceBus.Transports.FileBased
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class FileBasedTransportConfigurator : ConfigureTransport
    {
        public FileBasedTransportConfigurator()
        {
            DependsOn<UnicastBus>();
        }
        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            context.Container.ConfigureComponent<MessagePump>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<QueueCreator>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<Dispatcher>(DependencyLifecycle.InstancePerCall);
        }

        protected override string ExampleConnectionStringForErrorMessage => "";

        protected override bool RequiresConnectionString => false;

    }

    class Dispatcher:IDispatchMessages
    {
        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ReadOnlyContextBag context)
        {
            throw new NotImplementedException();
        }
    }

    class MessagePump: IPushMessages
    {
        public void Init(Func<PushContext, Task> pipe, PushSettings settings)
        {
            throw new NotImplementedException();
        }

        public void Start(PushRuntimeSettings limitations)
        {
            throw new NotImplementedException();
        }

        public Task Stop()
        {
            throw new NotImplementedException();
        }
    }

    class QueueCreator:ICreateQueues
    {
        public void CreateQueueIfNecessary(string address, string account)
        {
            Directory.CreateDirectory(address);
        }
    }
}