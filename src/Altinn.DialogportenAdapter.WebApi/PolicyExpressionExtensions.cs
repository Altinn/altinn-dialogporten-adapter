using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Wolverine.ErrorHandling;

namespace Altinn.DialogportenAdapter.WebApi;

public static class PolicyExpressionExtensions
{
    extension(PolicyExpression expression)
    {
        /// <summary>
        /// Matches if:
        /// - The InnerException is a TException, or
        /// - The exception is AggregateException and
        /// - The AggregateException contains any exception of type TException
        ///
        /// Can be used with: <see cref="TaskExtentions.WithAggregatedExceptions"/>
        /// </summary>
        /// <param name="description"></param>
        /// <typeparam name="TException"></typeparam>
        /// <returns></returns>
        public PolicyExpression OrAnyInner<TException>(string description = "Any Inner") where TException : Exception
        {
            return expression
                .OrInner<TException>()
                .Or<AggregateException>(
                    ex => ex.InnerExceptions.Any(x => x is TException),
                    description
                );
        }

        /// <summary>
        /// Matches if:
        /// - The InnerException is a TException or
        /// - The exception is AggregateException and
        /// - The AggregateException contains any exception of type TException and
        /// - The exceptionPredicate returns true for the matcher that matched the exception
        ///
        /// Use this to match exceptions if using parallel tasks: <see cref="TaskExtentions.WithAggregatedExceptions"/>
        /// </summary>
        /// <param name="exceptionPredicate"></param>
        /// <param name="description"></param>
        /// <typeparam name="TException"></typeparam>
        /// <returns></returns>
        public PolicyExpression OrAnyInner<TException>(
            Func<TException, bool> exceptionPredicate,
            string description = "Any Inner") where TException : Exception
        {
            return expression
                .OrInner(exceptionPredicate, description)
                .Or<AggregateException>(
                    ex => ex.InnerExceptions.OfType<TException>().Any(exceptionPredicate),
                    description
                );
        }
    }
}
