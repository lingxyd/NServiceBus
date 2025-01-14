namespace NServiceBus.AcceptanceTests.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Features;
    using NUnit.Framework;

    public class When_feature_overrides_hostinfo : NServiceBusAcceptanceTest
    {
        static Guid hostId = new Guid("6c0f50de-dac9-4693-b138-6d1033c15ed6");
        static string instanceName = "Foo";

        [Test]
        public async Task HostInfo_is_changed()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<MyEndpoint>(e => e.When(b => b.SendLocalAsync(new MyMessage())))
                .Done(c => c.OriginatingHostId != Guid.Empty)
                .Run();

            Assert.AreEqual(hostId, context.OriginatingHostId);
        }

        public class MyEndpoint : EndpointConfigurationBuilder
        {
            public MyEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class MyFeatureThatOverridesHostInformationDefaults : Feature
        {
            public MyFeatureThatOverridesHostInformationDefaults()
            {
                EnableByDefault();
                DependsOn("UnicastBus");
                Defaults(s =>
                {
                    s.SetDefault("NServiceBus.HostInformation.HostId", hostId);
                    s.SetDefault("NServiceBus.HostInformation.DisplayName", instanceName);
                    s.SetDefault("NServiceBus.HostInformation.Properties", new Dictionary<string, string>
                    {
                        {"RoleName", "My role name"},
                        {"RoleInstanceId", "the role instance id"},
                    });
                });
            }

            protected override void Setup(FeatureConfigurationContext context)
            {
            }
        }

        public class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public IBus Bus { get; set; }

            public Context Context { get; set; }

            public Task Handle(MyMessage message)
            {
                Context.OriginatingHostId = new Guid(Bus.CurrentMessageContext.Headers[Headers.OriginatingHostId]);
                return Task.FromResult(0);
            }
        }

        public class Context : ScenarioContext
        {
            public Guid OriginatingHostId { get; set; }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }
    }
}