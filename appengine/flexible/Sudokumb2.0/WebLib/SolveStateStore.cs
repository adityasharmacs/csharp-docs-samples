
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Datastore.V1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sudokumb;

namespace Sudokumb
{
    // Represents the status of one puzzle as its being solved.
    public class SolveState
    {
        /// <summary>
        ///  Null means the puzzle hasn't been completely solved.
        /// </summary>
        public GameBoard Solution { get; set; }
        /// <summary>
        /// How many game boards have been examined while searching for the
        /// solution?
        /// </summary>
        public long BoardsExaminedCount { get; set; }
    }

    public class SolveStateStore
    {
        const string SOLUTION_KIND = "Solution";
        readonly DatastoreDb _datastore;
        readonly KeyFactory _solutionKeyFactory;
        readonly DatastoreCounter _datastoreCounter;
        readonly IMemoryCache _cache;
        readonly ILogger _logger;
        readonly ICounter _locallyExaminedBoardCount = new InterlockedCounter();

        public long LocallyExaminedBoardCount
        {
            get => _locallyExaminedBoardCount.Count;
            private set {}
        }

        public SolveStateStore(DatastoreDb datastore,
            DatastoreCounter datastoreCounter, IMemoryCache cache,
            ILogger<SolveStateStore> logger)
        {
            _datastore = datastore;
            _datastoreCounter = datastoreCounter;
            _cache = cache;
            _logger = logger;
            _solutionKeyFactory = new KeyFactory(datastore.ProjectId,
                datastore.NamespaceId, SOLUTION_KIND);
        }

        public async Task<SolveState> GetAsync(string solveRequestId,
            CancellationToken cancellationToken)
        {
            Entity entity = await _datastore.LookupAsync(
                _solutionKeyFactory.CreateKey(solveRequestId));
            var solveState = new SolveState()
            {
                BoardsExaminedCount = await _datastoreCounter
                    .GetCountAsync(solveRequestId, cancellationToken)
            };
            if (null != entity && entity.Properties.ContainsKey(SOLUTION_KIND))
            {
                solveState.Solution = GameBoard.Create(
                    (string)entity[SOLUTION_KIND]);
            }
            return solveState;
        }

        public Task<SolveState> GetCachedAsync(string solveRequestId,
            CancellationToken cancellationToken)
        {
            return _cache.GetOrCreate<Task<SolveState>>(solveRequestId,
            entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromSeconds(1);
                return GetAsync(solveRequestId, cancellationToken);
            });
        }

        public Task SetAsync(string solveRequestId, GameBoard gameBoard,
            CancellationToken cancellationToken)
        {
            Entity entity = new Entity()
            {
                Key = _solutionKeyFactory.CreateKey(solveRequestId),
                [SOLUTION_KIND] = gameBoard.Board
            };
            entity[SOLUTION_KIND].ExcludeFromIndexes = true;
            return _datastore.UpsertAsync(entity,
                CallSettings.FromCancellationToken(cancellationToken));
        }

        public void IncreaseExaminedBoardCount(string solveRequestId,
            long amount)
        {
            _locallyExaminedBoardCount.Increase(amount);
            _datastoreCounter.GetLocalCounter(solveRequestId).Increase(amount);
        }
    }
}