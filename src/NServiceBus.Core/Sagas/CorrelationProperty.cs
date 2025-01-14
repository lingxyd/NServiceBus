namespace NServiceBus.Sagas
{
    /// <summary>
    /// Details about a saga data property used to correlate messages hitting the saga.
    /// </summary>
    public class CorrelationProperty
    {
        /// <summary>
        /// Creates a new instance of <see cref="CorrelationProperty"/>.
        /// </summary>
        /// <param name="name">The name of the saga data property.</param>
        public CorrelationProperty(string name)
        {
            Name = name;
        }

        /// <summary>
        /// The name of the saga data property.
        /// </summary>
        public string Name { get; private set; }
    }
}