using System;
using System.Threading;
using System.Threading.Tasks;
using AggregateSource.NEventStore.Framework;
using AggregateSource.NEventStore.Framework.Snapshots;
using NEventStore;
using NUnit.Framework;

namespace AggregateSource.NEventStore.Snapshots
{
    namespace SnapshotableRepositoryTests
    {
        [TestFixture]
        public class Construction
        {
            IStoreEvents _eventStore;
            UnitOfWork _unitOfWork;
            Func<SnapshotableAggregateRootEntityStub> _factory;

            [SetUp]
            public void SetUp()
            {
                _eventStore = Wireup.Init().UsingInMemoryPersistence().Build();
                _unitOfWork = new UnitOfWork();
                _factory = SnapshotableAggregateRootEntityStub.Factory;
            }

            [Test]
            public void FactoryCanNotBeNull()
            {
                AssertEx.ThrowsAsync<ArgumentNullException>(
                    () => new SnapshotableRepository<SnapshotableAggregateRootEntityStub>(null, _unitOfWork, _eventStore));
            }

            [Test]
            public void UnitOfWorkCanNotBeNull()
            {
                AssertEx.ThrowsAsync<ArgumentNullException>(
                    () => new SnapshotableRepository<SnapshotableAggregateRootEntityStub>(_factory, null, _eventStore));
            }

            [Test]
            public void EventStoreCanNotBeNull()
            {
                AssertEx.ThrowsAsync<ArgumentNullException>(
                    () => new SnapshotableRepository<SnapshotableAggregateRootEntityStub>(_factory, _unitOfWork, null));
            }
        }

        [TestFixture]
        public class WithEmptyStoreAndEmptyUnitOfWorkAndNoSnapshot
        {
            Task<SnapshotableRepository<SnapshotableAggregateRootEntityStub>> _sut;
            UnitOfWork _unitOfWork;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _unitOfWork = new UnitOfWork();
                _sut = new RepositoryScenarioBuilder().
                    WithUnitOfWork(_unitOfWork).
                    BuildForSnapshotableRepository();
            }

