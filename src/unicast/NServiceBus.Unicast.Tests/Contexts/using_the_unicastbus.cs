namespace NServiceBus.Unicast.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using MasterNode;
    using MessageInterfaces.MessageMapper.Reflection;
    using MessageMutator;
    using NUnit.Framework;
    using ObjectBuilder;
    using Queuing;
    using Rhino.Mocks;
    using Serialization;
    using Subscriptions;
    using Transport;

    public class using_a_configured_unicastbus
    {
        protected IBus bus;

        protected UnicastBus unicastBus;
        protected ISendMessages messageSender;
        protected FakeSubscriptionStorage subscriptionStorage;

        protected Address gatewayAddress;
        MessageHeaderManager headerManager = new MessageHeaderManager();
        MessageMapper messageMapper = new MessageMapper();

        [SetUp]
        public void SetUp()
        {
            string localAddress = "endpointA";
            Address masterNodeAddress = localAddress + "@MasterNode";

            try
            {
                Address.InitializeLocalAddress(localAddress);
            }
            catch // intentional
            {
            }

            ExtensionMethods.GetStaticOutgoingHeadersAction = () => MessageHeaderManager.staticHeaders;
            gatewayAddress = masterNodeAddress.SubScope("gateway");

            messageSender = MockRepository.GenerateStub<ISendMessages>();
            var masterNodeManager = MockRepository.GenerateStub<IManageTheMasterNode>();
            var builder = MockRepository.GenerateStub<IBuilder>();

            subscriptionStorage = new FakeSubscriptionStorage();

            builder.Stub(x => x.BuildAll<IMutateOutgoingMessages>()).Return(new IMutateOutgoingMessages[] { });

            builder.Stub(x => x.BuildAll<IMutateOutgoingTransportMessages>()).Return(new IMutateOutgoingTransportMessages[] { headerManager });

            masterNodeManager.Stub(x => x.GetMasterNode()).Return(masterNodeAddress);
            unicastBus = new UnicastBus
            {
                MessageSerializer = MockRepository.GenerateStub<IMessageSerializer>(),
                Builder = builder,
                MasterNodeManager = masterNodeManager,
                MessageSender = messageSender,
                Transport = MockRepository.GenerateStub<ITransport>(),
                SubscriptionStorage = subscriptionStorage,
                AutoSubscribe = true,
                MessageMapper = messageMapper
            };
            bus = unicastBus;
            ExtensionMethods.SetHeaderAction = headerManager.SetHeader;

        }

        protected void RegisterMessageHandlerType<T>()
        {
            unicastBus.MessageHandlerTypes = new[] { typeof(T) };
        }

        protected Address RegisterMessageType<T>()
        {
            var address = new Address(typeof (T).Name, "localhost");
            RegisterMessageType<T>(address);

            return address;
        }

        protected void RegisterMessageType<T>(Address address)
        {
            if(typeof(T).IsInterface)
                messageMapper.Initialize(new[]{typeof(T)});
            unicastBus.RegisterMessageType(typeof(T), address, false);

        }

        protected void StartBus()
        {
            ((IStartableBus)bus).Start();
        }
  
    }
    
    public class using_the_unicastbus:using_a_configured_unicastbus
    {
        [SetUp]
        public void SetUp()
        {
            StartBus();
        }
    }

    public class FakeSubscriptionStorage : ISubscriptionStorage
    {
        void ISubscriptionStorage.Subscribe(string client, IEnumerable<string> messageTypes)
        {
            ((ISubscriptionStorage)this).Subscribe(Address.Parse(client), messageTypes);
        }

        void ISubscriptionStorage.Subscribe(Address address, IEnumerable<string> messageTypes)
        {
            messageTypes.ToList().ForEach(m =>
            {
                if (!storage.ContainsKey(m))
                    storage[m] = new List<Address>();

                if (!storage[m].Contains(address))
                    storage[m].Add(address);
            });
        }

        void ISubscriptionStorage.Unsubscribe(string client, IEnumerable<string> messageTypes)
        {
            ((ISubscriptionStorage)this).Unsubscribe(Address.Parse(client), messageTypes);
        }

        void ISubscriptionStorage.Unsubscribe(Address address, IEnumerable<string> messageTypes)
        {
            messageTypes.ToList().ForEach(m =>
            {
                if (storage.ContainsKey(m))
                    storage[m].Remove(address);
            });
        }

        IEnumerable<string> ISubscriptionStorage.GetSubscribersForMessage(IEnumerable<string> messageTypes)
        {
            return ((ISubscriptionStorage)this).GetSubscriberAddressesForMessage(messageTypes)
                .Select(a => a.ToString());
        }

        IEnumerable<Address> ISubscriptionStorage.GetSubscriberAddressesForMessage(IEnumerable<string> messageTypes)
        {
            var result = new List<Address>();
            messageTypes.ToList().ForEach(m =>
            {
                if (storage.ContainsKey(m))
                    result.AddRange(storage[m]);
            });

            return result;
        }
        public void FakeSubscribe<T>(Address address)
        {
            ((ISubscriptionStorage)this).Subscribe(address, new[] { typeof(T).AssemblyQualifiedName });  
        }

        public void Init()
        {
        }

        private readonly Dictionary<string, List<Address>> storage = new Dictionary<string, List<Address>>();
    }
}