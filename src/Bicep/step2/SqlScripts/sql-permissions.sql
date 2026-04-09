IF NOT EXISTS(SELECT * FROM sys.database_principals WHERE [name] = '_applicationIdentity_')
BEGIN
    CREATE USER [_applicationIdentity_] FROM EXTERNAL PROVIDER;
END;
GO

ALTER ROLE db_datareader ADD MEMBER [_applicationIdentity_];
ALTER ROLE db_datawriter ADD MEMBER [_applicationIdentity_];

GO