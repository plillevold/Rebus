﻿using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebus.Bus;
using Rebus.Configuration;
using Rebus.Logging;
using Rebus.Tests.Contracts.ContainerAdapters.Factories;
using Shouldly;
using System.Linq;

namespace Rebus.Tests.Contracts.ContainerAdapters
{
    [TestFixture(typeof(WindsorContainerAdapterFactory))]
    [TestFixture(typeof(StructureMapContainerAdapterFactory))]
    [TestFixture(typeof(AutofacContainerAdapterFactory))]
    [TestFixture(typeof(UnityContainerAdapterFactory))]
    [TestFixture(typeof(NinjectContainerAdapterFactory))]
    [TestFixture(typeof(BuiltinContainerAdapterFactory))]
    public class TestContainerAdapters<TFactory> : FixtureBase where TFactory : IContainerAdapterFactory, new()
    {
        IContainerAdapter adapter;
        TFactory factory;

        protected override void DoSetUp()
        {
            SomeDisposableSingleton.Reset();
            SomeDisposableHandler.Reset();
            Console.WriteLine("Running setup for {0}", typeof(TFactory));
            factory = new TFactory();
            adapter = factory.Create();
            RebusLoggerFactory.Current = new ConsoleLoggerFactory(false);
        }

        [Test]
        public void NothingHappensWhenDisposingAnEmptyContainerAdapter()
        {
            Assert.DoesNotThrow(() => factory.DisposeInnerContainer());
        }

        [Test]
        public void MultipleCallsToGetYieldsNewInstances()
        {
            // arrange
            factory.Register<IHandleMessages<string>, SomeDisposableHandler>();
            factory.StartUnitOfWork();
            var firstInstance = adapter.GetHandlerInstancesFor<string>()
                                       .Single();

            // act
            var nextInstance = adapter.GetHandlerInstancesFor<string>()
                                      .Single();

            // assert
            nextInstance.ShouldNotBeSameAs(firstInstance);
        }

        [Test]
        public void CanGetHandlerInstancesAndReleaseThemAfterwardsAsExpected()
        {
            // arrange
            factory.Register<IHandleMessages<string>, SomeDisposableHandler>();
            factory.StartUnitOfWork();
            
            // act
            var instances = adapter.GetHandlerInstancesFor<string>();
            adapter.Release(instances);
            factory.EndUnitOfWork();

            // assert
            SomeDisposableHandler.WasDisposed.ShouldBe(true);
        }

        class SomeDisposableHandler : IHandleMessages<string>, IDisposable
        {
            public static bool WasDisposed { get; private set; }

            public void Handle(string message)
            {
            }

            public static void Reset()
            {
                WasDisposed = false;
            }

            public void Dispose()
            {
                WasDisposed = true;
            }
        }

        [Test]
        public void BusIsDisposedWhenContainerIsDisposed()
        {
            // arrange
            var disposableBus = new SomeDisposableSingleton();
            SomeDisposableSingleton.Disposed.ShouldBe(false);
            adapter.SaveBusInstances(disposableBus);

            // act
            factory.DisposeInnerContainer();

            // assert
            SomeDisposableSingleton.Disposed.ShouldBe(true);
        }

        class SomeDisposableSingleton : IBus, IAdvancedBus
        {
            public static bool Disposed { get; set; }

            public SomeDisposableSingleton()
            {
                Events = new SomeTestRebusEvents();
            }

            public void Dispose()
            {
                Disposed = true;
            }

            public static void Reset()
            {
                Disposed = false;
            }

            public void Send<TCommand>(TCommand message)
            {
                throw new NotImplementedException();
            }

            public void SendLocal<TCommand>(TCommand message)
            {
                throw new NotImplementedException();
            }

            public void Reply<TResponse>(TResponse message)
            {
                throw new NotImplementedException();
            }

            public void Subscribe<TEvent>()
            {
                throw new NotImplementedException();
            }

            public void Unsubscribe<T>()
            {
                throw new NotImplementedException();
            }

            public void Publish<TEvent>(TEvent message)
            {
                throw new NotImplementedException();
            }

            public void Defer(TimeSpan delay, object message)
            {
                throw new NotImplementedException();
            }

            public void AttachHeader(object message, string key, string value)
            {
                throw new NotImplementedException();
            }

            public IAdvancedBus Advanced { get { return this; } }

            public IRebusEvents Events { get; private set; }
            public IRebusBatchOperations Batch { get; private set; }
            public IRebusRouting Routing { get; private set; }

            class SomeTestRebusEvents : IRebusEvents
            {
                public event BeforeTransportMessageEventHandler BeforeTransportMessage;
                public event AfterTransportMessageEventHandler AfterTransportMessage;
                public event PoisonMessageEventHandler PoisonMessage;
                public event MessageSentEventHandler MessageSent;
                public event BeforeMessageEventHandler BeforeMessage;
                public event AfterMessageEventHandler AfterMessage;
                public event UncorrelatedMessageEventHandler UncorrelatedMessage;
                public event MessageContextEstablishedEventHandler MessageContextEstablished;
                public ICollection<IMutateMessages> MessageMutators { get; private set; }
                public void AddUnitOfWorkManager(IUnitOfWorkManager unitOfWorkManager)
                {
                }
            }
        }
    }
}