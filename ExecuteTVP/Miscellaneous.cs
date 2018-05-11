using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace III.Core
{

    /// <summary>
    /// Useful and/or frequently used constants.
    /// </summary>
    public static class Constants
    {
        #region Constants
        public const int C_OneDayInMinutes = 60 * 24;
        public const int C_OneHourInMilliseconds = 60 * 60 * 1000;
        public static readonly Char[] C_DefaultDelimiters = new Char[] { ',', ' ', ';' };
        public static readonly Char[] C_DefaultDelimitersNoSpace = new Char[] { ',', ';' };

        public static readonly DateTime C_DefaultDate = new DateTime(1980, 1, 1);
        public static readonly DateTime C_DefaultStartDate = C_DefaultDate;
        public static readonly DateTime C_DefaultEndDate = new DateTime(2029, 12, 31);
        public static readonly DateTime C_DefaultSafeMaxDate = new DateTime(8888, 12, 31);

        internal const int C_DefaultRandomLength = 30;

        internal const int C_DefaultRangeFrom = 33;
        internal const int C_DefaultRangeThru = 126;
        internal const int C_DefaultIntRangeFrom = 1500000001;
        internal const int C_DefaultIntRangeThru = 2100000999;
        internal const long C_DefaultLongRangeFrom = 5000000000000000001;
        internal const long C_DefaultLongRangeThru = 9000000000000000999;
        private const int DefaultMaxRandomIterations = 100000;

        private static readonly HashSet<Byte> Exclusions = new HashSet<Byte>(new Byte[] { 35, 47, 60, 61, 62, 63, 92 });
        #endregion

        #region Variables
        private static ASCIIEncoding _GlobalEncoding = new System.Text.ASCIIEncoding();

        private static readonly Random _GlobalRandom = new Random();

        [ThreadStatic]
        private static Random _Random;
        #endregion

        #region Properties
        /// <summary>
        /// From Stephen Toub ... http://blogs.msdn.com/b/pfxteam/archive/2009/02/19/9434171.aspx
        /// </summary>
        public static Random Randomizer
        {
            get
            {
                Random InstRnd = _Random;

                if (InstRnd == null)
                {
                    int seed;
                    lock (_GlobalRandom) seed = _GlobalRandom.Next();
                    _Random = InstRnd = new Random(seed);
                }

                return InstRnd;
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// This implementation only supports numbers and characters, and has certain exclusion ranges.
        /// And per Jon Skeet, using a global Encoding object is fine.
        /// </summary>
        /// <param name="aLength"></param>
        /// <param name="aPrefix"></param>
        /// <param name="aRangeFrom"></param>
        /// <param name="aRangeThru"></param>
        /// <param name="aRnd"></param>
        /// <returns></returns>
        public static string GetRandomString(int aLength = C_DefaultRandomLength, string aPrefix = null, int aRangeFrom = C_DefaultRangeFrom, int aRangeThru = C_DefaultRangeThru, Random aRnd = null)
        {
            if (aRnd == null)
            {
                aRnd = Randomizer;
            }

            int prefixLen = (aPrefix == null) ? 0 : aPrefix.Length;

            if (aLength <= prefixLen)
                throw new InvalidOperationException(String.Format("The 'aLength' paramter value [{0}] must be greater than the length of 'aPrefix' [{1}].", aLength, aPrefix.Length));

            Byte[] rgBytes = new Byte[aLength - prefixLen];
            Byte b;

            for (int i = 0; i < aLength - prefixLen; i++)
            {
                b = (byte)aRnd.Next(aRangeFrom, aRangeThru);
                while (Exclusions.Contains(b))
                {
                    b = (byte)aRnd.Next(aRangeFrom, aRangeThru);
                }
                rgBytes[i] = b;
            }

            return aPrefix + _GlobalEncoding.GetString(rgBytes);
        }

        public static int GetRandomInt(int aRangeFrom = C_DefaultIntRangeFrom, int aRangThru = C_DefaultIntRangeThru, Random aRnd = null)
        {
            if (aRnd == null)
            {
                aRnd = Randomizer;
            }

            int result = aRnd.Next(aRangeFrom, aRangThru);

            return result;
        }

        public static long GetRandomLong(long aRangeFrom = C_DefaultLongRangeFrom, long aRangThru = C_DefaultLongRangeThru, int aMaxIterations = DefaultMaxRandomIterations, Random aRnd = null)
        {
            if (aRnd == null)
            {
                aRnd = Randomizer;
            }

            // Set a default that can be tested to determine MaxIterations was hit.
            long result = -1;

            for (int i = 0; i < aMaxIterations; i++)
            {
                var tempRes = aRnd.NextInt64();
                if ((aRangeFrom <= tempRes) && (tempRes < aRangThru))
                {
                    result = tempRes;
                    break;
                }
            }

            if (result == -1)
                throw new InvalidOperationException(String.Format("A random Int64 value between the range of [{0}] and [{1}] could not be generated within [{2}] iterations. Consider increasing the 'aMaxIterations' parameter value or extending the valid range.", aRangeFrom, aRangThru, aMaxIterations));

            return result;
        }

        /// <summary>
        /// Shuffle extension method, included here to benefit from thread-safe random.
        /// http://stackoverflow.com/a/1262619/2908362
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Randomizer.Next(n + 1);

                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// This extension creates a random Int64 based on a random set of bytes.
        /// </summary>
        /// <param name="rnd"></param>
        /// <returns></returns>
        public static Int64 NextInt64(this Random rnd)
        {
            var buffer = new byte[sizeof(Int64)];
            rnd.NextBytes(buffer);
            return BitConverter.ToInt64(buffer, 0);
        }

        public static CoinToss TossCoin()
        {
            return (GetRandomInt(1, 100000) % 2 == 0) ? CoinToss.Heads : CoinToss.Tails;
        }

        public static bool IsEven(this int number)
        {
            return (number % 2 == 0);
        }

        public static bool IsOdd(this int number)
        {
            return !number.IsEven();
        }

        /// <summary>
        /// True as a string can have many representations:
        /// 
        ///     "True" (case-insensitive)
        ///     "Yes" (case-insensitive)
        ///     Any non-zero number
        ///     
        /// This is the order used in the following evaluation.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>boolean</returns>
        public static bool IsTrue(this string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            // Start with the direct evaluation.
            if (Boolean.TryParse(value, out bool result))
            {
                // If TryParse() succeeded, it's either true or false, so bail.
                return result;
            }

            // Clean up the input.
            value = value.Trim();

            // Test for 'Yes'.
            if (value.Equals("Yes", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            // See if we've got a number, and then do the built-in conversion.
            if (value.IsNumeric())
            {
                result = Convert.ToBoolean(Double.Parse(value));
            }

            return result;
        }

        public static bool IsFalse(this string value)
        {
            return !value.IsTrue();
        }

        /// <summary>
        /// The following will detect a slew of different formats.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>boolean</returns>
        public static bool IsNumeric(this string value)
        {
            bool result = false;

            if (!String.IsNullOrWhiteSpace(value))
            {
                // Use the CurrentCulture, which comes from the executing thread.
                result = decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal numValue);
            }

            return result;
        }

        public static DateTime StartOfMonth(this DateTime value)
        {
            return new DateTime(value.Year, value.Month, 1);
        }

        public static DateTime EndOfMonth(this DateTime value)
        {
            return value.StartOfMonth().AddMonths(1).AddSeconds(-1);
        }

        /// <summary>
        /// A null-safe helper function to faciliate performing a Trim() operation.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string TrimEx(this string value, params char[] trimChars)
        {
            if (value == null)
                return null;

            return value.Trim(trimChars);
        }

        public static string TrimEndEx(this string value, params char[] trimChars)
        {
            if (value == null)
                return null;

            return value.TrimEnd(trimChars);
        }

        public static string TrimStartEx(this string value, params char[] trimChars)
        {
            if (value == null)
                return null;

            return value.TrimStart(trimChars);
        }
        #endregion
    }

    public enum CoinToss
    {
        Heads = 0,
        Tails = 1
    }

    public static class GeneralExtensions
    {
        /// <summary>
        /// Helper extension to locate methods with a specific signature.
        /// </summary>
        /// <param name="aType"></param>
        /// <param name="aReturnType"></param>
        /// <param name="aParameterTypes"></param>
        /// <returns></returns>
        public static IEnumerable<MethodInfo> GetMethodsBySignature(this Type aType, Type aReturnType, params Type[] aParameterTypes)
        {
            Validation.AssertNotNull(aType, "aType");
            Validation.AssertNotNull(aReturnType, "aReturnType");

            return aType.GetMethods().Where(m =>
            {
            // Verify the return type.
            // Special case the generic return type.
            if ((!aReturnType.IsGenericType) && (m.ReturnType != aReturnType))
                    return false;

                if ((aReturnType.IsGenericType) && (!aReturnType.IsGenericTypeAssignableFrom(m.ReturnType)))
                    return false;

                var Params = m.GetParameters();

            // Verify input parameter count match.
            if ((aParameterTypes == null || aParameterTypes.Length == 0))
                    return (Params.Length == 0);

            // Bail if there is a difference.
            if (Params.Length != aParameterTypes.Length)
                    return false;

            // Iterate the parameters and check for equivalency.
            // Special support for assignable types.
            for (int i = 0; i < aParameterTypes.Length; i++)
                {
                    if (Params[i].ParameterType.Equals(aParameterTypes[i]))
                        continue;

                // Params represents the concrete types, so presume that aParameterTypes
                // are either base class or generic.
                if (aParameterTypes[i].IsAssignableFrom(Params[i].ParameterType))
                        continue;

                    if ((aParameterTypes[i].IsGenericType) && (aParameterTypes[i].IsGenericTypeAssignableFrom(Params[i].ParameterType)))
                        continue;

                // If none of the prior tests complete, this is a no-match.
                return false;
                }

                return true;
            });
        }

        /// <summary>
        /// Helper method to scan CustomAttributes of type to determine if
        /// Attribute T is present.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="aType"></param>
        /// <returns></returns>
        public static bool HasCustomAttribute<T>(this Type aType, bool aInherit = true) where T : Attribute
        {
            bool result = false;

            if (aType != null)
            {
                result = (aType.GetCustomAttribute<T>(aInherit) != null);
            }

            return result;
        }

        /// <summary>
        /// Extension method to execute an Action on each element in an enumerable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="action"></param>
        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items)
            {
                action(item);
            }
        }

        /// <summary>
        /// The following is taken from http://stackoverflow.com/a/1075059.
        /// 
        /// NOTE: The second version just inverts the parameters and calls back into this method. It "fits" better 
        /// the IsAssignableFrom???() standard.
        /// </summary>
        public static bool IsAssignableToGenericType(this Type givenType, Type genericType)
        {
            /*
            if (givenType == null || genericType == null)
            {
                return false;
            }

            return givenType == genericType
              || givenType.MapsToGenericTypeDefinition(genericType)
              || givenType.HasInterfaceThatMapsToGenericTypeDefinition(genericType)
              || givenType.BaseType.IsAssignableToGenericType(genericType);
            */

            if (givenType == null || genericType == null)
            {
                return false;
            }

            var interfaceTypes = givenType.GetInterfaces();

            foreach (var it in interfaceTypes)
            {
                if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
                    return true;
            }

            if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
                return true;

            // Special case generic tasks where the Task Result is equivalent.
            if ((givenType.IsGenericType) && (genericType.IsGenericType) &&
                (givenType.BaseType != null) && (genericType.BaseType != null) &&
                (givenType.BaseType.Equals(typeof(Task))) && (genericType.BaseType.Equals(typeof(Task))))
            {
                // Compare the Type arguments for either direct or generic equivalency.
                if ((givenType.IsAssignableFrom(genericType)) ||
                    (givenType.GenericTypeArguments[0].IsAssignableToGenericType(genericType.GenericTypeArguments[0])))
                    return true;
            }

            Type baseType = givenType.BaseType;
            if (baseType == null) return false;

            return IsAssignableToGenericType(baseType, genericType);
        }

        public static bool IsGenericTypeAssignableFrom(this Type genericType, Type givenType)
        {
            return givenType.IsAssignableToGenericType(genericType);
        }

        public static Type GetEnumerableType(this Type type)
        {
            if (type != null)
            {
                foreach (Type t in type.GetInterfaces())
                {
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        return t.GetGenericArguments()[0];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// A trivial extension that invokes the standard Contains() method
        /// but using the invariant case-insensitive comparer.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool ContainsInvariant(this IEnumerable<string> source, string value)
        {
            return ((source != null) && source.Contains(value, StringComparer.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// This method uses the "*" character as the wildcard, and it can be present 
        /// in either the value to match or an entry in the list to compare.
        /// 
        /// If the wildcard is detected, a StartsWith() comparison is performed to determine
        /// a match.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool ContainsInvariantIgnoreCaseWildcard(this IEnumerable<string> source, string value)
        {
            bool result = false;

            if ((source != null) && (source.Any()) && (!String.IsNullOrWhiteSpace(value)))
            {
                // Do the easy test first.
                if (source.ContainsInvariant(value))
                {
                    return true;
                }

                // Now we have to iterate the source and check for wildcard matching.
                // NOTE: The wildcard can be in either the source or the value!
                var WildcardMatchResult = source.FirstOrDefault(s => ((s != null) && (((s.EndsWith("*")) && (value.StartsWith(s.Substring(0, s.Length - 1), StringComparison.InvariantCultureIgnoreCase))) ||
                                                                      ((value.EndsWith("*")) && (s.StartsWith(value.Substring(0, value.Length - 1), StringComparison.InvariantCultureIgnoreCase))))));
                result = !String.IsNullOrWhiteSpace(WildcardMatchResult);
            }

            return result;
        }

        /// <summary>
        /// Helpful wrapper around List.Remove() call.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static int RemoveRange<T>(this List<T> items, IEnumerable<T> source)
        {
            int result = 0;

            if ((items != null) && (source != null))
            {
                source.ForEach(delegate (T item)
                {
                    if (items.Remove(item))
                    {
                        result++;
                    }
                });
            }

            return result;
        }

        #region Private Helper Methods
        private static bool HasInterfaceThatMapsToGenericTypeDefinition(this Type givenType, Type genericType)
        {
            return givenType
              .GetInterfaces()
              .Where(it => it.IsGenericType)
              .Any(it => it.GetGenericTypeDefinition() == genericType);
        }

        private static bool MapsToGenericTypeDefinition(this Type givenType, Type genericType)
        {
            return genericType.IsGenericTypeDefinition
              && givenType.IsGenericType
              && givenType.GetGenericTypeDefinition() == genericType;
        }
        #endregion
    }
}
