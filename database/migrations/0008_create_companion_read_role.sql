IF DATABASE_PRINCIPAL_ID(N'AdventuresSuiteCompanionReadRuntime') IS NULL
BEGIN
    CREATE ROLE AdventuresSuiteCompanionReadRuntime AUTHORIZATION dbo;
END;

GRANT SELECT ON OBJECT::planning.AdventurePlans TO AdventuresSuiteCompanionReadRuntime;
GRANT SELECT ON OBJECT::planning.TravelerParticipations TO AdventuresSuiteCompanionReadRuntime;
GRANT SELECT ON OBJECT::planning.DestinationVisits TO AdventuresSuiteCompanionReadRuntime;
GRANT SELECT ON OBJECT::auth.CreatorMemberships TO AdventuresSuiteCompanionReadRuntime;
GRANT SELECT ON OBJECT::auth.CreatorMembershipRoles TO AdventuresSuiteCompanionReadRuntime;
GRANT SELECT ON OBJECT::auth.CreatorMembershipPermissionGrants TO AdventuresSuiteCompanionReadRuntime;

DENY INSERT, UPDATE, DELETE ON OBJECT::planning.AdventurePlans TO AdventuresSuiteCompanionReadRuntime;
DENY INSERT, UPDATE, DELETE ON OBJECT::planning.TravelerParticipations TO AdventuresSuiteCompanionReadRuntime;
DENY INSERT, UPDATE, DELETE ON OBJECT::planning.DestinationVisits TO AdventuresSuiteCompanionReadRuntime;
DENY INSERT, UPDATE, DELETE ON OBJECT::auth.CreatorMemberships TO AdventuresSuiteCompanionReadRuntime;
DENY INSERT, UPDATE, DELETE ON OBJECT::auth.CreatorMembershipRoles TO AdventuresSuiteCompanionReadRuntime;
DENY INSERT, UPDATE, DELETE ON OBJECT::auth.CreatorMembershipPermissionGrants TO AdventuresSuiteCompanionReadRuntime;

DENY ALTER, CONTROL ON SCHEMA::planning TO AdventuresSuiteCompanionReadRuntime;
DENY ALTER, CONTROL ON SCHEMA::auth TO AdventuresSuiteCompanionReadRuntime;
