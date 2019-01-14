using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NEventStore;

namespace AggregateSource.NEventStore
{
    /// <summary>
    /// Represents a virtual collection of <typeparamref name="TAggregateRoot"/>.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The type of the aggregate root in this collection.</typeparam>
    public class Repository<TAggregateRoot> : IAsyncRepository<TAggregateRoot> where TAggregateRoot : IAggregateRootEntity
    {
        readonly Func<TAggregateRoot> _rootFactory;
        readonly UnitOfWork _unitOfWork;
        readonly IStoreEvents _eventStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="Repository{TAggregateRoot}"/> class.
        /// </summary>
        /// <param name="rootFactory">The aggregate root entity factory.</param>
        /// <param name="unitOfWork">The unit of work to interact with.</param>
        /// <param name="eventStore">The event store to use.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="rootFactory"/> or <paramref name="unitOfWork"/> or <paramref name="eventStore"/> is null.</exception>
        public Repository(Func<TAggregateRoot> rootFactory, UnitOfWork unitOfWork, IStoreEvents eventStore)
        {
            _rootFactory = rootFactory ?? throw new ArgumentNullException(nameof(rootFactory));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        }

        /// <summary>
        /// Gets the aggregate root entity factory.
        /// </summary>
        /// <value>
        /// The aggregate root entity factory.
        /// </value>
        public Func<TAggregateRoot> RootFactory => _rootFactory;

        /// <summary>
        /// Gets the unit of work.
        /// </summary>
        /// <value>
        /// The unit of work.
        /// </value>
        public UnitOfWork UnitOfWork => _unitOfWork;

        /// <summary>
        /// Gets the event store to use.
        /// </summary>
        /// <value>
        /// The event store to use.
        /// </value>
        public IStoreEvents EventStore => _eventStore;

        /// <summary>
        /// Gets the aggregate root entity associated with the specified aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>An instance of <typeparamref name="TAggregateRoot"/>.</returns>
        /// <exception cref="AggregateNotFoundException">Thrown when an aggregate is not found.</exception>
        public async Task<TAggregateRoot> GetAsync(string identifier, CancellationToken cancellationToken)
        {
			var result = await GetOptionalAsync(identifier, cancellationToken)
                .ConfigureAwait(false);
            if (!result.HasValue)
            {
                throw new AggregateNotFoundException(identifier, typeof(TAggregateRoot));
            }

            return result.Value;
        }

        /// <summary>
        /// Attempts to get the aggregate root entity associated with the aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The found <typeparamref name="TAggregateRoot"/>, or empty if not found.</returns>
        public async Task<Optional<TAggregateRoot>> GetOptionalAsync(string identifier, CancellationToken cancellationToken)
        {
            if (_unitOfWork.TryGet(identifier, out var aggregate))
            {
                return new Optional<TAggregateRoot>((TAggregateRoot)aggregate.Root);
            }

            using (var stream = await _eventStore
                .OpenStreamAsync(identifier, cancellationToken, 0)
                .ConfigureAwait(false))
            {
                if (stream.StreamRevision == 0)
                {
                    return Optional<TAggregateRoot>.Empty;
                }

                var root = _rootFactory();
                root.Initialize(stream.CommittedEvents.Select(eventMessage => eventMessage.Body));
                _unitOfWork.Attach(new Aggregate(identifier, stream.StreamRevision, root));
                return new Optional<TAggregateRoot>(root);
            }
        }

        /// <summary>
        /// Adds the aggregate root entity to this collection using the specified aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <param name="root">The aggregate root entity.</param>
        public void Add(string identifier, TAggregateRoot root)
        {
            _unitOfWork.Attach(new Aggregate(identifier, 0, root));
        }
    }
}
