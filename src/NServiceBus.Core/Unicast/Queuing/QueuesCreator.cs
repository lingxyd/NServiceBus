namespace NServiceBus.Unicast.Queuing
{
    using System.Threading.Tasks;
    using Installation;
    using Logging;
    using NServiceBus.Settings;
    using Transports;

    class QueuesCreator : IInstall
    {
        readonly ICreateQueues queueCreator;
        readonly ReadOnlySettings settings;

        public QueuesCreator(ICreateQueues queueCreator, ReadOnlySettings settings)
        {
            this.queueCreator = queueCreator;
            this.settings = settings;
        }

        public Task InstallAsync(string identity)
        {
            if (settings.Get<bool>("Endpoint.SendOnly"))
            {
                return TaskEx.Completed;
            }

            if (!settings.CreateQueues())
            {
                return TaskEx.Completed;
            }

            var queueBindings = settings.Get<QueueBindings>();

            foreach (var receiveLogicalAddress in queueBindings.ReceivingAddresses)
            {
                CreateQueue(identity, receiveLogicalAddress);
            }

            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                CreateQueue(identity, sendingAddress);
            }

            return TaskEx.Completed;
        }

        void CreateQueue(string identity, string transportAddress)
        {
            queueCreator.CreateQueueIfNecessary(transportAddress, identity);
            Logger.DebugFormat("Verified that the queue: [{0}] existed", transportAddress);
        }

        static ILog Logger = LogManager.GetLogger<QueuesCreator>();
    }
}
