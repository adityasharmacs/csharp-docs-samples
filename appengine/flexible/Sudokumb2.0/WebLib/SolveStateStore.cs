
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Datastore.V1;
using Microsoft.Extensions.Hosting;
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
        public int BoardsExaminedCount { get; set; }
    }

    public class SolveStateStore : IHostedService
    {
        const string SOLUTION_KIND = "Solution";
        readonly DatastoreDb _datastore;
        KeyFactory _solutionKeyFactory;
        readonly DatastoreCounter _datastoreCounter;
        CancellationTokenSource _cancelHostedService;
        Task _hostedService;

        ConcurrentDictionary<string, ICounter> _examinedBoardCounts
             = new ConcurrentDictionary<string, ICounter>();

        public SolveStateStore(DatastoreDb datastore,
            DatastoreCounter datastoreCounter)
        {
            _datastore = datastore;
            _datastoreCounter = datastoreCounter;
            _solutionKeyFactory = new KeyFactory(datastore.ProjectId,
                datastore.NamespaceId, SOLUTION_KIND);
        }

        public async Task<SolveState> GetAsync(string solveRequestId)
        {
            Entity entity = await _datastore.LookupAsync(
                _solutionKeyFactory.CreateKey(solveRequestId));
            var solveState = new SolveState()
            {
                BoardsExaminedCount = 7
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

        public void IncreaseExaminedBoardCount(string solveRequestId,
            long amount)
        {
            ICounter counter = _examinedBoardCounts.GetOrAdd(solveRequestId,
                (key) => (ICounter) new InterlockedCounter());
            counter.Increase(amount);
        }

        public async Task ReportExaminedBoardCountsAsync(CancellationToken
            cancellationToken)
        {
            List<Task> tasks = new List<Task>();
            foreach (var keyValue in _examinedBoardCounts)
            {
                long count = keyValue.Value.Reset();
                if (count > 0)
                {
                    tasks.Add(_datastoreCounter.IncreaseAsync(keyValue.Key,
                        count, cancellationToken));
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
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                await Task.Delay(1000, cancellationToken);
                await ReportExaminedBoardCountsAsync(cancellationToken);
            }
        }

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
    }
}