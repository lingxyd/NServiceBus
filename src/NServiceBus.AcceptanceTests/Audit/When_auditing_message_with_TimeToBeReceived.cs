﻿namespace NServiceBus.AcceptanceTests.Audit
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_auditing_message_with_TimeToBeReceived : NServiceBusAcceptanceTest
    {

        [Test]
        public async Task Should_not_honor_TimeToBeReceived_for_audit_message()
        {
            var context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithAuditOn>(b => b.When(bus => bus.SendLocalAsync(new MessageToBeAudited())))
            .WithEndpoint<EndpointThatHandlesAuditMessages>()
            .Done(c => c.IsMessageHandlingComplete && c.TTBRHasExpiredAndMessageIsStillInAuditQueue)
            .Run();

            Assert.IsTrue(context.IsMessageHandlingComplete);
        }

        class Context : ScenarioContext
        {
            public bool IsMessageHandlingComplete { get; set; }
            public DateTime? FirstTimeProcessedByAudit { get; set; }
            public bool TTBRHasExpiredAndMessageIsStillInAuditQueue { get; set; }
        }

        class EndpointWithAuditOn : EndpointConfigurationBuilder
        {

            public EndpointWithAuditOn()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo<EndpointThatHandlesAuditMessages>();
            }

            class MessageToBeAuditedHandler : IHandleMessages<MessageToBeAudited>
            {
                Context context;

                public MessageToBeAuditedHandler(Context context)
                {
                    this.context = context;
                }

                public Task Handle(MessageToBeAudited message)
                {
                    context.IsMessageHandlingComplete = true;
                    return Task.FromResult(0);
                }
            }
        }

        class EndpointThatHandlesAuditMessages : EndpointConfigurationBuilder
        {

            public EndpointThatHandlesAuditMessages()
            {
                EndpointSetup<DefaultServer>();
            }

            class AuditMessageHandler : IHandleMessages<MessageToBeAudited>
            {
                Context context;
                IBus bus;

                public AuditMessageHandler(Context context, IBus bus)
                {
                    this.context = context;
                    this.bus = bus;
                }

                public Task Handle(MessageToBeAudited message)
                {
                    var auditProcessingStarted = DateTime.Now;
                    if (context.FirstTimeProcessedByAudit == null)
                    {
                        context.FirstTimeProcessedByAudit = auditProcessingStarted;
                    }

                    var ttbr = TimeSpan.Parse(bus.CurrentMessageContext.Headers[Headers.TimeToBeReceived]);
                    var ttbrExpired = auditProcessingStarted > (context.FirstTimeProcessedByAudit.Value + ttbr);
                    if (ttbrExpired)
                    {
                        context.TTBRHasExpiredAndMessageIsStillInAuditQueue = true;
                        var timeElapsedSinceFirstHandlingOfAuditMessage = auditProcessingStarted - context.FirstTimeProcessedByAudit.Value;
                        Console.WriteLine("Audit message not removed because of TTBR({0}) after {1}. Success.", ttbr, timeElapsedSinceFirstHandlingOfAuditMessage);
                    }
                    else
                    {
                        return bus.HandleCurrentMessageLaterAsync();
                    }

                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        [TimeToBeReceived("00:00:03")]
        class MessageToBeAudited : IMessage
        {
        }
    }
}
