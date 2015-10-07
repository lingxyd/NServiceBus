namespace NServiceBus.Core.Tests.Config
{
    using System.Threading.Tasks;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    public class When_no_custom_configuration_source_is_specified
    {
        [Test]
        public async Task The_default_configuration_source_should_be_default()
        {
            var builder = new BusConfiguration();

            builder.SendOnly();
            builder.TypesToScanInternal(new[] { typeof(ConfigSectionValidatorFeature) });
            builder.EnableFeature<ConfigSectionValidatorFeature>();

            var endpoint = await Endpoint.CreateAndStartAsync(builder);
            await endpoint.StopAsync();
        }

        class ConfigSectionValidatorFeature : Feature
        {
            public ConfigSectionValidatorFeature()
            {
                RegisterStartupTask<ValidatorTask>();
            }

            protected internal override void Setup(FeatureConfigurationContext context)
            {
            }

            class ValidatorTask : FeatureStartupTask
            {
                ReadOnlySettings settings;

                public ValidatorTask(ReadOnlySettings settings)
                {
                    this.settings = settings;
                }

                protected override void OnStart(ISendOnlyBus bus)
                {
                    Assert.AreEqual(settings.GetConfigSection<TestConfigurationSection>().TestSetting, "test");
                }
            }
        }
    }
}