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
        private readonly SolveStateStore _solveStateStore;

        public InMemoryGameBoardStackImpl(Solver solver,
            SolveStateStore solveStateStore)
        {
            _solver = solver;
            _solveStateStore = solveStateStore;
        }

        public async Task<bool> Publish(string solveRequestId,
            IEnumerable<GameBoard> gameBoards,
            CancellationToken cancellationToken)
        {
            Stack<GameBoard> stack = new Stack<GameBoard>(gameBoards);
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _solveStateStore.IncreaseExaminedBoardCount(solveRequestId, 1);
                GameBoard board = stack.Pop();
                IEnumerable<GameBoard> nextMoves;
                if (_solver.ExamineGameBoard(board, out nextMoves))
                {
                    await _solveStateStore.SetAsync(solveRequestId, board,
                        cancellationToken);
                    return true;
                }
                foreach (GameBoard gameBoard in nextMoves)
                {
                    stack.Push(gameBoard);
                }
            }
            return false;
        }
    }

    public class InMemoryGameBoardStack : InMemoryGameBoardStackImpl, IGameBoardQueue
    {
        public InMemoryGameBoardStack(Solver solver,
            SolveStateStore solveStateStore)
            : base(solver, solveStateStore)
        {
        }
    }
}