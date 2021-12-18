using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VideoCompresser
{
    public class CommandObserver : KeyedCollection<ConsoleKey, ConsoleCommand>
    {
        private readonly CancellationTokenSource _cts = new();
        private CancellationTokenSource? _combinedTokenSource;

        protected override ConsoleKey GetKeyForItem(ConsoleCommand item) => item.ActivatorKey;

        public Task StartObserving() => Task.Run(ReadAndProcessInput, _cts.Token);
        public Task StartObserving(CancellationToken token)
        {
            _combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);
            return Task.Run(ReadAndProcessInput, _combinedTokenSource.Token);
        }

        private void ReadAndProcessInput()
        {
            while (true)
            {
                ConsoleKeyInfo readKey = Console.ReadKey(true);
                if (!Dictionary.TryGetValue(readKey.Key, out ConsoleCommand command))
                    continue;
                command.Execute();
            }
        }

        public void StopObserving()
        {
            _cts.Cancel();
            _combinedTokenSource?.Cancel();
        }
    }

    public record ConsoleCommand(ConsoleKey ActivatorKey, Action Execute);
}
