namespace NServiceBus.Routing
{
    /// <summary>
    /// Represents a route directly to the specified destination.
    /// </summary>
    public class UnicastAddressTag : AddressTag
    {
        /// <summary>
        /// The destination.
        /// </summary>
        public string Destination { get; }

        /// <summary>
        /// Initializes the strategy.
        /// </summary>
        /// <param name="destination">The destination.</param>
        public UnicastAddressTag(string destination)
        {
            Guard.AgainstNullAndEmpty(nameof(destination), destination);
            Destination = destination;
        }
    }
}