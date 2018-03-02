using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sudokumb
{
    public static class SwitchingGameBoardQueueExtensions
    {
        public static IServiceCollection AddSwitchingGameBoardQueue(
            this IServiceCollection services, bool runHostedServices)
        {
            services.AddSingleton<SwitchingGameBoardQueue>();
            services.AddSingleton<IGameBoardQueue>(
                provider => provider.GetService<SwitchingGameBoardQueue>()
            );
            if (runHostedServices)
            {
                services.AddSingleton<IHostedService>(
                    provider => provider.GetService<SwitchingGameBoardQueue>()
                );
            }
            services.AddSingleton<InMemoryGameBoardStackImpl>();
            services.AddSingleton<PubsubGameBoardQueueImpl>();
            return services;
        }
    }

    public class SwitchingGameBoardQueue : IGameBoardQueue, IHostedService
    {
        private readonly InMemoryGameBoardStackImpl _gameBoardStack;
        private readonly PubsubGameBoardQueueImpl _gameBoardQueue;
        private readonly IDumb _idumb;

        public SwitchingGameBoardQueue(
            Solver solver,
            InMemoryGameBoardStackImpl gameBoardStack,
            PubsubGameBoardQueueImpl gameBoardQueue,
            IDumb idumb)
        {
            this._gameBoardStack = gameBoardStack;
            this._gameBoardQueue = gameBoardQueue;
            this._idumb = idumb;
            solver.Queue = this;
        }

        public async Task<bool> Publish(string solveRequestId,
            IEnumerable<GameBoard> gameBoards,
            CancellationToken cancellationToken)
        {
            if (await _idumb.IsDumbAsync())
            {
                return await _gameBoardQueue.Publish(solveRequestId, gameBoards,
                    cancellationToken);
            }
            else
            {
                return await _gameBoardStack.Publish(solveRequestId, gameBoards,
                    cancellationToken);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken) =>
            _gameBoardQueue.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) =>
            _gameBoardQueue.StopAsync(cancellationToken);
    }
}
