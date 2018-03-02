using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Sudokumb
{
    public class InMemoryGameBoardStackImpl
    {
        readonly Solver _solver;
        public InMemoryGameBoardStackImpl(Solver solver)
        {
            _solver = solver;
        }

        public async Task<bool> Publish(string solveRequestId,
            IEnumerable<GameBoard> gameBoards,
            CancellationToken cancellationToken)
        {
            foreach (GameBoard board in gameBoards)
            {
                bool solved = await _solver.ExamineGameBoard(solveRequestId,
                    board, cancellationToken);
                if (solved)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class InMemoryGameBoardStack : InMemoryGameBoardStackImpl, IGameBoardQueue
    {
        public InMemoryGameBoardStack(Solver solver) : base(solver)
        {
             solver.Queue = this;
       }
    }
}