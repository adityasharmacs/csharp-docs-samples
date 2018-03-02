using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Datastore.V1;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sudokumb;
using Newtonsoft.Json;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;

namespace Sudokumb
{
    /// <summary>
    /// Reads sudoku puzzles from Pub/Sub and solves them.
    /// </summary>
    public class Solver
    {
        readonly SolveStateStore _solveStateStore;

        public Solver(SolveStateStore solveStateStore)
        {
            _solveStateStore = solveStateStore;
        }

        public IGameBoardQueue Queue { get; set; }

        public async Task<bool> ExamineGameBoard(GameBoardMessage message,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            _solveStateStore.IncreaseExaminedBoardCount(
                message.SolveRequestId, 1);
            if (!message.Board.HasEmptyCell())
            {
                // Solved!
                await _solveStateStore.SetAsync(message.SolveRequestId,
                    message.Board);
                return true;
            }
            var nextMoves = message.Board.FillNextEmptyCell();
            if (nextMoves.Count() == 0)
            {
                return false;
            }
            // Enumerate the next possible board states.
            return await Queue.Publish(message.SolveRequestId,
                nextMoves, cancellationToken);
        }
    }
}