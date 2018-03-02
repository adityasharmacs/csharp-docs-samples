using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Sudokumb
{
    public class InMemoryGameBoardStack : IGameBoardQueue
    {
        readonly Solver _solver;
        public InMemoryGameBoardStack(Solver solver)
        {
            _solver = solver;
        }

        public async Task<bool> Publish(string solveRequestId,
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
                bool solved = await _solver.ExamineGameBoard(message, this,
                    cancellationToken);
                if (solved)
                {
                    return true;
                }
            }
            return false;
        }
    }
}