using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Sudokumb
{
    public interface IGameBoardQueue
    {
        // Returns true if the puzzle was solved immediately.
        Task<bool> Publish(string solveRequestId,
            IEnumerable<GameBoard> gameBoards,
            CancellationToken cancellationToken);
    }

    public static class IGameBoardQueueExtensions
    {
        public static async Task<string> StartSolving(
            this IGameBoardQueue queue, GameBoard gameBoard,
            CancellationToken cancellationToken)
        {
            // Create a new request and publish it to pubsub.
            string solveRequestId = Guid.NewGuid().ToString();
            await queue.Publish(solveRequestId, new [] { gameBoard },
                cancellationToken);
            return solveRequestId;
        }
    }
}