namespace NServiceBus.Routing.MessageDrivenSubscriptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents an address of a publisher.
    /// </summary>
    public class PublisherAddress
    {
        EndpointName endpoint;
        EndpointInstanceName[] instances;
        string[] addresses;

        /// <summary>
        /// Creates a new publisher based on the endpoint name.
        /// </summary>
        public PublisherAddress(EndpointName endpoint)
        {
            Guard.AgainstNull("endpoint", endpoint);
            this.endpoint = endpoint;
        }

        /// <summary>
        /// Creates a new publisher based on a set of endpoint instance names.
        /// </summary>
        public PublisherAddress(params EndpointInstanceName[] instances)
        {
            Guard.AgainstNull("instances", instances);
            if (instances.Length == 0)
            {
                throw new ArgumentException("You have to provide at least one instance.");
            }
            this.instances = instances;
        }

        /// <summary>
        /// Creates a new publisher based on a set of physical addresses.
        /// </summary>
        public PublisherAddress(params string[] addresses)
        {
            Guard.AgainstNull("addresses",addresses);
            if (addresses.Length == 0)
            {
                throw new ArgumentException("You need to provide at least one address.");
            }
            this.addresses = addresses;
        }

        internal IEnumerable<string> Resolve(Func<EndpointName, IEnumerable<EndpointInstanceName>> instanceResolver, Func<EndpointInstanceName, string> addressResolver)
        {
            if (addresses != null)
            {
                return addresses;
            }
            if (instances != null)
            {
                return instances.Select(addressResolver);
            }
            return instanceResolver(endpoint).Select(addressResolver);
        }
    }
}