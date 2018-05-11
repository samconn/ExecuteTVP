using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Inflector.Cultures;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Inflector;

namespace III.Core
{
    /// <summary>
    /// This SqlConnection-extension class implements support for invoking SQL Server Stored Procedures
    /// which have User-Defined Table Types inputs.
    /// 
    /// The simplest usage is to follow a convention-over-configuration model:
    /// 
    ///     1. Schema for both stored procedures and table types is "dbo".
    ///     2. There is a 1-to-1 name match between the POCO and TVP types.
    ///     3. The stored procedure is named "dbo.Save???" where ??? is the pluralized
    ///        POCO type name, e.g., Contact -> Contacts, Company -> Companies.
    ///     4. This only works for single-type execution calls.
    ///        
    /// When these conventions are satisified, the following will just work:
    /// 
    ///     var res = myContext.ExecuteTVPProcedure<Contact>(Contacts);
    ///     var res = myContext.ExecuteTVPProcedure<Company>(CompanyList);
    ///     
    ///     (Presuming User-Defined Table Types named "dbo.Contacts" and "dbo.Company" and 
    ///      the stored procedures namd "dbo.SaveContacts" and "dbo.SaveCompanies" all
    ///      exist in the database.)
    ///      
    /// More complex usage scenarios can be achieved by making a call to the RegisterProcedure() 
    /// method:
    /// 
    ///     1. Defining the stored procedure name:
    /// 
    ///             TVPExtensions.RegisterProcedure<Contact>("dbo.UpdateAllContacts");
    ///             TVPExtensions.RegisterProcedure<Contact, EmployeeContact>("dbo.ProcessEmployeeContacts");
    ///     
    ///     2. Defining the User-Defined Table Type:
    /// 
    ///             TVPExtensions.RegisterProcedure<EmployeeContact>("dbo.SaveEmployeeContacts", "hr.uddtEmployeeContact");
    ///             TVPExtensions.RegisterProcedure<Contact, Event>("dbo.RegisterContactForEvents", null, "evt.uddtExternalEvent");
    /// 
    ///        The set of TypeNames is an ordered list, so pass a null value for any Types where the
    ///        default value is acceptable.
    ///        
    ///        
    /// Once a procedure has been registered, it can be called using the following syntax:
    /// 
    ///     var res = myContext.ExecuteTVPProcedure<Contact>(Contacts);
    ///     var res = myContext.ExecuteTVPProcedure<Contact, EmployeeContact>(Contacts, EmpContacts);    
    /// 
    /// Once a type-set is defined, additional stored procedures can be invoked by specifing the 
    /// stored procedure name to execute:
    /// 
    ///     var res = myContext.ExecuteTVPProcedure<Contact>("dbo.UpdateContacts", Contacts);
    ///     var res = myContext.ExecuteTVPProcedure<Contact>("dbo.ExportContacts", Contacts);
    ///     var res = myContext.ExecuteTVPProcedure<Contact, EmployeeContact>("dbo.MergeEmployeeContacts", Contacts, EmpContacts);
    ///     var res = myContext.ExecuteTVPProcedure<Contact, EmployeeContact>("dbo.RegisterContactsForEmployee", Contacts, EmpContacts);
    ///     
    /// The stored procedure name is optional and if absent, the stored procedure associated with the 
    /// first registration call will be invoked. The following calls are equivalent:
    /// 
    ///     var res = myContext.ExecuteTVPProcedure<Contact>(Contacts);
    ///     var res = myContext.ExecuteTVPProcedure<Contact>("dbo.SaveContacts", Contacts);
    /// 
    /// 
    /// Certain conventions need to be followed:
    /// 
    ///     1. The POCO class used must have the same (equivalent!) datatypes and property names
    ///        as the TVP declared in SQL Server.
    ///        
    ///        The TVP TypeName will be determined by the concatenation of either the default or 
    ///        a custom schema, an optional type prefix, and then the name of the POCO class:
    ///        
    ///             SqlTypeName = String.Format("{0}{1}{2}", (C_CustomTVPSchema ?? "dbo."), C_CustomTVPPrefix, aTypes[i].Name);
    /// 
    ///        It is possible to override this behaviour by passing the TypeName to use as part of the 
    ///        RegisterProcedure<>() call:
    ///        
    ///             TVPExtensions.RegisterProcedure<Contact, EmployeeContact>("dbo.SaveEmployeeContacts", null, "hr.uddtEmployeeContacts");
    /// 
    ///        The set of TypeNames is an ordered list, so pass a null value for any Types where the
    ///        default value is acceptable.
    ///        
    ///     2. The parameters passed to the ExecuteTVPProcedure<>() method must be in the same order as the 
    ///        declaration, and be an IEnumerable<> of the declared type.
    ///     
    ///             myContext.ExecuteTVPProcedure<Contact, EmployeeContact>(ContactsList, EmpContactsList);
    /// 
    ///     3. Additional non-TVP parameters are permitted, after all other TVP parameters have been referenced:
    ///     
    ///             myContext.ExecuteTVPProcedure<Contact>(Contacts, false);
    ///             
    ///        The SqlDbType used in the stored procedure invocation is based on the type of the passed parameter.
    ///        A null value will be handled properly, presuming the stored procedure can handle a null input.
    /// 
    ///     4. When creating the SQL Stored Procedure, it is recommended to declare with an associated
    ///        default value for any non-TVP parameters:
    ///        
    ///             CREATE PROCEDURE dbo.SaveContacts(@Contact dbo.Contact READONLY, @Flag bit = 0)
    ///             
    ///     5. The RETURN_VALUE of the executed stored procedure is returned by the ExecuteTVPProcedure<>() call.
    ///        This is an integer-type value, and any other result set(s) will be ignored.
    ///        
    ///     6. All stored procedure parameters are passed 'positionally'. Be very careful when ALTERing
    ///        existing stored procedure definitions or bad things might happen! ;-}
    /// 
    /// </summary>
    public static class TVPExtensions
    {
        #region Constants
        /// <summary>
        /// Used as the schema for the Sql Table Type name. If null, "dbo." is used.
        /// </summary>
        public static string C_CustomTVPSchema = null;

