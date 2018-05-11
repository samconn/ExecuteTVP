using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using FluentAssertions;
using III.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace III.UnitTests
{
    [TestClass]
    public class TVPStoredProcedureTests
    {
        #region Constants
        public static string            C_DefaultConnectionString           = "Data Source=(LocalDB)\\MSSQLLocalDB ;AttachDbFilename={0}\\SQL\\III-Test.mdf;Initial Catalog=III-Test;Integrated Security=True;MultipleActiveResultSets=true";
        #endregion


        [ClassInitialize]
        public static void Setup(TestContext aTestContext)
        {
            // Update the default connection string with the current directory.
            C_DefaultConnectionString = String.Format(C_DefaultConnectionString, Directory.GetCurrentDirectory());
        }

        [TestMethod]
        public void VerifySaveContactSimple()
        {
            var Cn = new SqlConnection(C_DefaultConnectionString);

            var Contacts = CreateTestContacts();

            var result = Cn.ExecuteTVPProcedure<Contact>(Contacts);

            result.Should().Be(1, "The dbo.SaveContacts sproc didn't return expected result");
        }

        [TestMethod]
        public void VerifySaveContactsMany()
        {
            var Cn = new SqlConnection(C_DefaultConnectionString);

            var ExpectedCount = 100;
            var Contacts = CreateTestContacts(ExpectedCount);

            var result = Cn.ExecuteTVPProcedure<Contact>(Contacts);

            result.Should().Be(ExpectedCount, "The dbo.SaveContacts sproc didn't return expected result");
        }

        [TestMethod]
        public void VerifySaveCompanySimple()
        {
            var Cn = new SqlConnection(C_DefaultConnectionString);

            var Companys = CreateTestCompanys();

            var result = Cn.ExecuteTVPProcedure<Company>(Companys);

            result.Should().Be(1, "The dbo.SaveCompanies sproc didn't return expected result");
        }

        [TestMethod]
        public void VerifySaveCompanysMany()
        {
            var Cn = new SqlConnection(C_DefaultConnectionString);

            var ExpectedCount = 100;
            var Companys = CreateTestCompanys(ExpectedCount);

            var result = Cn.ExecuteTVPProcedure<Company>(Companys);

            result.Should().Be(ExpectedCount, "The dbo.SaveCompanies sproc didn't return expected result");
        }

        [TestMethod]
        public void VerifyUpdateAllContactsSimple()
        {
            var Cn = new SqlConnection(C_DefaultConnectionString);

            var ExpectedCount = 25;
            var Contacts = CreateTestContacts(ExpectedCount);

            var result = Cn.ExecuteTVPProcedure<Contact>("dbo.UpdateAllContacts", Contacts);

            result.Should().Be(ExpectedCount, "The dbo.UpdateAllContacts sproc didn't return expected result");
        }

        [TestMethod]
        public void VerifyProcessEmployeeContactsRegisterAndExecute()
        {
            TVPExtensions.RegisterProcedure<Contact, EmployeeContact>("dbo.ProcessEmployeeContacts", null, "hr.uddtEmployeeContact");

            var Cn = new SqlConnection(C_DefaultConnectionString);

            var ExpectedContactCount = Constants.GetRandomInt(10, 35);
            var Contacts = CreateTestContacts(ExpectedContactCount);

            var ExpectedEmployeeContactCount = Constants.GetRandomInt(17, 29);
            var EmployeeContacts = CreateTestEmployeeContacts(ExpectedEmployeeContactCount);

            var result = Cn.ExecuteTVPProcedure<Contact, EmployeeContact>("dbo.ProcessEmployeeContacts", Contacts, EmployeeContacts);

            result.Should().Be(ExpectedContactCount + ExpectedEmployeeContactCount, "The dbo.ProcessEmployeeContacts sproc didn't return expected result");
        }

        [TestMethod]
        public void VerifyAlternateSaveContactsWithExtraParams()
        {
            var Cn = new SqlConnection(C_DefaultConnectionString);

            var ExpectedCount = 33;
            var Contacts = CreateTestContacts(ExpectedCount);

            var ContactBottleCount = Constants.GetRandomInt(14, 56);

            var result = Cn.ExecuteTVPProcedure<Contact>("SaveContactBeerConsumptionPreferences", Contacts, false, ContactBottleCount);

            result.Should().Be(ExpectedCount + ContactBottleCount, "The dbo.SaveContactBeerConsumptionPreferences sproc didn't return expected result");
        }

        [TestMethod]
        public void VerifyAlternateSaveContactsWithExtraParamsMinuResult()
        {
            var Cn = new SqlConnection(C_DefaultConnectionString);

            var ExpectedCount = 33;
            var Contacts = CreateTestContacts(ExpectedCount);

            var ContactBottleCount = Constants.GetRandomInt(14, 56);

            var result = Cn.ExecuteTVPProcedure<Contact>("SaveContactBeerConsumptionPreferences", Contacts, true, ContactBottleCount);

            result.Should().Be(-1 * (ExpectedCount + ContactBottleCount), "The dbo.SaveContactBeerConsumptionPreferences sproc didn't return expected result");
        }

        #region Helper Methods
        private IEnumerable<Contact> CreateTestContacts(int Count = 1)
        {
            var result = new List<Contact>();

            for (int i = 0; i < Count; i++)
            {
                var C = new Contact()
                {
                    ContactKey = Constants.GetRandomInt(),
                    ContactType = 'E',
                    FirstName = Constants.GetRandomString(30),
                    MiddleName = Constants.GetRandomString(30),
                    LastName = Constants.GetRandomString(30),
                    Email = Constants.GetRandomString(30),
                    Address1 = Constants.GetRandomString(100),
                    City = Constants.GetRandomString(30),
                    State = Constants.GetRandomString(2),
                    ZipCode = Constants.GetRandomString(5, aRangeFrom: 49, aRangeThru: 57)
                };

                result.Add(C);
            }

            return result;
        }

        private IEnumerable<Company> CreateTestCompanys(int Count = 1)
        {
            var result = new List<Company>();

            for (int i = 0; i < Count; i++)
            {
                var C = new Company()
                {
                    CompanyKey = Constants.GetRandomInt(),
                    CompanyName = Constants.GetRandomString(50),
                    Address1 = Constants.GetRandomString(100),
                    City = Constants.GetRandomString(30),
                    State = Constants.GetRandomString(2),
                    ZipCode = Constants.GetRandomString(5, aRangeFrom: 49, aRangeThru: 57)
                };

                result.Add(C);
            }

            return result;
        }

        private IEnumerable<EmployeeContact> CreateTestEmployeeContacts(int Count = 1)
        {
            var result = new List<EmployeeContact>();

            for (int i = 0; i < Count; i++)
            {
                var C = new EmployeeContact()
                {
                    EmployeeContactKey = Constants.GetRandomInt(),
                    ContactKey = Constants.GetRandomInt(),
                    CompanyKey = Constants.GetRandomInt(),
                    ManagerContactKey = Constants.GetRandomInt()
                };

                result.Add(C);
            }

            return result;
        }
        #endregion
    }

    #region Test Models
    public class Contact
    {
        public int ContactKey { get; set; }

        public char ContactType { get; set; }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }

        public string Address1 { get; set; }

        public string Address2 { get; set; }

        public string City { get; set; }

        public string State { get; set; }

        public string ZipCode { get; set; }

        public string HomePhone { get; set; }

        public string CellPhone { get; set; }

        public string Email { get; set; }
    }

    public class Company
    {
        public int CompanyKey { get; set; }

        public string CompanyName { get; set; }

        public string Address1 { get; set; }

        public string Address2 { get; set; }

        public string City { get; set; }

        public string State { get; set; }

        public string ZipCode { get; set; }

        public string MainPhone { get; set; }
    }

    public class EmployeeContact
    {
        public int EmployeeContactKey { get; set; }

        public int ContactKey { get; set; }

        public int CompanyKey { get; set; }

        public int ManagerContactKey { get; set; }
    }

    public class ExternalEvent
    {
        public int EventKey { get; set; }

        public DateTime EventDate { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Location { get; set; }
    }
    #endregion
}