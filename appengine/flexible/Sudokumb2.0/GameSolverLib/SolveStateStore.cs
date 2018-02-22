
using System.Threading.Tasks;
using Google.Cloud.Datastore.V1;
using Sudokumb;

namespace Sudokumb
{
    // Represents the status of one puzzle as its being solved.
    public class SolveState
    {
        /// <summary>
        ///  Null means the puzzle hasn't been completely solved.
        /// </summary>
        public GameBoard Solution { get; set; }
        /// <summary>
        /// How many game boards have been examined while searching for the
        /// solution?
        /// </summary>
        public int BoardsExaminedCount { get; set; }
    }

    public class SolveStateStore
    {
        const string TYPE = "SolveState", SOLUTION = "Solution";
        readonly DatastoreDb datastore_;
        KeyFactory keyFactory_;

        public SolveStateStore(DatastoreDb datastore)
        {
            datastore_ = datastore;
            keyFactory_ = new KeyFactory(datastore.ProjectId,
                datastore.NamespaceId, SOLUTION);
        }

        public async Task<SolveState> GetAsync(string solveRequestId)
        {
            Entity entity = await datastore_.LookupAsync(
                keyFactory_.CreateKey(solveRequestId));
            var solveState = new SolveState()
            {
                BoardsExaminedCount = 7
            };
            if (null != entity && entity.Properties.ContainsKey(SOLUTION))
            {
                solveState.Solution = GameBoard.Create(
                    (string)entity[SOLUTION]);
            }
            return solveState;
        }

        public Task SetAsync(string solveRequestId, GameBoard gameBoard)
        {
            Entity entity = new Entity()
            {
                Key = keyFactory_.CreateKey(solveRequestId),
                [SOLUTION] = gameBoard.Board
            };
            return datastore_.UpsertAsync(entity);
        }
    }
}