using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace III.Core
{
    /// <summary>
    /// Simple helper class for dealing with various validation concerns.
    /// </summary>
    [ExcludeFromCodeCoverage]
    #pragma warning disable IDE0019 // Use pattern matching
    public static class Validation
    {
        #region Public Methods
        public static void Assert(bool aCondition, String aFormat, params object[] aArgs)
        {
            if (!aCondition)
            {
                throw new InvalidOperationException(String.Format(aFormat, aArgs));
            }
        }

        public static void AssertNotNull(object aInput, string aParameter)
        {
            AssertArgument(aInput != null, "The '{0}' parameter cannot be null!", aParameter);
        }

        public static void AssertNotEmpty(string aInput, string aParameter)
        {
            AssertArgument(!String.IsNullOrWhiteSpace(aInput), "The '{0}' parameter cannot be null or empty!", aParameter);
        }

        public static void AssertNull(object aInput, string aParameter)
        {
            AssertArgument(aInput == null, "The '{0}' parameter cannot be null!", aParameter);
        }

        public static void AssertEmpty(string aInput, string aParameter)
        {
            AssertArgument(String.IsNullOrWhiteSpace(aInput), "The '{0}' parameter cannot be null or empty!", aParameter);
        }

        public static void AssertNotZero(int aInput, string aParameter)
        {
            AssertArgument(aInput != 0, "The '{0}' parameter must not be zero!", aParameter);
        }

        public static void AssertZero(int aInput, string aParameter)
        {
            AssertArgument(aInput == 0, "The '{0}' parameter must be zero!", aParameter);
        }

        public static void AssertArgument(bool aCondition, String aFormat, params object[] aArgs)
        {
            if (!aCondition)
            {
                throw new ArgumentException(String.Format(aFormat, aArgs));
            }
        }

        public static void AssertExists(string aPath)
        {
            if (!File.Exists(aPath))
                throw new FileNotFoundException(String.Format("The requested file [{0}] was not found!", aPath));
        }

        /// <summary>
        /// Created by Stephen Toub, this helper wraps access to the Task 
        /// Result property to remove the first level of AggregateException.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="task"></param>
        /// <returns></returns>
        public static TResult GetResult<TResult>(this Task<TResult> task)
        {
            try
            {
                return task.Result;
            }
            catch (AggregateException ae)
            {
                throw ae.Flatten().InnerException;
            }
        }
        #endregion

        #region Miscellaneous Exception Extensions
        public static IEnumerable<Exception> GetInnerEnumerable(this Exception aEx)
        {
            if (aEx == null)
                yield break;

            var AllExceptions = new Queue<Exception>();
            AllExceptions.Enqueue(aEx);

            while (AllExceptions.Count > 0)
            {
                var CurrExc = AllExceptions.Dequeue();

                yield return CurrExc;

                var AggrExc = CurrExc as AggregateException;
                if (AggrExc != null)
                {
                    foreach (var inner in AggrExc.Flatten().InnerExceptions)
                    {
                        AllExceptions.Enqueue(inner);
                    }
                }
                else
                {
                    if (CurrExc.InnerException != null)
                    {
                        AllExceptions.Enqueue(CurrExc.InnerException);
                    }
                }
            }
        }

        public static String GetSimpleAggregateMessages(this Exception aEx, bool aIncludeStackTrace = true)
        {
            StringBuilder sb = new StringBuilder();

            if (aEx != null)
            {
                foreach (var InnerEx in aEx.GetInnerEnumerable())
                {
                    if (InnerEx is AggregateException)
                    {
                        // Just go on to the next one!
                        continue;
                    }
                    else if (InnerEx is WebException Inner)
                    {
                        var Resp = Inner.Response as HttpWebResponse;

                        if (Resp != null)
                        {
                            sb.AppendLine(String.Format("\t{0} {1}", Resp.StatusCode, Resp.StatusDescription));
                        }
                        else
                        {
                            sb.AppendLine(String.Format("{0}\t{1}", Inner.Status, InnerEx.Message));
                        }
                    }
                    else
                    {
                        sb.AppendLine(InnerEx.Message);
                        if (InnerEx.InnerException == null && aIncludeStackTrace)
                            sb.AppendLine(InnerEx.StackTrace);
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Simple helper method that permits accessing an exception message even when there is not one.
        /// </summary>
        /// <param name="aEx"></param>
        /// <returns></returns>
        public static string ExtractMessage(this Exception aEx)
        {
            string result = null;

            if (aEx != null)
            {
                result = aEx.Message;
            }

            return result;
        }
        #endregion

        #region Private Methods
        private static String ExtractErrorMessage(Exception aError, String aFormat = "", params object[] aArgs)
        {
            String message = String.Format(aFormat ?? "", aArgs ?? new object[0]);
            try
            {
                return String.Format("{0}\n{1}\n{2}", message, aError.GetSimpleAggregateMessages(),
                    (aError != null) ? aError.ToString() : "");
            }
            catch
            {
                return String.Format("{0}\n{1}", message, (aError != null) ? aError.ToString() : "");
            }
        }
        #endregion
    }
    #pragma warning restore IDE0019 // Use pattern matching
}