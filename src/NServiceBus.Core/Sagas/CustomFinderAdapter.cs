namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using ObjectBuilder;
    using Sagas;

    class CustomFinderAdapter<TSagaData,TMessage> : SagaFinder where TSagaData : IContainSagaData
    {
        internal override async Task<IContainSagaData> Find(IBuilder builder, SagaFinderDefinition finderDefinition, ReadOnlyContextBag context, object message)
        {
            var customFinderType = (Type)finderDefinition.Properties["custom-finder-clr-type"];

            var finder = (IFindSagas<TSagaData>.Using<TMessage>)builder.Build(customFinderType);

            return await finder.FindBy((TMessage)message, context).ConfigureAwait(false);
        }
    }
}
