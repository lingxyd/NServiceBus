﻿namespace NServiceBus.Features
{
    using System.Threading.Tasks;
    using Extensibility;
    using NServiceBus.Outbox;
    using Pipeline;

    class ReceiveFeature : Feature
    {
        public ReceiveFeature()
        {
            EnableByDefault();
        }

        protected internal override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.RegisterConnector<TransportReceiveToPhysicalMessageProcessingConnector>("Allows to abort processing the message");
            context.Pipeline.RegisterConnector<DeserializeLogicalMessagesConnector>("Deserializes the physical message body into logical messages");
            context.Pipeline.RegisterConnector<LoadHandlersConnector>("Gets all the handlers to invoke from the MessageHandler registry based on the message type.");


            context.Pipeline
                .Register(WellKnownStep.ExecuteUnitOfWork, typeof(UnitOfWorkBehavior), "Executes the UoW")
                .Register(WellKnownStep.MutateIncomingTransportMessage, typeof(MutateIncomingTransportMessageBehavior), "Executes IMutateIncomingTransportMessages")
                .Register(WellKnownStep.MutateIncomingMessages, typeof(MutateIncomingMessageBehavior), "Executes IMutateIncomingMessages")
                .Register(WellKnownStep.InvokeHandlers, typeof(InvokeHandlerTerminator), "Calls the IHandleMessages<T>.Handle(T)");

            context.Container.ConfigureComponent(b =>
            {
                var pipelinesCollection = context.Settings.Get<PipelineConfiguration>();

                var pipeline = new PipelineBase<BatchDispatchContext>(b, context.Settings, pipelinesCollection.MainPipeline);

                IOutboxStorage storage;

                if (context.Container.HasComponent<IOutboxStorage>())
                {
                    storage = b.Build<IOutboxStorage>();
                }
                else
                {
                    storage = new NoOpOutbox();
                }

                return new TransportReceiveToPhysicalMessageProcessingConnector(pipeline, storage);
            }, DependencyLifecycle.InstancePerCall);
        }

        class NoOpOutbox : IOutboxStorage
        {
            public Task<OutboxMessage> Get(string messageId, ReadOnlyContextBag options)
            {
                return Task.FromResult<OutboxMessage>(null);
            }

            public Task Store(OutboxMessage message, OutboxTransaction transaction, ReadOnlyContextBag options)
            {
                return TaskEx.Completed;
            }

            public Task SetAsDispatched(string messageId, ReadOnlyContextBag options)
            {
                return TaskEx.Completed;
            }

            public Task<OutboxTransaction> BeginTransaction(ReadOnlyContextBag context)
            {
                return Task.FromResult<OutboxTransaction>(new NoOpOutboxTransaction());
            }
        }
    }

    class NoOpOutboxTransaction : OutboxTransaction
    {
        public void Dispose()
        {
            
        }

        public Task Commit()
        {
            return TaskEx.Completed;
        }
    }
}