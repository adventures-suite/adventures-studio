IF DB_ID(N'$(DatabaseName)') IS NULL THROW 51000, 'Apply migrations before provisioning the application identity.', 1;
IF SUSER_ID(N'$(ApplicationLogin)') IS NULL
BEGIN
    DECLARE @CreateLogin nvarchar(max)=N'CREATE LOGIN [' + REPLACE('$(ApplicationLogin)',']',']]')
        + N'] WITH PASSWORD=N''' + REPLACE('$(ApplicationPassword)', '''', '''''') + N''', CHECK_POLICY=ON;';
    EXEC sys.sp_executesql @CreateLogin;
END;
GO
USE [$(DatabaseName)];
GO
IF USER_ID(N'$(ApplicationLogin)') IS NULL CREATE USER [$(ApplicationLogin)] FOR LOGIN [$(ApplicationLogin)];
IF IS_ROLEMEMBER(N'AdventuresSuiteAuthenticationRuntime', N'$(ApplicationLogin)')<>1 ALTER ROLE AdventuresSuiteAuthenticationRuntime ADD MEMBER [$(ApplicationLogin)];
IF IS_ROLEMEMBER(N'AdventuresSuiteMembershipRuntime', N'$(ApplicationLogin)')<>1 ALTER ROLE AdventuresSuiteMembershipRuntime ADD MEMBER [$(ApplicationLogin)];
IF IS_ROLEMEMBER(N'AdventuresSuitePlanningRuntime', N'$(ApplicationLogin)')<>1 ALTER ROLE AdventuresSuitePlanningRuntime ADD MEMBER [$(ApplicationLogin)];
GRANT CONNECT TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.AdventurePlans TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.Travelers TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.TravelerPreferences TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.DestinationVisits TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.ItineraryDays TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.PlannedActivities TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.TransportationSegments TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.Accommodations TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.Reservations TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.PlanningNotes TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.PlanningTasks TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.BudgetItems TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.PackingItems TO [$(ApplicationLogin)];
GRANT SELECT ON OBJECT::planning.TravelerParticipations TO [$(ApplicationLogin)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::planning.AdventurePlans TO [$(ApplicationLogin)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::planning.DestinationVisits TO [$(ApplicationLogin)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::planning.ItineraryDays TO [$(ApplicationLogin)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::planning.PlannedActivities TO [$(ApplicationLogin)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::planning.TransportationSegments TO [$(ApplicationLogin)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::planning.Accommodations TO [$(ApplicationLogin)];
GRANT SELECT, INSERT, UPDATE ON OBJECT::planning.Reservations TO [$(ApplicationLogin)];
DENY DELETE ON SCHEMA::planning TO [$(ApplicationLogin)];
DENY ALTER ON SCHEMA::planning TO [$(ApplicationLogin)];
GO
