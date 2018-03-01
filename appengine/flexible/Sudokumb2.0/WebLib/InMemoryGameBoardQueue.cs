using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sudokumb
{
    class InMemoryGameBoardQueue : IGameBoardQueue
    {
        public Func<GameBoardMessage, CancellationToken,
            Task<bool>> GameBoardMessageHandler { get; set; }

        public async Task Publish(string solveRequestId,
            IEnumerable<GameBoard> gameBoards,
            CancellationToken cancellationToken)
        {
            GameBoardMessage message = new GameBoardMessage()
            {
                SolveRequestId = solveRequestId
            };
            foreach (GameBoard gameBoard in gameBoards)
            {
                message.Board = gameBoard;
                var solved = await GameBoardMessageHandler.Invoke(message,
                    cancellationToken);
                if (solved || cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}