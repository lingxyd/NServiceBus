﻿namespace NServiceBus.Features
{

    /// <summary>
    /// Used to configure in memory saga persistence.
    /// </summary>
    public class InMemorySagaPersistence : Feature
    {
        internal InMemorySagaPersistence()
        {
            DependsOn<Sagas>();
        }

        /// <summary>
        /// See <see cref="Feature.Setup"/>.
        /// </summary>
        protected internal override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<InMemorySagaPersister>(DependencyLifecycle.SingleInstance);
        }
    }
}