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
        public bool ExamineGameBoard(GameBoard board,
            out IEnumerable<GameBoard> nextMoves)
        {
            if (!board.HasEmptyCell())
            {
                nextMoves = null;
                return true;
            }
            nextMoves = board.FillNextEmptyCell();
            return false;
        }
    }
}