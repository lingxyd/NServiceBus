﻿namespace NServiceBus.AcceptanceTests.PerfMon.CriticalTime
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;

    public class When_CriticalTime_enabled : NServiceBusAcceptanceTest
    {
        float counterValue;

        [Test]
        [Explicit("Since perf counters need to be enabled with powershell")]
        public async Task Should_have_perf_counter_set()
        {
            using (var counter = new PerformanceCounter("NServiceBus", "Critical Time", "CriticaltimeEnabled.Endpoint", false))
            using (new Timer(state => CheckPerfCounter(counter), null, 0, 100))
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Endpoint>(b => b.When((bus, c) => bus.SendLocalAsync(new MyMessage())))
                    .Done(c => c.WasCalled)
                    .Repeat(r => r.For(Transports.Default))
                    .Should(c => Assert.True(c.WasCalled, "The message handler should be called"))
                    .Run();
            }
            Assert.Greater(counterValue, 0);
        }

        void CheckPerfCounter(PerformanceCounter counter)
        {
            float rawValue = counter.RawValue;
            if (rawValue > 0)
            {
                counterValue = rawValue;
            }
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(builder => builder.EnableCriticalTimePerformanceCounter());
            }
        }

        public class MyMessage : IMessage
        {
        }

        public class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public Context Context { get; set; }

            public Task Handle(MyMessage message)
            {
                Context.WasCalled = true;
                return Task.FromResult(0);
            }
        }
    }
}