using NServiceBus.Features;

namespace NServiceBus.Transports.FileBased
{
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
}