using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


namespace VendTech.BLL.Common
{
    public class RetryAwaitable
    {
        private readonly Func<Task> _action;
        private readonly int _retries;
        private readonly int _delay;

        public RetryAwaitable(Func<Task> action, int retries = 5, int delay = 1000)
        {
            _action = action;
            _retries = retries;
            _delay = delay;
        }

        public RetryAwaiter GetAwaiter() => new RetryAwaiter(_action, _retries, _delay);
    }

    public class RetryAwaiter : INotifyCompletion
    {
        private readonly Func<Task> _action;
        private readonly int _retries;
        private readonly int _delay;
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public RetryAwaiter(Func<Task> action, int retries, int delay)
        {
            _action = action;
            _retries = retries;
            _delay = delay;
            ExecuteWithRetry();
        }

        private async void ExecuteWithRetry()
        {
            int attempt = 0;
            while (attempt < _retries)
            {
                try
                {
                    attempt++;
                    await _action();
                    _tcs.SetResult(true);
                    return;
                }
                catch (Exception) when (attempt < _retries)
                {
                    Console.WriteLine($"Attempt {attempt} failed. Retrying in {_delay}ms...");
                    await Task.Delay(_delay);
                }
            }
            _tcs.SetException(new Exception("Max retry attempts reached."));
        }

        public bool IsCompleted => _tcs.Task.IsCompleted;

        public void OnCompleted(Action continuation) => _tcs.Task.ContinueWith(_ => continuation());

        public void GetResult() => _tcs.Task.GetAwaiter().GetResult();
    }

}
