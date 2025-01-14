﻿namespace NServiceBus.AcceptanceTests.Routing
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_base_event_from_2_publishers : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_events_from_all_publishers()
        {
            var context = await Scenario.Define<Context>()
               .WithEndpoint<Publisher1>(b =>
                        b.When(c => c.SubscribedToPublisher1, bus => bus.PublishAsync(new DerivedEvent1()))
                     )
                .WithEndpoint<Publisher2>(b =>
                        b.When(c => c.SubscribedToPublisher2, bus => bus.PublishAsync(new DerivedEvent2()))
                     )
               .WithEndpoint<Subscriber1>(b => b.When(async (bus, c) =>
               {
                   await bus.SubscribeAsync<DerivedEvent1>();
                   await bus.SubscribeAsync<DerivedEvent2>();

                   if (c.HasNativePubSubSupport)
                   {
                       c.SubscribedToPublisher1 = true;
                       c.SubscribedToPublisher2 = true;
                   }
               }))
               .AllowExceptions(e => e.Message.Contains("Oracle.DataAccess.Client.OracleException: ORA-00001") || e.Message.Contains("System.Data.SqlClient.SqlException: Violation of PRIMARY KEY constraint"))
               .Done(c => c.GotTheEventFromPublisher1 && c.GotTheEventFromPublisher2)
               .Run();

            Assert.True(context.GotTheEventFromPublisher1);
            Assert.True(context.GotTheEventFromPublisher2);
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEventFromPublisher1 { get; set; }
            public bool GotTheEventFromPublisher2 { get; set; }
            public bool SubscribedToPublisher1 { get; set; }
            public bool SubscribedToPublisher2 { get; set; }
        }

        public class Publisher1 : EndpointConfigurationBuilder
        {
            public Publisher1()
            {
                EndpointSetup<DefaultPublisher>(b => b.OnEndpointSubscribed<Context>((s, context) =>
                {
                    context.AddTrace("Publisher1 SubscriberReturnAddress=" + s.SubscriberReturnAddress);
                    if (s.SubscriberReturnAddress.Contains("Subscriber1"))
                    {
                        context.SubscribedToPublisher1 = true;
                    }
                }));
            }
        }

        public class Publisher2 : EndpointConfigurationBuilder
        {
            public Publisher2()
            {
                EndpointSetup<DefaultPublisher>(b => b.OnEndpointSubscribed<Context>((s, context) =>
                {
                    context.AddTrace("Publisher2 SubscriberReturnAddress=" + s.SubscriberReturnAddress);

                    if (s.SubscriberReturnAddress.Contains("Subscriber1"))
                    {
                        context.SubscribedToPublisher2 = true;
                    }
                }));
            }
        }

        public class Subscriber1 : EndpointConfigurationBuilder
        {
            public Subscriber1()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<DerivedEvent1>(typeof(Publisher1))
                    .AddMapping<DerivedEvent2>(typeof(Publisher2));
            }

            public class BaseEventHandler : IHandleMessages<BaseEvent>
            {
                public Context Context { get; set; }

                public Task Handle(BaseEvent message)
                {
                    if (message.GetType().FullName.Contains("DerivedEvent1"))
                        Context.GotTheEventFromPublisher1 = true;
                    if (message.GetType().FullName.Contains("DerivedEvent2"))
                        Context.GotTheEventFromPublisher2 = true;

                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class BaseEvent : IEvent
        {
        }

        [Serializable]
        public class DerivedEvent1 : BaseEvent
        {

        }

        [Serializable]
        public class DerivedEvent2 : BaseEvent
        {

        }
    }
}