using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AggregateSource.NEventStore.Framework.Snapshots;
using AggregateSource.NEventStore.Snapshots;
using NEventStore;

namespace AggregateSource.NEventStore.Framework
{
    public class RepositoryScenarioBuilder
    {
        readonly IStoreEvents _eventStore;
        readonly List<Func<IStoreEvents, Task>> _eventStoreSchedule;
        readonly List<Action<UnitOfWork>> _unitOfWorkSchedule;
        UnitOfWork _unitOfWork;

        public RepositoryScenarioBuilder()
        {
            _eventStore = Wireup.Init().UsingInMemoryPersistence().Build();
            _unitOfWork = new UnitOfWork();
            _eventStoreSchedule = new List<Func<IStoreEvents, Task>>();
            _unitOfWorkSchedule = new List<Action<UnitOfWork>>();
        }

        public RepositoryScenarioBuilder WithUnitOfWork(UnitOfWork value)
        {
            _unitOfWork = value;
            return this;
        }

        public RepositoryScenarioBuilder ScheduleAppendToStream(string stream, params object[] events)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (events == null) throw new ArgumentNullException(nameof(events));
            _eventStoreSchedule.Add(
                async store =>
                {
                    using (var _ = await store
                        .OpenStreamAsync(stream, CancellationToken.None, 0)
                        .ConfigureAwait(false))
                    {
                        foreach (var @event in events)
                        {
                            _.Add(new EventMessage { Body = @event });
                        }

                        await _
                            .CommitChangesAsync(Guid.NewGuid(), CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                });
            return this;
        }

        public RepositoryScenarioBuilder ScheduleSnapshots(params Snapshot[] snapshots)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            _eventStoreSchedule.Add(
                async store =>
                {
					foreach (var snapshot in snapshots)
					{
						await store.Advanced
                            .AddSnapshotAsync(snapshot, CancellationToken.None)
                            .ConfigureAwait(false);
					}
                });
            return this;
        }

        public RepositoryScenarioBuilder ScheduleDeleteStream(string stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            _eventStoreSchedule.Add(store => store.Advanced
                .DeleteStreamAsync(Bucket.Default, stream, CancellationToken.None));
            return this;
        }

        public RepositoryScenarioBuilder ScheduleAttachToUnitOfWork(Aggregate aggregate)
        {
            if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));
            _unitOfWorkSchedule.Add(uow => uow.Attach(aggregate));
            return this;
        }

        public async Task<Repository<AggregateRootEntityStub>> BuildForRepository()
        {
            await ExecuteScheduledActions();
            return new Repository<AggregateRootEntityStub>(
                AggregateRootEntityStub.Factory,
                _unitOfWork,
                _eventStore);
        }

        public async Task<SnapshotableRepository<SnapshotableAggregateRootEntityStub>> BuildForSnapshotableRepository()
        {
            await ExecuteScheduledActions();
            return new SnapshotableRepository<SnapshotableAggregateRootEntityStub>(
                SnapshotableAggregateRootEntityStub.Factory,
                _unitOfWork,
                _eventStore);
        }

        private async Task ExecuteScheduledActions()
        {
            foreach (var action in _eventStoreSchedule)
            {
                await action(_eventStore);
            }
            foreach (var action in _unitOfWorkSchedule)
            {
                action(_unitOfWork);
            }
        }
    }
}