            [Test]
            public async Task GetThrows()
            {
				var sut = await _sut;
                var exception =
                    AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(SnapshotableAggregateRootEntityStub)));
            }

            [Test]
            public async Task GetOptionalReturnsEmpty()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.UnknownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<SnapshotableAggregateRootEntityStub>.Empty));
            }

            [Test]
            public async Task AddAttachesToUnitOfWork()
            {
				var sut = await _sut;
				var root = SnapshotableAggregateRootEntityStub.Factory();

                sut.Add(_model.KnownIdentifier, root);

                Aggregate aggregate;
                var result = _unitOfWork.TryGet(_model.KnownIdentifier, out aggregate);
                Assert.That(result, Is.True);
                Assert.That(aggregate.Identifier, Is.EqualTo(_model.KnownIdentifier));
                Assert.That(aggregate.Root, Is.SameAs(root));
            }
        }


        [TestFixture]
        public class WithEmptyStoreAndEmptyUnitOfWorkAndSnapshot
        {
            Task<SnapshotableRepository<SnapshotableAggregateRootEntityStub>> _sut;
            UnitOfWork _unitOfWork;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _unitOfWork = new UnitOfWork();
                _sut = new RepositoryScenarioBuilder().
                    WithUnitOfWork(_unitOfWork).
                    ScheduleSnapshots(new Snapshot(_model.KnownIdentifier, 100, new object())).
                    BuildForSnapshotableRepository();
            }

            [Test]
            public async Task GetThrows()
            {
				var sut = await _sut;
				var exception =
                    AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(SnapshotableAggregateRootEntityStub)));
            }

            [Test]
            public async Task GetOptionalReturnsEmpty()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.UnknownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<SnapshotableAggregateRootEntityStub>.Empty));
            }

            [Test]
            public async Task AddAttachesToUnitOfWork()
            {
				var sut = await _sut;
				var root = SnapshotableAggregateRootEntityStub.Factory();

                sut.Add(_model.KnownIdentifier, root);

                Aggregate aggregate;
                var result = _unitOfWork.TryGet(_model.KnownIdentifier, out aggregate);
                Assert.That(result, Is.True);
                Assert.That(aggregate.Identifier, Is.EqualTo(_model.KnownIdentifier));
                Assert.That(aggregate.Root, Is.SameAs(root));
            }
        }

        [TestFixture]
        public class WithEmptyStoreAndFilledUnitOfWorkAndNoSnapshot
        {
            Task<SnapshotableRepository<SnapshotableAggregateRootEntityStub>> _sut;
            UnitOfWork _unitOfWork;
            SnapshotableAggregateRootEntityStub _root;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _root = SnapshotableAggregateRootEntityStub.Factory();
                _unitOfWork = new UnitOfWork();
                _unitOfWork.Attach(new Aggregate(_model.KnownIdentifier, 0, _root));
                _sut = new RepositoryScenarioBuilder()
					.WithUnitOfWork(_unitOfWork)
					.BuildForSnapshotableRepository();
            }

            [Test]
            public async Task GetThrowsForUnknownId()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(SnapshotableAggregateRootEntityStub)));
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

                Assert.That(result, Is.EqualTo(Optional<SnapshotableAggregateRootEntityStub>.Empty));
            }

            [Test]
			public async Task GetOptionalReturnsRootForKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(new Optional<SnapshotableAggregateRootEntityStub>(_root)));
            }
        }

        [TestFixture]
        public class WithEmptyStoreAndFilledUnitOfWorkAndSnapshot
        {
            Task<SnapshotableRepository<SnapshotableAggregateRootEntityStub>> _sut;
            UnitOfWork _unitOfWork;
            SnapshotableAggregateRootEntityStub _root;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _root = SnapshotableAggregateRootEntityStub.Factory();
                _unitOfWork = new UnitOfWork();
                _unitOfWork.Attach(new Aggregate(_model.KnownIdentifier, 0, _root));
                _sut = new RepositoryScenarioBuilder().
                    WithUnitOfWork(_unitOfWork).
                    ScheduleSnapshots(new Snapshot(_model.KnownIdentifier, 100, new object())).
                    BuildForSnapshotableRepository();
            }

            [Test]
            public async Task GetThrowsForUnknownId()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(SnapshotableAggregateRootEntityStub)));
            }

            [Test]
			public async Task GetReturnsRootOfKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result, Is.SameAs(_root));
            }

            [Test]
			public async Task GetReturnsRootOfKnownIdNotRestoredFromSnapshot()
            {
				var sut = await _sut;
				var result = await sut.GetAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result.RecordedSnapshot, Is.Null);
            }

            [Test]
			public async Task GetOptionalReturnsEmptyForUnknownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.UnknownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<SnapshotableAggregateRootEntityStub>.Empty));
            }

            [Test]
			public async Task GetOptionalReturnsRootForKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(new Optional<SnapshotableAggregateRootEntityStub>(_root)));
            }

            [Test]
			public async Task GetOptionalReturnsRootForKnownIdNotRestoredFromSnapshot()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result.Value.RecordedSnapshot, Is.Null);
            }
        }

        [TestFixture]
        public class WithFilledStoreAndNoSnapshot
        {
            Task<SnapshotableRepository<SnapshotableAggregateRootEntityStub>> _sut;
            UnitOfWork _unitOfWork;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _unitOfWork = new UnitOfWork();
                _sut = new RepositoryScenarioBuilder().
                    WithUnitOfWork(_unitOfWork).
                    ScheduleAppendToStream(_model.KnownIdentifier, new EventStub(1)).
                    BuildForSnapshotableRepository();
            }

            [Test]
            public async Task GetThrowsForUnknownId()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(SnapshotableAggregateRootEntityStub)));
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

                Assert.That(result, Is.EqualTo(Optional<SnapshotableAggregateRootEntityStub>.Empty));
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
        public class WithFilledStoreAndSnapshot
        {
            Task<SnapshotableRepository<SnapshotableAggregateRootEntityStub>> _sut;
            UnitOfWork _unitOfWork;
            Model _model;

            [SetUp]
            public void SetUp()
            {
                _model = new Model();
                _unitOfWork = new UnitOfWork();
                _sut = new RepositoryScenarioBuilder().
                    WithUnitOfWork(_unitOfWork).
                    ScheduleAppendToStream(_model.KnownIdentifier, new EventStub(1)).
                    ScheduleSnapshots(new Snapshot(_model.KnownIdentifier, 1, new State(1))).
                    ScheduleAppendToStream(_model.KnownIdentifier, new EventStub(2)).
                    BuildForSnapshotableRepository();
            }

            [Test]
            public async Task GetThrowsForUnknownId()
            {
				var sut = await _sut;
				var exception =
					AssertEx.ThrowsAsync<AggregateNotFoundException>(
                        () => sut.GetAsync(_model.UnknownIdentifier, CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(exception.Identifier, Is.EqualTo(_model.UnknownIdentifier));
                Assert.That(exception.ClrType, Is.EqualTo(typeof(SnapshotableAggregateRootEntityStub)));
            }

            [Test]
			public async Task GetReturnsRootOfKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result.RecordedEvents, Is.EquivalentTo(new[] { new EventStub(2) }));
            }

            [Test]
			public async Task GetReturnsRootRestoredFromSnapshot()
            {
				var sut = await _sut;
				var result = await sut.GetAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result.RecordedSnapshot, Is.EqualTo(new State(1)));
            }

            [Test]
			public async Task GetOptionalReturnsEmptyForUnknownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.UnknownIdentifier, CancellationToken.None);

                Assert.That(result, Is.EqualTo(Optional<SnapshotableAggregateRootEntityStub>.Empty));
            }

            [Test]
			public async Task GetOptionalReturnsRootForKnownId()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result.HasValue, Is.True);
                Assert.That(result.Value.RecordedEvents, Is.EquivalentTo(new[] { new EventStub(2) }));
            }

            [Test]
			public async Task GetOptionalReturnsRootForKnownIdRestoredFromSnapshot()
            {
				var sut = await _sut;
				var result = await sut.GetOptionalAsync(_model.KnownIdentifier, CancellationToken.None);

                Assert.That(result.Value.RecordedSnapshot, Is.EqualTo(new State(1)));
            }

            class State
            {
                bool Equals(State other)
                {
                    return _value == other._value;
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    if (ReferenceEquals(this, obj)) return true;
                    if (obj.GetType() != GetType()) return false;
                    return Equals((State) obj);
                }

                public override int GetHashCode()
                {
                    return _value;
                }

                readonly int _value;

                public State(int value)
                {
                    _value = value;
                }
            }
        }
    }
}