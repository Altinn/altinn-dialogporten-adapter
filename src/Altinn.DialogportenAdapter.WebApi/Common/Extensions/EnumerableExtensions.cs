namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class EnumerableExtensions
{
    public static IEnumerable<T> DequeueWhile<T>(this Queue<T> queue, Func<T, bool> predicate)
    {
        while (queue.TryPeek(out var next) && predicate(next))
        {
            yield return queue.Dequeue();
        }
    }
}