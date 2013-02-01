using System.Threading.Tasks;

namespace AsyncEventAggregator
{
    public static class TaskExtensions
    {
        public static Task<T> AsTask<T>(this T value)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();
            taskCompletionSource.SetResult(value);
            return taskCompletionSource.Task;
        }
    }
}