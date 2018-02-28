
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Datastore.V1;
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

    public class SolveStateStore : IHostedService
    {
        const string SOLUTION_KIND = "Solution";
        readonly DatastoreDb _datastore;
        KeyFactory _solutionKeyFactory;
        readonly DatastoreCounter _datastoreCounter;
        CancellationTokenSource _cancelHostedService;
        Task _hostedService;
        ILogger _logger;

        ConcurrentDictionary<string, ICounter> _examinedBoardCounts
             = new ConcurrentDictionary<string, ICounter>();

        public SolveStateStore(DatastoreDb datastore,
            DatastoreCounter datastoreCounter,
            ILogger<SolveStateStore> logger)
        {
            _datastore = datastore;
            _datastoreCounter = datastoreCounter;
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

        public Task SetAsync(string solveRequestId, GameBoard gameBoard)
        {
            Entity entity = new Entity()
            {
                Key = _solutionKeyFactory.CreateKey(solveRequestId),
                [SOLUTION_KIND] = gameBoard.Board
            };
            return _datastore.UpsertAsync(entity);
        }

        // /////////////////////////////////////////////////////////////////////
        // IHostedService implementation periodically saves examined game board
        // counts to datastore.
        public Task StartAsync(CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.Assert(null == _cancelHostedService);
            _cancelHostedService = new CancellationTokenSource();
            _hostedService = Task.Run(async() => await HostedServiceMainAsync(
                _cancelHostedService.Token));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancelHostedService.Cancel();
            return _hostedService;
        }

        public void IncreaseExaminedBoardCount(string solveRequestId,
            long amount)
        {
            ICounter counter = _examinedBoardCounts.GetOrAdd(solveRequestId,
                (key) => (ICounter) new InterlockedCounter());
            counter.Increase(amount);
        }

        async Task ReportExaminedBoardCountsAsync(CancellationToken
            cancellationToken)
        {
            List<Task> tasks = new List<Task>();
            foreach (var keyValue in _examinedBoardCounts)
            {
                long count = keyValue.Value.Count;
                if (count > 0)
                {
                    tasks.Add(Task.Run(async() =>
                    {
                        await _datastoreCounter.IncreaseAsync(keyValue.Key,
                            count, cancellationToken);
                        keyValue.Value.Increase(-count);  // Reset to 0.
                    }));
                }
            }
            foreach (Task task in tasks)
            {
                await task;
            }
        }

        public async Task HostedServiceMainAsync(
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("SolveStateStore.HostedServiceMainAsync()");
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                try 
                {
                    await Task.Delay(1000, cancellationToken);
                    await ReportExaminedBoardCountsAsync(cancellationToken);
                }
                catch (Exception e)
                when (!(e is OperationCanceledException))
                {
                    _logger.LogError(1, e, "Error while reporting examined board count.");
                }
            }
        }
    }
}