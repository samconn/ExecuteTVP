-- Create the HR and EVT schemas.
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'hr')
  EXEC sys.sp_executesql N'CREATE SCHEMA hr'

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'evt')
  EXEC sys.sp_executesql N'CREATE SCHEMA evt'

-- Create the test user-defined table types.
IF NOT EXISTS (SELECT * FROM sys.types st JOIN sys.schemas ss ON st.schema_id = ss.schema_id WHERE st.name = N'Contact' AND ss.name = N'dbo')
  CREATE TYPE dbo.Contact AS TABLE(
      ContactKey INT NOT NULL,
      ContactType CHAR(1) NOT NULL,
      FirstName VARCHAR(100) NULL,
      MiddleName VARCHAR(100) NULL,
      LastName VARCHAR(100) NULL,
      Address1 VARCHAR(255) NULL,
      Address2 VARCHAR(255) NULL,
      City VARCHAR(255) NULL,
      State CHAR(2) NULL,
      ZipCode VARCHAR(10) NULL,
      HomePhone VARCHAR(10) NULL,
      CellPhone VARCHAR(10) NULL,
      Email VARCHAR(255) NULL,
    PRIMARY KEY CLUSTERED (ContactKey ASC)
  )

IF NOT EXISTS (SELECT * FROM sys.types st JOIN sys.schemas ss ON st.schema_id = ss.schema_id WHERE st.name = N'Company' AND ss.name = N'dbo')
  CREATE TYPE dbo.Company AS TABLE(
      CompanyKey INT NOT NULL,
      CompanyName VARCHAR(100) NULL,
      Address1 VARCHAR(255) NULL,
      Address2 VARCHAR(255) NULL,
      City VARCHAR(255) NULL,
      State CHAR(2) NULL,
      ZipCode VARCHAR(10) NULL,
      MainPhone VARCHAR(10) NULL,
    PRIMARY KEY CLUSTERED (CompanyKey ASC)
  )

IF NOT EXISTS (SELECT * FROM sys.types st JOIN sys.schemas ss ON st.schema_id = ss.schema_id WHERE st.name = N'uddtEmployeeContact' AND ss.name = N'hr')
  CREATE TYPE hr.uddtEmployeeContact AS TABLE(
      EmployeeContactKey INT NOT NULL,
      ContactKey INT NOT NULL,
      CompanyKey INT NOT NULL,
      ManagerContactKey INT NULL,
    PRIMARY KEY CLUSTERED (EmployeeContactKey ASC)
  )

IF NOT EXISTS (SELECT * FROM sys.types st JOIN sys.schemas ss ON st.schema_id = ss.schema_id WHERE st.name = N'uddtExternalEvent' AND ss.name = N'evt')
  CREATE TYPE evt.uddtExternalEvent AS TABLE(
      EventKey INT NOT NULL,
      EventDate DATETIME NULL,
      Name VARCHAR(255) NOT NULL,
      Description VARCHAR(2000) NOT NULL,
      Location VARCHAR(255) NOT NULL,
    PRIMARY KEY CLUSTERED (EventKey ASC)
  )
GO

-- Create the test stored procedures. The RESULT will be the the count of the rows in the input tvp parameter.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.SaveContacts') AND type in (N'P', N'PC'))
  EXEC sp_executesql N'CREATE PROCEDURE dbo.SaveContacts AS BEGIN RETURN 0 END'
GO
ALTER PROCEDURE dbo.SaveContacts(@Contacts dbo.Contact READONLY)
AS
BEGIN
  DECLARE @Result int
  SELECT @Result = COUNT(*) FROM @Contacts

  RETURN @Result
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.SaveCompanies') AND type in (N'P', N'PC'))
  EXEC sp_executesql N'CREATE PROCEDURE dbo.SaveCompanies AS BEGIN RETURN 0 END'
GO
ALTER PROCEDURE dbo.SaveCompanies(@Companies dbo.Company READONLY)
AS
BEGIN
  DECLARE @Result int
  SELECT @Result = COUNT(*) FROM @Companies

  RETURN @Result
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.UpdateAllContacts') AND type in (N'P', N'PC'))
  EXEC sp_executesql N'CREATE PROCEDURE dbo.UpdateAllContacts AS BEGIN RETURN 0 END'
GO
ALTER PROCEDURE dbo.UpdateAllContacts(@Contacts dbo.Contact READONLY)
AS
BEGIN
  DECLARE @Result int
  SELECT @Result = COUNT(*) FROM @Contacts

  RETURN @Result
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.SaveEmployeeContacts') AND type in (N'P', N'PC'))
  EXEC sp_executesql N'CREATE PROCEDURE dbo.SaveEmployeeContacts AS BEGIN RETURN 0 END'
GO
ALTER PROCEDURE dbo.SaveEmployeeContacts(@EmployeeContacts hr.uddtEmployeeContact READONLY)
AS
BEGIN
  DECLARE @Result int
  SELECT @Result = COUNT(*) FROM @EmployeeContacts

  RETURN @Result
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.UpdateAllContacts') AND type in (N'P', N'PC'))
  EXEC sp_executesql N'CREATE PROCEDURE dbo.UpdateAllContacts AS BEGIN RETURN 0 END'
GO
ALTER PROCEDURE dbo.UpdateAllContacts(@Contacts dbo.Contact READONLY)
AS
BEGIN
  DECLARE @Result int
  SELECT @Result = COUNT(*) FROM @Contacts

  RETURN @Result
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.ProcessEmployeeContacts') AND type in (N'P', N'PC'))
  EXEC sp_executesql N'CREATE PROCEDURE dbo.ProcessEmployeeContacts AS BEGIN RETURN 0 END'
GO
ALTER PROCEDURE dbo.ProcessEmployeeContacts(@Contacts dbo.Contact READONLY,
                                            @EmployeeContacts hr.uddtEmployeeContact READONLY)
AS
BEGIN
  DECLARE @Result int
  SELECT @Result = COUNT(*) FROM @Contacts

  SELECT @Result = @Result + COUNT(*) FROM @EmployeeContacts

  RETURN @Result
END
GO


IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.SaveContactBeerConsumptionPreferences') AND type in (N'P', N'PC'))
  EXEC sp_executesql N'CREATE PROCEDURE dbo.SaveContactBeerConsumptionPreferences AS BEGIN RETURN 0 END'
GO
ALTER PROCEDURE dbo.SaveContactBeerConsumptionPreferences(@Contacts dbo.Contact READONLY,
                                                          @DomesticOnly bit = 0, 
                                                          @PreferredBottleCount int = 99)
AS
BEGIN
  DECLARE @Result int
  SELECT @Result = COUNT(*) + ISNULL(@PreferredBottleCount, 99) FROM @Contacts

  -- Invert if flag is set.
  IF (@DomesticOnly = 1)
    SELECT @Result = @Result * -1

  RETURN @Result
END
GO
