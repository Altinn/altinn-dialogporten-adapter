using System.Runtime.CompilerServices;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class TaskExtentions
{

    /// <summary>
    /// Extension method on await that enable await on tuples.
    /// If one task throws an Exception, this method simply throws that exception
    /// If multiple tasks throws different exceptions, this method throws an AggregateException where the:
    /// - InnerException will be whichever occurred first in time
    /// - InnerExceptions will contain all exceptions in the order of occurrence in time.
    /// Note:
    /// This is unlike if you use WhenAll directly, where the arguments order determines the order of the exceptions.
    /// </summary>
    /// <returns>A tuple of all tasks in the same order as passed to this function</returns>
    public static TaskAwaiter<(T1, T2)> GetAwaiter<T1, T2>(
        this (Task<T1>, Task<T2>) taskTuple)
    {
        return CombineTasks().GetAwaiter();
        async Task<(T1, T2)> CombineTasks()
        {
            var (task1, task2) = taskTuple;
            await Task.WhenAll(task1, task2).WithAggregatedExceptions();
            return (task1.Result, task2.Result);
        }
    }

    /// <inheritdoc cref="GetAwaiter{T1,T2}" />
    public static TaskAwaiter<(T1, T2, T3)> GetAwaiter<T1, T2, T3>(
        this (Task<T1>, Task<T2>, Task<T3>) taskTuple)
    {
        return CombineTasks().GetAwaiter();
        async Task<(T1, T2, T3)> CombineTasks()
        {
            var (task1, task2, task3) = taskTuple;
            await Task.WhenAll(task1, task2, task3).WithAggregatedExceptions();
            return (task1.Result, task2.Result, task3.Result);
        }
    }

    /// <inheritdoc cref="GetAwaiter{T1,T2}" />
    public static TaskAwaiter<(T1, T2, T3, T4)> GetAwaiter<T1, T2, T3, T4>(
        this (Task<T1>, Task<T2>, Task<T3>, Task<T4>) taskTuple)
    {
        return CombineTasks().GetAwaiter();
        async Task<(T1, T2, T3, T4)> CombineTasks()
        {
            var (task1, task2, task3, task4) = taskTuple;
            await Task.WhenAll(task1, task2, task3, task4).WithAggregatedExceptions();
            return (task1.Result, task2.Result, task3.Result, task4.Result);
        }
    }

    /// <inheritdoc cref="GetAwaiter{T1,T2}" />
    public static TaskAwaiter<(T1, T2, T3, T4, T5)> GetAwaiter<T1, T2, T3, T4, T5>(
        this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>) taskTuple)
    {
        return CombineTasks().GetAwaiter();
        async Task<(T1, T2, T3, T4, T5)> CombineTasks()
        {
            var (task1, task2, task3, task4, task5) = taskTuple;
            await Task.WhenAll(task1, task2, task3, task4, task5).WithAggregatedExceptions();
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result);
        }
    }

    /// <inheritdoc cref="GetAwaiter{T1,T2}" />
    public static TaskAwaiter<(T1, T2, T3, T4, T5, T6)> GetAwaiter<T1, T2, T3, T4, T5, T6>(
        this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>, Task<T6>) taskTuple)
    {
        return CombineTasks().GetAwaiter();
        async Task<(T1, T2, T3, T4, T5, T6)> CombineTasks()
        {
            var (task1, task2, task3, task4, task5, task6) = taskTuple;
            await Task.WhenAll(task1, task2, task3, task4, task5, task6).WithAggregatedExceptions();
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result);
        }
    }

    /// <inheritdoc cref="GetAwaiter{T1,T2}" />
    public static TaskAwaiter<(T1, T2, T3, T4, T5, T6, T7)> GetAwaiter<T1, T2, T3, T4, T5, T6, T7>(
        this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>, Task<T6>, Task<T7>) taskTuple)
    {
        return CombineTasks().GetAwaiter();
        async Task<(T1, T2, T3, T4, T5, T6, T7)> CombineTasks()
        {
            var (task1, task2, task3, task4, task5, task6, task7) = taskTuple;
            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7).WithAggregatedExceptions();
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result);
        }
    }

    /// <inheritdoc cref="GetAwaiter{T1,T2}" />
    public static TaskAwaiter<(T1, T2, T3, T4, T5, T6, T7, T8)> GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8>(
        this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>, Task<T6>, Task<T7>, Task<T8>) taskTuple)
    {
        return CombineTasks().GetAwaiter();
        async Task<(T1, T2, T3, T4, T5, T6, T7, T8)> CombineTasks()
        {
            var (task1, task2, task3, task4, task5, task6, task7, task8) = taskTuple;
            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7, task8).WithAggregatedExceptions();
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result,
                task8.Result);
        }
    }

    /// <inheritdoc cref="GetAwaiter{T1,T2}" />
    public static TaskAwaiter<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> GetAwaiter<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
        this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>, Task<T6>, Task<T7>, Task<T8>, Task<T9>) taskTuple)
    {
        return CombineTasks().GetAwaiter();
        async Task<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> CombineTasks()
        {
            var (task1, task2, task3, task4, task5, task6, task7, task8, task9) = taskTuple;
            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7, task8, task9).WithAggregatedExceptions();
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result,
                task8.Result, task9.Result);
        }
    }

    /// <summary>
    /// Exposes the internal AggregateException if there is more than one exception from running parallell tasks.
    /// Usage: Task.WhenAll(...).WithAggregatedExceptions();
    ///
    /// If one task throws an Exception, the Task will simply throw that exception
    /// If multiple tasks throws different exceptions, the Task will throw an AggregateException where the:
    /// - InnerException will be the leftmost errored task in the arguments supplied to WhenAll
    /// - InnerExceptions will contain all exceptions in the same order as the arguments to WhenAll
    ///
    /// Advantages:
    /// - All exceptions can be logged as part of the AggregateException
    /// - Enable more specific retry-policies
    ///
    /// </summary>
    /// <param name="this"></param>
    /// <returns></returns>
    public static Task WithAggregatedExceptions(this Task @this)
    {
        return @this
            .ContinueWith(
                continuationFunction: anteTask => anteTask is { IsFaulted: true, Exception: not null } &&
                                                  (anteTask.Exception.InnerExceptions.Count > 1
                                                   || anteTask.Exception.InnerException is AggregateException)
                    ? Task.FromException(anteTask.Exception.Flatten())
                    : anteTask,
                cancellationToken: CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default)
            .Unwrap();
    }
}