        /// <summary>
        /// Any value for the following will be used as a prefix to the POCO class
        /// name, e.g., "uddt" results in "dbo.uddtMyTableClass".
        /// </summary>
        public static string C_CustomTVPPrefix = null;

        public const BindingFlags C_DefaultBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty;
        #endregion

        #region Variables
        /// <summary>
        /// Dictionary to hold all TVPProcedure registrations.
        /// </summary>
        private static readonly ConcurrentDictionary<string, TVPProcInfo> _Registrations = new ConcurrentDictionary<string, TVPProcInfo>();

        /// <summary>
        /// Mapping dictionary used to convert from native to Sql types.
        /// </summary>
        private static readonly Dictionary<Type, SqlDbType> _SqlDataTypes = new Dictionary<Type, SqlDbType>();
        #endregion

        #region Static Constructor
        static TVPExtensions()
        {
            InitializeSqlTypesDictionary();

            // Initialize the Inflector to use 'en-us'.
            Inflector.Inflector.SetDefaultCultureFunc = () => new CultureInfo("en");
        }
        #endregion

        #region Properties
        /// <summary>
        /// Expose the Registrations as an internal property to aid in unit testing.
        /// </summary>
        internal static ConcurrentDictionary<string, TVPProcInfo> Registrations
        {
            get { return _Registrations; }
        }
        #endregion

