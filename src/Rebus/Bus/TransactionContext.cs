﻿using System;

namespace Rebus.Bus
{
    /// <summary>
    /// Gives access to a thread-bound <see cref="ITransactionContext"/>
    /// </summary>
    public class TransactionContext
    {
        [ThreadStatic] static ITransactionContext current;

        /// <summary>
        /// Gets the <see cref="ITransactionContext"/> associated with the current thread
        /// </summary>
        public static ITransactionContext Current
        {
            get { return current; }
        }

        /// <summary>
        /// Assigns the specified <see cref="ITransactionContext"/> to the current thread
        /// </summary>
        public static void Set(ITransactionContext context)
        {
            if (!context.IsTransactional)
            {
                throw new InvalidOperationException(string.Format(@"Cannot mount {0} as the current ambient Rebus transaction context, but it does not make sense to do so.

It does not make sense because a non-transactional transaction context does not have a life span that should be allowed to function as a context - by definition, a non-transactional context must be a throw-away context whose lifetime is purely transient.", context));
            }
            current = context;
        }

        /// <summary>
        /// Clears any context that might currently be assigned to the current thread
        /// </summary>
        public static void Clear()
        {
            current = null;
        }
    }
}