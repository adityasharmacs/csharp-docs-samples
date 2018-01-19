using System;
using System.Collections.Generic;

namespace Sudokumb
{
    public class Solver
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Type in a sudoku board:");
            var board = GameBoard.ParseHandInput(Console.OpenStandardInput());            
            Console.WriteLine("Solving");
            Console.WriteLine(board.ToPrettyString());
            
            var solution = Solve(board);
            if (null == solution)
            {
                Console.WriteLine("No solution found.");
            }
            else
            {
                Console.WriteLine("Solved!\n{0}", solution.ToPrettyString());
            }
        }

        public static GameBoard Solve(GameBoard startingBoard)
        {
            var moves = new Stack<GameBoard>();
            moves.Push(startingBoard);
            while (moves.Count > 0)
            {
                GameBoard board = moves.Pop();
                if (!board.HasEmptyCell())
                {
                    return board;
                }
                foreach (var move in board.FillNextEmptyCell())
                {
                    moves.Push(move);
                }
            }
            return null;
        }
    }
}
