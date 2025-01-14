namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.ObjectBuilder.Common;
    using NServiceBus.Pipeline;
    using NServiceBus.Settings;
    using NServiceBus.Utils.Reflection;

    /// <summary>
    ///     Central configuration entry point.
    /// </summary>
    public partial class Configure
    {
        /// <summary>
        ///     Creates a new instance of <see cref="Configure"/>.
        /// </summary>
        internal Configure(SettingsHolder settings, IContainer container, List<Action<IConfigureComponents>> registrations, PipelineSettings pipelineSettings, PipelineConfiguration pipelineConfiguration)
        {
            Settings = settings;
            this.pipelineSettings = pipelineSettings;
            this.pipelineConfiguration = pipelineConfiguration;

            RegisterContainerAdapter(container);
            RunUserRegistrations(registrations);

            this.container.RegisterSingleton(this);
            this.container.RegisterSingleton<ReadOnlySettings>(settings);
        }

        /// <summary>
        ///     Provides access to the settings holder.
        /// </summary>
        public SettingsHolder Settings { get; }

        /// <summary>
        ///     Gets the builder.
        /// </summary>
        public IBuilder Builder { get; private set; }

        /// <summary>
        ///     Returns types in assemblies found in the current directory.
        /// </summary>
        public IList<Type> TypesToScan => Settings.GetAvailableTypes();

        void RunUserRegistrations(List<Action<IConfigureComponents>> registrations)
        {
            foreach (var registration in registrations)
            {
                registration(container);
            }
        }

        void RegisterContainerAdapter(IContainer container)
        {
            var b = new CommonObjectBuilder
            {
                Container = container,
            };

            Builder = b;
            this.container = b;

            this.container.ConfigureComponent<CommonObjectBuilder>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty(c => c.Container, container);
        }

        void WireUpConfigSectionOverrides()
        {
            foreach (var t in TypesToScan.Where(t => t.GetInterfaces().Any(IsGenericConfigSource)))
            {
                container.ConfigureComponent(t, DependencyLifecycle.InstancePerCall);
            }
        }

        internal void Initialize()
        {
            WireUpConfigSectionOverrides();

            var featureActivator = new FeatureActivator(Settings);

            container.RegisterSingleton(featureActivator);

            ForAllTypes<Feature>(TypesToScan, t => featureActivator.Add(t.Construct<Feature>()));

            ForAllTypes<IWantToRunWhenBusStartsAndStops>(TypesToScan, t => container.ConfigureComponent(t, DependencyLifecycle.InstancePerCall));

            ActivateAndInvoke<IWantToRunBeforeConfigurationIsFinalized>(TypesToScan, t => t.Run(this));

            var featureStats = featureActivator.SetupFeatures(new FeatureConfigurationContext(this));

            pipelineConfiguration.RegisterBehaviorsInContainer(Settings, container);

            container.RegisterSingleton(featureStats);

            featureActivator.RegisterStartupTasks(container);

            ReportFeatures(featureStats);
            StartFeatures(featureActivator);
        }

        static void ReportFeatures(FeaturesReport featureStats)
        {
            var reporter = new DisplayDiagnosticsForFeatures();
            reporter.Run(featureStats);
        }

        void StartFeatures(FeatureActivator featureActivator)
        {
            var featureRunner = new FeatureRunner(Builder, featureActivator);
            container.RegisterSingleton(featureRunner);

            featureRunner.Start();
        }

        /// <summary>
        ///     Applies the given action to all the scanned types that can be assigned to <typeparamref name="T" />.
        /// </summary>
        internal static void ForAllTypes<T>(IEnumerable<Type> types, Action<Type> action) where T : class
        {
            // ReSharper disable HeapView.SlowDelegateCreation
            foreach (var type in types.Where(t => typeof(T).IsAssignableFrom(t) && !(t.IsAbstract || t.IsInterface)))
            {
                action(type);
            }
            // ReSharper restore HeapView.SlowDelegateCreation
        }

        internal static void ActivateAndInvoke<T>(IList<Type> types, Action<T> action) where T : class
        {
            ForAllTypes<T>(types, t =>
            {
                var instanceToInvoke = (T)Activator.CreateInstance(t);
                action(instanceToInvoke);
            });
        }

        static bool IsGenericConfigSource(Type t)
        {
            if (!t.IsGenericType)
            {
                return false;
            }

            var args = t.GetGenericArguments();
            if (args.Length != 1)
            {
                return false;
            }

            return typeof(IProvideConfiguration<>).MakeGenericType(args).IsAssignableFrom(t);
        }

        internal IConfigureComponents container;
        internal PipelineSettings pipelineSettings;
        PipelineConfiguration pipelineConfiguration;
    }
}
