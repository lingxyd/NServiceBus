namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.MessagingBestPractices;
    using NServiceBus.Pipeline;
    using NServiceBus.Routing;
    using NServiceBus.Routing.MessagingBestPractices;

    class EnforceUnsubscribeBestPracticesBehavior : Behavior<UnsubscribeContext>
    {
        public EnforceUnsubscribeBestPracticesBehavior(Validations validations)
        {
            this.validations = validations;
        }

        public override Task Invoke(UnsubscribeContext context, Func<Task> next)
        {
            EnforceBestPracticesOptions options;

            if (!context.TryGet(out options) || options.Enabled)
            {
                validations.AssertIsValidForPubSub(context.EventType);
            }

            return next();
        }

        Validations validations;
    }
}