        #region Public Methods
        #region Execute-related overloads.
        public static int ExecuteTVPProcedure<T1>(this SqlConnection aConnection, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1) }, null, args);
        }

        public static int ExecuteTVPProcedure<T1>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1) }, aProcName, args);
        }

        public static int ExecuteTVPProcedure<T1, T2>(this SqlConnection aConnection, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1), typeof(T2) }, null, args);
        }

        public static int ExecuteTVPProcedure<T1, T2>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1), typeof(T2) }, aProcName, args);
        }

        public static int ExecuteTVPProcedure<T1, T2, T3>(this SqlConnection aConnection, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3) }, null, args);
        }

        public static int ExecuteTVPProcedure<T1, T2, T3>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3) }, aProcName, args);
        }

        public static int ExecuteTVPProcedure<T1, T2, T3, T4>(this SqlConnection aConnection, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, null, args);
        }

        public static int ExecuteTVPProcedure<T1, T2, T3, T4>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, aProcName, args);
        }

        public static int ExecuteTVPProcedure<T1, T2, T3, T4, T5>(this SqlConnection aConnection, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) }, null, args);
        }

        public static int ExecuteTVPProcedure<T1, T2, T3, T4, T5>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return _ExecuteTVPProcedure(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) }, aProcName, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1>(this SqlConnection aConnection, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1) }, null, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1) }, aProcName, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1, T2>(this SqlConnection aConnection, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1), typeof(T2) }, null, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1, T2>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1), typeof(T2) }, aProcName, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1, T2, T3>(this SqlConnection aConnection, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3) }, null, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1, T2, T3>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3) }, aProcName, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1, T2, T3, T4>(this SqlConnection aConnection, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, null, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1, T2, T3, T4>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, aProcName, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1, T2, T3, T4, T5>(this SqlConnection aConnection, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) }, null, args);
        }

        public static async Task<int> ExecuteTVPProcedureAsync<T1, T2, T3, T4, T5>(this SqlConnection aConnection, string aProcName = null, params object[] args)
        {
            return await _ExecuteTVPProcedureAsync(aConnection, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) }, aProcName, args);
        }
        #endregion

        #region Registration-related overloads.
        public static void RegisterProcedure<T1>(string aProcName, params string[] args)
        {
            _RegisterProcedure(aProcName, args, typeof(T1));
        }

        public static void RegisterProcedure<T1, T2>(string aProcName, params string[] args)
        {
            _RegisterProcedure(aProcName, args, typeof(T1), typeof(T2));
        }

        public static void RegisterProcedure<T1, T2, T3>(string aProcName, params string[] args)
        {
            _RegisterProcedure(aProcName, args, typeof(T1), typeof(T2), typeof(T3));
        }

        public static void RegisterProcedure<T1, T2, T3, T4>(string aProcName, params string[] args)
        {
            _RegisterProcedure(aProcName, args, typeof(T1), typeof(T2), typeof(T3), typeof(T4));
        }

        public static void RegisterProcedure<T1, T2, T3, T4, T5>(string aProcName, params string[] args)
        {
            _RegisterProcedure(aProcName, args, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));
        }
        #endregion

        #region Miscellaneous
        /// <summary>
        /// Helper method to convert a Typed enumerable to a DataTable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="aData"></param>
        /// <returns></returns>
        public static DataTable ToDataTable<T>(this IEnumerable<T> aData)
        {
            Validation.AssertNotNull(aData, "aData");

            Type type = typeof(T);

            // Start by extracting the Properties of the Type.
            var Properties = GetProperties(type).ToList();

            // Just pass all the parameters to the internal method, let it do the heavy lifting.
            return _ToDataTable(type, (IEnumerable<object>)aData, Properties);
        }

        public static IEnumerable<PropertyInfo> GetProperties(Type aType, BindingFlags aBindingFlags = C_DefaultBindingFlags, bool aIgnoreIndex = true, bool aIgnoreEnumerables = true)
        {
            var Properties = aType.GetProperties(aBindingFlags);

            foreach (var prop in Properties)
            {
                if (aIgnoreIndex || aIgnoreEnumerables)
                {
                    if (aIgnoreIndex && IsIndexer(prop))
                    {
                        continue;
                    }

                    if (aIgnoreEnumerables && IsEnumerable(prop))
                    {
                        continue;
                    }
                }

                yield return prop;
            }
        }

        /// <summary>
        /// Helper extension method to convert from native type to SqlDbType.
        /// </summary>
        /// <param name="aType"></param>
        /// <returns></returns>
        public static SqlDbType ToSqlType(this Type aType)
        {
            Validation.AssertNotNull(aType, "aType");

            return _SqlDataTypes[aType];
        }

        #endregion
        #endregion

        #region Internal Methods and Helpers
        /// <summary>
        /// Internal method where all the other ExecuteTVPProcedure() calls end up.
        /// </summary>
        /// <param name="aConnection"></param>
        /// <param name="aTypes"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private static int _ExecuteTVPProcedure(SqlConnection aConnection, Type[] aTypes, string aProcName = null, params object[] args)
        {

            int result = -1;

            // Invoke the prepare method to do all the hard work.
            _PrepareRequest(aConnection, aTypes, out string SQL, out SqlParameter[] SqlParameters, out SqlParameter ResultParam, aProcName, args);

            // Execute the SQL, with the trailing comma removed
            result = aConnection.ExecuteSqlCommand(SQL, SqlParameters);

            // Get the result of the sproc invocation.
            result = Convert.ToInt32(ResultParam.Value);

            return result;
        }

        private async static Task<int> _ExecuteTVPProcedureAsync(SqlConnection aConnection, Type[] aTypes, string aProcName = null, params object[] args)
        {

            int result = -1;

            // Invoke the prepare method to do all the hard work.
            _PrepareRequest(aConnection, aTypes, out string SQL, out SqlParameter[] SqlParameters, out SqlParameter ResultParam, aProcName, args);

            // Execute the SQL, with the trailing comma removed
            result = await aConnection.ExecuteSqlCommandAsync(SQL, SqlParameters);

            // Get the result of the sproc invocation.
            result = Convert.ToInt32(ResultParam.Value);

            return result;
        }

        private static void _PrepareRequest(SqlConnection aConnection, Type[] aTypes, out string aSQL, out SqlParameter[] aSQLParameters, out SqlParameter aResultParam, string aProcName = null, params object[] args)
        {
            // Validate the inputs
            Validation.AssertNotNull(aConnection, "aConnection");
            Validation.AssertNotNull(args, "args");
            Validation.AssertNotZero(args.Length, "args");

            List<object> Parameters = new List<object>();

            // Assert the args count >= aTypes count.
            if (aTypes.Length > args.Length)
                throw new ArgumentOutOfRangeException("The number of referenced Types must be less than the number of passed arguments!");

            // Make sure the specific sproc is pre-registered.
            string KeyName = DetermineKeyName(aProcName, aTypes);

            if (!_Registrations.TryGetValue(KeyName, out TVPProcInfo ProcInfo) && !_Registrations.TryGetValue(DetermineKeyName(null, aTypes), out ProcInfo))
            {
                // Perform auto-registration, with special support for a subtle race condition.
                try
                {
                    ProcInfo = _RegisterProcedure(null, null, aTypes);
                }
                catch (AlreadyRegisteredException)
                {
                    // It wasn't registered, but now it is.
                    _Registrations.TryGetValue(KeyName, out ProcInfo);
                }
            }

            // Compose the SQL Statement to execute, deferring to the inputted Procedure name, if present.
            var SQL = new StringBuilder("EXEC @Result = " + (aProcName ?? ProcInfo.ProcedureName));

            // Verify type "appropriateness" in the passed arguments, create datatables, and add to parameters list.
            for (int i = 0; i < aTypes.Length; i++)
            {
                // Create an Enumerable type of the referenced type.
                var ParamType = Type.GetType(String.Format("System.Collections.Generic.IEnumerable`1[{0}]", aTypes[i].Name));

                // Determine if the parameter is an enumerable.
                var EnumerableType = args[i].GetType().GetEnumerableType();

                // Make sure they're assignable between the declared and passed types.
                if ((EnumerableType == null) || (!EnumerableType.IsAssignableFrom(aTypes[i])))
                    throw new ArgumentException(String.Format("Input parameter type [{0}] is not of the expected type '{1}'.", args[i].GetType(), ParamType));

                // Convert the data to a datatable.
                var dt = _ToDataTable(aTypes[i], (IEnumerable<object>)args[i], ProcInfo.Details[i].Properties);
                Parameters.Add(dt);

                // Add a new parameter placeholder to the SQL.
                SQL.AppendFormat(" @P{0},", i);
            }

            // Add any remaining input parameters.
            for (int i = aTypes.Length; i < args.Length; i++)
            {
                SQL.AppendFormat(" @P{0},", i);
                Parameters.Add(args[i]);
            }

            // Convert the parameters list to an equivalent SqlParameters array.
            var SqlParameters = ConvertToSqlParams(ProcInfo, Parameters, aTypes.Length, out aResultParam);

            // If it makes it this far, assign the out variables and return.
            // And return the SQL string without the trailing comma
            aSQL = SQL.ToString().TrimEnd(',', ' ');
            aSQLParameters = SqlParameters;
        }

        private static TVPProcInfo _RegisterProcedure(string aProcName = null, string[] aTVPTypeNames = null, params Type[] aTypes)
        {
            // Auto-registration is possible, but only for single-type invocations.
            Validation.AssertArgument(!String.IsNullOrWhiteSpace(aProcName) || (aTypes.Length == 1), "The 'aProcName' parameter cannot be null or empty when more than a single Type is being Registered!");

            if (String.IsNullOrWhiteSpace(aProcName))
            {
                // For auto-registration, determine the sproc name based on the pluralization of the type
                // and using the default schema.
                aProcName = String.Format("dbo.Save{0}", aTypes[0].Name.Pluralize());
            }

            var KeyName = DetermineKeyName(aProcName, aTypes);
            var EmptyKeyName = DetermineKeyName(null, aTypes);

            // Ensure only a single registration per ordered TVP-type combination and sproc name.
            if (_Registrations.TryGetValue(KeyName, out TVPProcInfo found))
                throw new AlreadyRegisteredException(String.Format("A stored procedure named '{0}' has already been registered for the Type set [{1}].", found.ProcedureName, KeyName));

            // If an EmptyKeyName registration already exists, reuse the TVPProcInfo. This 
            // will happen with multiple RegisterProcedure() calls for the same TVP-type set.
            if (_Registrations.TryGetValue(EmptyKeyName, out TVPProcInfo TPI))
            {
                // Register it under the additional sproc name and return it.
                _Registrations[KeyName] = TPI;
                return TPI;
            }

            // Create and populate a TVPProcInfo instance.
            TPI = new TVPProcInfo() { ProcedureName = aProcName };

            // Iterate the Types and create a TVPProcInfo per instance.
            for (int i = 0; i < aTypes.Length; i++)
            {
                // See if we have a user-defined name or should use the convention, including any custom schema or prefix.
                var STN = ((aTVPTypeNames != null) && (aTVPTypeNames.Length > i) && (!String.IsNullOrWhiteSpace(aTVPTypeNames[i]))) ?
                                aTVPTypeNames[i] :
                                String.Format("{0}{1}{2}", (C_CustomTVPSchema ?? "dbo."), C_CustomTVPPrefix, aTypes[i].Name);

                var TD = new TVPDetails() { SqlTypeName = STN, Properties = GetProperties(aTypes[i]).ToList() };

                // Add it to the sproc tracker.
                TPI.Details.Add(TD);
            }

            // Register with the sproc name as part of the key.
            _Registrations[KeyName] = TPI;

            // Attempt to register a reference for the TVP-type set with an empty sproc
            // name as the default for when executing without passing sproc name.
            // It is okay if this does not succeed when registering a second sproc
            // using the same types.
            _Registrations.TryAdd(EmptyKeyName, TPI);

            // Return the created ProcInfo instance.
            return TPI;
        }

        /// <summary>
        /// This helper converts the input list of native pararmeters to the
        /// equivalent SqlParameter.
        /// </summary>
        /// <returns></returns>
        private static SqlParameter[] ConvertToSqlParams(TVPProcInfo aProcInfo, List<object> aParameters, int aTVPCount, out SqlParameter aResultParam)
        {
            // Iterate the input parameters and convert to SqlParameters.
            List<SqlParameter> result = new List<SqlParameter>();
            for (int i = 0; i < aParameters.Count; i++)
            {
                // When i < aTypes.Length, the parameter will be a 'Structured' type.
                // If not a table, then make special allowance for a null value being passed
                // and set the parameter type to VarChar, since this can accept a DbNull value.
                SqlParameter p = new SqlParameter(String.Format("@P{0}", i), (i < aTVPCount) ? SqlDbType.Structured : (aParameters[i] != null) ? _SqlDataTypes[aParameters[i].GetType()] : SqlDbType.VarChar)
                {
                    Direction = ParameterDirection.Input
                };

                // For TableType parameters, the SQL TypeName is required.
                if (i < aTVPCount)
                {
                    p.TypeName = aProcInfo.Details[i].SqlTypeName;
                }

                // If value is present then set it, else set DbNull and the length (mini optimization).
                if (aParameters[i] != null)
                {
                    p.Value = aParameters[i];
                }
                else
                {
                    p.Value = DBNull.Value;
                    p.Size = 1;
                }

                result.Add(p);
            }

            // Add a final parameter representing the ReturnValue.
            aResultParam = new SqlParameter("@Result", SqlDbType.Int)
            {
                Value = result,
                Direction = ParameterDirection.Output
            };
            result.Add(aResultParam);

            // Return result as an array, as this is required by ExecuteSqlCommand().
            return result.ToArray();
        }

        private static DataTable _ToDataTable(Type aType, IEnumerable<object> aData, List<PropertyInfo> aProperties)
        {
            DataTable result = new DataTable();

            // Create table schema using Property values.
            // If anything is an Enum, force to an int.
            foreach (var prop in aProperties)
            {
                result.Columns.Add(prop.Name, (prop.PropertyType.IsEnum) ? typeof(Int32) : prop.PropertyType);
            }

            // Populate the Data values row by row.
            object[] DataValues = new object[aProperties.Count];
            foreach (var item in aData)
            {
                for (int i = 0; i < aProperties.Count; i++)
                {
                    DataValues[i] = aProperties[i].GetValue(item, null);
                }

                result.Rows.Add(DataValues);
            }

            return result;
        }

        /// <summary>
        /// Initialize the dictionary that maps native to Sql types.
        /// </summary>
        private static void InitializeSqlTypesDictionary()
        {
            _SqlDataTypes.Add(typeof(string), SqlDbType.NVarChar);
            _SqlDataTypes.Add(typeof(Guid), SqlDbType.UniqueIdentifier);
            _SqlDataTypes.Add(typeof(long), SqlDbType.BigInt);
            _SqlDataTypes.Add(typeof(byte[]), SqlDbType.Binary);
            _SqlDataTypes.Add(typeof(bool), SqlDbType.Bit);
            _SqlDataTypes.Add(typeof(DateTime), SqlDbType.DateTime);
            _SqlDataTypes.Add(typeof(decimal), SqlDbType.Decimal);
            _SqlDataTypes.Add(typeof(double), SqlDbType.Float);
            _SqlDataTypes.Add(typeof(int), SqlDbType.Int);
            _SqlDataTypes.Add(typeof(float), SqlDbType.Real);
            _SqlDataTypes.Add(typeof(short), SqlDbType.SmallInt);
            _SqlDataTypes.Add(typeof(byte), SqlDbType.TinyInt);
            _SqlDataTypes.Add(typeof(object), SqlDbType.Udt);
            _SqlDataTypes.Add(typeof(DataTable), SqlDbType.Structured);
            _SqlDataTypes.Add(typeof(DateTimeOffset), SqlDbType.DateTimeOffset);
        }

        private static string DetermineKeyName(string aProcName = null, params Type[] aTypes)
        {
            // Create a concatenated string separated by '|' delimeter.
            string[] TypeNames = Array.ConvertAll(aTypes, t => t.FullName);

            // Tack the ProcName at the end. The trailing delimiter is okay.
            string result = String.Format("{0}|{1}", String.Join("|", TypeNames), aProcName);

            return result;
        }

        /// <summary> 
        /// Check if property is indexer.
        /// </summary> 
        private static bool IsIndexer(PropertyInfo aProperty)
        {
            var parameters = aProperty.GetIndexParameters();

            return ((parameters != null) && (parameters.Length > 0));
        }

        /// <summary> 
        /// Determine if property implements IEnumerable and optionally, is not a string.
        /// </summary> 
        private static bool IsEnumerable(PropertyInfo aProperty, bool aIgnoreStrings = true)
        {
            return (((aProperty.PropertyType.Equals(typeof(string))) && !aIgnoreStrings) &&
                    (aProperty.PropertyType.GetInterfaces().Any(x => x.Equals(typeof(System.Collections.IEnumerable)))));
        }
        #endregion

        #region Internal Helper Classes
        internal class TVPDetails
        {
            #region Properties
            public string SqlTypeName { get; set; }

            public List<PropertyInfo> Properties { get; set; }
            #endregion
        }

        internal class TVPProcInfo
        {
            #region Constructor
            public TVPProcInfo()
            {
                Details = new List<TVPDetails>();
            }
            #endregion

            #region Properties
            public string ProcedureName { get; set; }

            public List<TVPDetails> Details { get; private set; }
            #endregion
        }
        #endregion
    }

    /// <summary>
    /// This "marker" exception is created solely to workaround a subtle
    /// race condition when using auto-registration.
    /// </summary>
    public class AlreadyRegisteredException : InvalidOperationException
    {
        public AlreadyRegisteredException(string message) : base(message)
        {
        }
    }

    public static class SqlConnectionExtensions
    {
        public static int ExecuteSqlCommand(this SqlConnection aConnection, string aSQL, params object[] parameters)
        {
            Validation.AssertNotNull(aConnection, "aConnection");
            Validation.AssertNotEmpty(aSQL, "aSQL");

            var result = -1;

            SqlCommand Cmd = new SqlCommand(aSQL, aConnection)
            {
                CommandType = CommandType.Text
            };

            Cmd.Parameters.AddRange(parameters);

            try
            {
                aConnection.Open();

                result = Cmd.ExecuteNonQuery();
            }
            catch (SqlException sqlEx)
            {
                throw new Exception($"SQL Exception detected: [{sqlEx.Message}]", sqlEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception detected: [{ex.Message}]", ex);
            }
            finally
            {
                aConnection.Close();
            }

            return result;
        }

        public static Task<int> ExecuteSqlCommandAsync(this SqlConnection aConnection, string aSQL, params object[] parameters)
        {
            Validation.AssertNotNull(aConnection, "aConnection");
            Validation.AssertNotEmpty(aSQL, "aSQL");

            return Task.FromResult(-1);
        }
    }
}
