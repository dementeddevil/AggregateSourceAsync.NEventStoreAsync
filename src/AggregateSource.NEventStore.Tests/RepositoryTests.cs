using System;
using System.Threading;
using System.Threading.Tasks;
using AggregateSource.NEventStore.Framework;
using NEventStore;
using NUnit.Framework;

namespace AggregateSource.NEventStore
{
    namespace RepositoryTests
    {
        [TestFixture]
        public class Construction
        {
            UnitOfWork _unitOfWork;
            Func<AggregateRootEntityStub> _factory;
            IStoreEvents _store;

            [SetUp]
            public void SetUp()
            {
                _store = Wireup.Init().UsingInMemoryPersistence().Build();
                _unitOfWork = new UnitOfWork();
                _factory = AggregateRootEntityStub.Factory;
            }

            [Test]
            public void FactoryCanNotBeNull()
            {
                AssertEx.ThrowsAsync<ArgumentNullException>(
                    () => new Repository<AggregateRootEntityStub>(null, _unitOfWork, _store));
            }

            [Test]
            public void UnitOfWorkCanNotBeNull()
            {
                AssertEx.ThrowsAsync<ArgumentNullException>(
                    () => new Repository<AggregateRootEntityStub>(_factory, null, _store));
            }

            [Test]
            public void EventStoreCanNotBeNull()
            {
                AssertEx.ThrowsAsync<ArgumentNullException>(
                    () => new Repository<AggregateRootEntityStub>(_factory, _unitOfWork, null));
            }

            [Test]
            public void UsingCtorReturnsInstanceWithExpectedProperties()
            {
                var sut = new Repository<AggregateRootEntityStub>(_factory, _unitOfWork, _store);
                Assert.That(sut.RootFactory, Is.SameAs(_factory));
                Assert.That(sut.UnitOfWork, Is.SameAs(_unitOfWork));
                Assert.That(sut.EventStore, Is.SameAs(_store));
            }
        }

        [TestFixture]
        public class WithEmptyStoreAndEmptyUnitOfWork
        {
            Task<Repository<AggregateRootEntityStub>> _sut;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _sut = new RepositoryScenarioBuilder()
					.BuildForRepository();
            }

            [Test]
            public async Task GetThrows()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(AggregateRootEntityStub)));
            }

            [Test]
            public async Task GetOptionalReturnsEmpty()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.UnknownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<AggregateRootEntityStub>.Empty));
            }

            [Test]
            public async Task AddAttachesToUnitOfWork()
            {
				var sut = await _sut;
				var root = AggregateRootEntityStub.Factory();

                sut.Add(_model.KnownIdentifier, root);

                Aggregate aggregate;
                var result = sut.UnitOfWork.TryGet(_model.KnownIdentifier, out aggregate);
                Assert.That(result, Is.True);
                Assert.That(aggregate.Identifier, Is.EqualTo(_model.KnownIdentifier));
                Assert.That(aggregate.Root, Is.SameAs(root));
            }
        }

        [TestFixture]
        public class WithEmptyStoreAndFilledUnitOfWork
        {
            Task<Repository<AggregateRootEntityStub>> _sut;
            AggregateRootEntityStub _root;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _root = AggregateRootEntityStub.Factory();
                _sut = new RepositoryScenarioBuilder().
                    ScheduleAttachToUnitOfWork(new Aggregate(_model.KnownIdentifier, 0, _root)).
                    BuildForRepository();
            }

            [Test]
            public async Task GetThrowsForUnknownId()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(AggregateRootEntityStub)));
            }

            [Test]
            public async Task GetReturnsRootOfKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result, Is.SameAs(_root));
            }

            [Test]
            public async Task GetOptionalReturnsEmptyForUnknownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.UnknownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<AggregateRootEntityStub>.Empty));
            }

            [Test]
            public async Task GetOptionalReturnsRootForKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(new Optional<AggregateRootEntityStub>(_root)));
            }
        }

        [TestFixture]
        public class WithStreamPresentInStore
        {
            Task<Repository<AggregateRootEntityStub>> _sut;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _sut = new RepositoryScenarioBuilder().
                    ScheduleAppendToStream(_model.KnownIdentifier, new EventStub(1)).
                    BuildForRepository();
            }

            [Test]
            public async Task GetThrowsForUnknownId()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(AggregateRootEntityStub)));
            }

            [Test]
            public async Task GetReturnsRootOfKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result.RecordedEvents, Is.EquivalentTo(new[] { new EventStub(1) }));
            }

            [Test]
            public async Task GetOptionalReturnsEmptyForUnknownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.UnknownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<AggregateRootEntityStub>.Empty));
            }

            [Test]
            public async Task GetOptionalReturnsRootForKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result.HasValue, Is.True);
                Assert.That(result.Value.RecordedEvents, Is.EquivalentTo(new[] { new EventStub(1) }));
            }
        }

        [TestFixture]
        public class WithDeletedStreamInStore
        {
            Task<Repository<AggregateRootEntityStub>> _sut;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _sut = new RepositoryScenarioBuilder().
                    ScheduleAppendToStream(_model.KnownIdentifier, new EventStub(1)).
                    ScheduleDeleteStream(_model.KnownIdentifier).
                    BuildForRepository();
            }

            [Test]
            public async Task GetThrowsForUnknownId()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(AggregateRootEntityStub)));
            }

            [Test]
            public async Task GetThrowsForKnownDeletedId()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.KnownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.KnownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(AggregateRootEntityStub)));
            }

            [Test]
            public async Task GetOptionalReturnsEmptyForUnknownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.UnknownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<AggregateRootEntityStub>.Empty));
            }

            [Test]
            public async Task GetOptionalReturnsEmptyForKnownDeletedId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<AggregateRootEntityStub>.Empty));
            }
        }
    }
}