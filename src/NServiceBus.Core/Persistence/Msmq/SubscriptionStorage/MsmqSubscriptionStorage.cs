namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Messaging;
    using System.Threading.Tasks;
    using System.Transactions;
    using Logging;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;
    using MessageType = Unicast.Subscriptions.MessageType;

    /// <summary>
    /// Provides functionality for managing message subscriptions
    /// using MSMQ.
    /// </summary>
    class MsmqSubscriptionStorage : IInitializableSubscriptionStorage, IDisposable
    {
        public bool TransactionsEnabled { get; set; }

        public void Init()
        {
            q = new MessageQueue(Queue.FullPath);
            bool transactional;
            try
            {
                transactional = q.Transactional;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"There is a problem with the subscription storage queue {Queue}. See enclosed exception for details.", ex);
            }

            if (!transactional && TransactionsEnabled)
                throw new ArgumentException("Queue must be transactional (" + Queue + ").");

            var messageReadPropertyFilter = new MessagePropertyFilter { Id = true, Body = true, Label = true };

            q.Formatter = new XmlMessageFormatter(new[] { typeof(string) });

            q.MessageReadPropertyFilter = messageReadPropertyFilter;

            foreach (var m in q.GetAllMessages())
            {
                var subscriber = m.Label;
                var messageTypeString = m.Body as string;
                var messageType = new MessageType(messageTypeString); //this will parse both 2.6 and 3.0 type strings

                entries.Add(new Entry { MessageType = messageType, Subscriber = subscriber });
                AddToLookup(subscriber, messageType, m.Id);
            }
        }

        public Task<IEnumerable<string>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, SubscriptionStorageOptions options)
        {
            var subscribers = new List<string>();
            var result = new List<string>();
            var messagelist = messageTypes.ToList();
            lock (locker)
            {
                foreach (var e in entries)
                {
                    foreach (var m in messagelist)
                    {
                        if (e.MessageType == m)
                        {
                            var loweredSubscriberAddress = e.Subscriber.ToLowerInvariant();
                            if (!result.Contains(loweredSubscriberAddress))
                            {
                                result.Add(loweredSubscriberAddress);
                                subscribers.Add(e.Subscriber);
                            }
                        }
                    }
                }
            }
            return Task.FromResult((IEnumerable<string>) subscribers);
        }

        public Task Subscribe(string address, IEnumerable<MessageType> messageTypes, SubscriptionStorageOptions options)
        {
            lock (locker)
            {
                var messagelist = messageTypes.ToList();
                foreach (var messageType in messagelist)
                {
                    var found = false;
                    foreach (var e in entries)
                    {
                        if (e.MessageType == messageType && e.Subscriber == address)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        Add(address, messageType);

                        var entry = new Entry
                                    {
                                        MessageType = messageType,
                                        Subscriber = address
                                    };
                        entries.Add(entry);

                        log.DebugFormat("Subscriber {0} added for message {1}.", address, messageType);
                    }
                }
            }
            return TaskEx.Completed;
        }

        public Task Unsubscribe(string address, IEnumerable<MessageType> messageTypes, SubscriptionStorageOptions options)
        {
            lock (locker)
            {
                var messagelist = messageTypes.ToList();
                foreach (var e in entries.ToArray())
                {
                    foreach (var messageType in messagelist)
                    {
                        if (e.MessageType == messageType && e.Subscriber == address)
                        {
                            Remove(address, messageType);

                            entries.Remove(e);

                            log.Debug("Subscriber " + address + " removed for message " + messageType + ".");
                        }
                    }
                }
            }
            return TaskEx.Completed;
        }

        /// <summary>
        /// Adds a message to the subscription store.
        /// </summary>
        public void Add(string subscriber, MessageType messageType)
        {
            var toSend = new Message
                         {
                             Formatter = q.Formatter,
                             Recoverable = true, 
                             Label = subscriber, 
                             Body = messageType.TypeName + ", Version=" + messageType.Version
                         };

            q.Send(toSend, GetTransactionType());

            AddToLookup(subscriber, messageType, toSend.Id);
        }

        /// <summary>
        /// Removes a message from the subscription store.
        /// </summary>
        public void Remove(string subscriber, MessageType messageType)
        {
            var messageId = RemoveFromLookup(subscriber, messageType);

            if (messageId == null)
                return;

            q.ReceiveById(messageId, GetTransactionType());
        }

        /// <summary>
        /// Checks if configuration is wrong - endpoint isn't transactional and
        /// object isn't configured to handle own transactions.
        /// </summary>
        bool ConfigurationIsWrong()
        {
            return (Transaction.Current == null && !DontUseExternalTransaction);
        }

        /// <summary>
        /// Returns the transaction type (automatic or single) that should be used
        /// based on the configuration of enlisting into external transactions.
        /// </summary>
        MessageQueueTransactionType GetTransactionType()
        {
            if (!TransactionsEnabled)
            {
                return MessageQueueTransactionType.None;
            }

            if (ConfigurationIsWrong())
            {
                throw new InvalidOperationException("This endpoint is not configured to be transactional. Processing subscriptions on a non-transactional endpoint is not supported by default. If you still wish to do so, please set the 'DontUseExternalTransaction' property of MsmqSubscriptionStorage to 'true'.\n\nThe recommended solution to this problem is to include '.IsTransaction(true)' after '.MsmqTransport()' in your fluent initialization code, or if you're using NServiceBus.Host.exe to have the class which implements IConfigureThisEndpoint to also inherit AsA_Server or AsA_Publisher.");
            }

            var t = MessageQueueTransactionType.Automatic;
            if (DontUseExternalTransaction)
            {
                t = MessageQueueTransactionType.Single;
            }

            return t;
        }

        /// <summary>
        /// Gets/sets whether or not to use a transaction started outside the
        /// subscription store.
        /// </summary>
        public virtual bool DontUseExternalTransaction { get; set; }

        /// <summary>
        /// Sets the address of the queue where subscription messages will be stored.
        /// For a local queue, just use its name - msmq specific info isn't needed.
        /// </summary>
        public MsmqAddress Queue{get;set;}

        /// <summary>
        /// Adds a message to the lookup to find message from
        /// subscriber, to message type, to message id
        /// </summary>
        void AddToLookup(string subscriber, MessageType typeName, string messageId)
        {
            lock (lookup)
            {
                Dictionary<MessageType, string> dictionary;
                if (!lookup.TryGetValue(subscriber, out dictionary))
                {
                    lookup[subscriber] = dictionary = new Dictionary<MessageType, string>();
                }

                if (!dictionary.ContainsKey(typeName))
                {
                    dictionary.Add(typeName, messageId);
                }
            }
        }

        string RemoveFromLookup(string subscriber, MessageType typeName)
        {
            string messageId = null;
            lock (lookup)
            {
                Dictionary<MessageType, string> endpoints;
                if (lookup.TryGetValue(subscriber, out endpoints))
                {
                    if (endpoints.TryGetValue(typeName, out messageId))
                    {
                        endpoints.Remove(typeName);
                        if (endpoints.Count == 0)
                        {
                            lookup.Remove(subscriber);
                        }
                    }
                }
            }
            return messageId;
        }

        MessageQueue q;

        /// <summary>
        /// lookup from subscriber, to message type, to message id.
        /// </summary>
        Dictionary<string, Dictionary<MessageType, string>> lookup = new Dictionary<string, Dictionary<MessageType, string>>(StringComparer.OrdinalIgnoreCase);

        List<Entry> entries = new List<Entry>();
        object locker = new object();

        static ILog log = LogManager.GetLogger(typeof(ISubscriptionStorage));

        public void Dispose()
        {
            // Filled in by Janitor.fody
        }
    }
}