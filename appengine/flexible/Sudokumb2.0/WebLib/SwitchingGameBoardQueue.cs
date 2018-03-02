using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sudokumb
{
    public class SwitchingGameBoardQueue : IGameBoardQueue
    {
        public SwitchingGameBoardQueue(
            InMemoryGameBoardStack gameBoardStack,
            PubsubGameBoardQueue gameBoardQueue,
            IDumb idumb)
        {

        }

        public Task<bool> Publish(string solveRequestId, IEnumerable<GameBoard> gameBoards, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}
