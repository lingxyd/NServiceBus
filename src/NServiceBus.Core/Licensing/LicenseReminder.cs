namespace NServiceBus.Features
{
    using System;
    using System.Diagnostics;
    using Logging;
    using NServiceBus.Licensing;

    class LicenseReminder : Feature
    {
        public LicenseReminder()
        {
            EnableByDefault();
        }

        protected internal override void Setup(FeatureConfigurationContext context)
        {
            try
            {
                LicenseManager.InitializeLicense();

                var licenseExpired = LicenseManager.HasLicenseExpired();

                if (!licenseExpired)
                {
                    return;
                }

                context.Pipeline.Register("LicenseReminder", typeof(AuditInvalidLicenseBehavior), "Audits that the message was processed by an endpoint with an expired license");

                if (Debugger.IsAttached)
                {
                    context.Pipeline.Register("LogErrorOnInvalidLicense", typeof(LogErrorOnInvalidLicenseBehavior), "Logs an error when running in debug mode with an expired license");
                }
            }
            catch (Exception ex)
            {
                //we only log here to prevent licensing issue to abort startup and cause production outages
                Logger.Fatal("Failed to initialize the license", ex);
            }
        }

        static ILog Logger = LogManager.GetLogger<LicenseReminder>();
    }
}