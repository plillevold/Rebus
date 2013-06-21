using System;
using System.Collections.Generic;

namespace Rebus.Bus
{
    class RebusEvents : IRebusEvents
    {
        readonly List<IUnitOfWorkManager> unitOfWorkManagers = new List<IUnitOfWorkManager>();

        public RebusEvents()
        {
            MessageMutators = new List<IMutateMessages>();
        }

        public event MessageSentEventHandler MessageSent = delegate { };

        public event BeforeMessageEventHandler BeforeMessage = delegate { };

        public event AfterMessageEventHandler AfterMessage = delegate { };

        public event UncorrelatedMessageEventHandler UncorrelatedMessage = delegate { };

        public event MessageContextEstablishedEventHandler MessageContextEstablished = delegate { };

        public event BeforeTransportMessageEventHandler BeforeTransportMessage = delegate { };

        public event AfterTransportMessageEventHandler AfterTransportMessage = delegate { };

        public event PoisonMessageEventHandler PoisonMessage = delegate { };

        public ICollection<IMutateMessages> MessageMutators { get; private set; }

        public void AddUnitOfWorkManager(IUnitOfWorkManager unitOfWorkManager)
        {
            unitOfWorkManagers.Add(unitOfWorkManager);
        }

        internal IEnumerable<IUnitOfWorkManager> UnitOfWorkManagers
        {
            get { return unitOfWorkManagers; }
        }

        internal void RaiseMessageContextEstablished(IBus bus, IMessageContext messageContext)
        {
            MessageContextEstablished(bus, messageContext);
        }

        internal void RaiseMessageSent(IBus bus, string destination, object message)
        {
            MessageSent(bus, destination, message);
        }

        internal void RaiseBeforeMessage(IBus bus, object message)
        {
            BeforeMessage(bus, message);
        }

        internal void RaiseAfterMessage(IBus bus, Exception exception, object message)
        {
            AfterMessage(bus, exception, message);
        }

        internal void RaiseBeforeTransportMessage(IBus bus, ReceivedTransportMessage transportMessage)
        {
            BeforeTransportMessage(bus, transportMessage);
        }

        internal void RaiseAfterTransportMessage(IBus bus, Exception exception, ReceivedTransportMessage transportMessage)
        {
            AfterTransportMessage(bus, exception, transportMessage);
        }

        internal void RaisePoisonMessage(IBus bus, ReceivedTransportMessage transportMessage, PoisonMessageInfo poisonMessageInfo)
        {
            PoisonMessage(bus, transportMessage, poisonMessageInfo);
        }

        internal void RaiseUncorrelatedMessage(IBus bus, object message, Saga saga)
        {
            UncorrelatedMessage(bus, message, saga);
        }
    }
}