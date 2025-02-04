﻿namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_to_another_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_the_message()
        {
            var context = await Scenario.Define<Context>(c => { c.Id = Guid.NewGuid(); })
                    .WithEndpoint<Sender>(b => b.When((bus, c) =>
                    {
                        var sendOptions = new SendOptions();

                        sendOptions.SetHeader("MyHeader", "MyHeaderValue");
                        sendOptions.SetMessageId("MyMessageId");

                        return bus.SendAsync(new MyMessage { Id = c.Id }, sendOptions);
                    }))
                    .WithEndpoint<Receiver>()
                    .Done(c => c.WasCalled)
                    .Run();

            Assert.True(context.WasCalled, "The message handler should be called");
            Assert.AreEqual(1, context.TimesCalled, "The message handler should only be invoked once");
            Assert.AreEqual("StaticHeaderValue", context.ReceivedHeaders["MyStaticHeader"], "Static headers should be attached to outgoing messages");
            Assert.AreEqual("MyHeaderValue", context.MyHeader, "Static headers should be attached to outgoing messages");

        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }

            public int TimesCalled { get; set; }

            public IDictionary<string, string> ReceivedHeaders { get; set; }

            public Guid Id { get; set; }

            public string MyHeader { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.AddHeaderToAllOutgoingMessages("MyStaticHeader", "StaticHeaderValue");
                }).AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public IBus Bus { get; set; }

                public Task Handle(MyMessage message)
                {
                    if (Context.Id != message.Id)
                        return Task.FromResult(0);

                    Assert.AreEqual(Bus.CurrentMessageContext.Id, "MyMessageId");

                    Context.TimesCalled++;

                    Context.MyHeader = Bus.CurrentMessageContext.Headers["MyHeader"];

                    Context.ReceivedHeaders = Bus.CurrentMessageContext.Headers;

                    Context.WasCalled = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}
