using System;

namespace Sudokumb
{
    class Solve
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Type in a sudoku board:");
            var board = GameBoard.ParseHandInput(Console.OpenStandardInput());
            Console.WriteLine(board.ToPrettyString());
        }
    }
}
