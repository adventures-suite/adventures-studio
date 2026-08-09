ALTER TABLE auth.UserSessions
    ADD ExternalIdentityId nvarchar(64) COLLATE Latin1_General_100_BIN2 NULL;

ALTER TABLE auth.ExternalIdentities
    ADD CONSTRAINT UQ_ExternalIdentities_Id_User
        UNIQUE (ExternalIdentityId, UserId);

ALTER TABLE auth.UserSessions
    ADD CONSTRAINT FK_UserSessions_ExternalIdentity
        FOREIGN KEY (ExternalIdentityId, UserId)
        REFERENCES auth.ExternalIdentities (ExternalIdentityId, UserId);

CREATE INDEX IX_UserSessions_ExternalIdentity_Revocation_Expiry
    ON auth.UserSessions (ExternalIdentityId, RevokedAtUtc, AbsoluteExpiresAtUtc)
    INCLUDE (UserId, SecurityVersion, LastSeenAtUtc);

-- Existing sessions predate an authoritative establishing-identity binding and
-- therefore remain NULL and fail closed during validation. Every newly created
-- session supplies a validated active mapping through the repository.
