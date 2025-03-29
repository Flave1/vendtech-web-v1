using System;
using System.Net.Http;
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
            _action = action ?? throw new ArgumentNullException(nameof(action));
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
        private readonly Task _task;

        public RetryAwaiter(Func<Task> action, int retries, int delay)
        {
            _action = action;
            _retries = retries;
            _delay = delay;
            _task = ExecuteWithRetry();
        }

        private async Task ExecuteWithRetry()
        {
            int attempt = 0;
            while (attempt < _retries)
            {
                try
                {
                    attempt++;
                    await _action();
                    return;
                }
                catch (Exception ex) when (attempt < _retries && IsTransient(ex))
                {
                    string msg = $"Attempt {attempt} failed due to {ex.GetType().Name}. Retrying in {_delay}ms...";
                    Console.WriteLine(msg);
                    Utilities.LogExceptionToDatabase(new Exception(msg), $"retried_exception: {ex.Message}");

                    await Task.Delay(_delay);
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Final failure after {attempt} attempts: {ex.Message}";
                    Console.WriteLine(errorMsg);
                    Utilities.LogExceptionToDatabase(ex, "final_retry_failure");

                    throw new Exception($"Final failure in RetryAwaitable after {attempt} attempts: {ex.Message}", ex);
                }
            }

            throw new Exception("Max retry attempts reached.");
        }

        private static bool IsTransient(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TimeoutException ||
                   ex is TaskCanceledException ||
                   ex is OperationCanceledException;
        }

        public bool IsCompleted => _task.IsCompleted;

        public void OnCompleted(Action continuation) => _task.ContinueWith(_ => continuation());

        public void GetResult()
        {
            try
            {
                _task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex, "retry_final_get_result");
                throw;
            }
        }
    }
}
