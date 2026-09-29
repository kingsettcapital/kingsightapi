-- User Management — roles and users in schema input.
-- Microsoft Fabric Warehouse: VARCHAR (not NVARCHAR).
--
-- Drops and recreates tables so RoleId/UserId are plain INT (no IDENTITY).
-- Sequential ids 1, 2, 3… come from the seed script and API: ISNULL(MAX(id), 0) + 1.
-- If you previously created these with BIGINT IDENTITY, you must run this script
-- before the seed script or inserts will fail with Msg 544.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'input')
BEGIN
    EXEC('CREATE SCHEMA input');
END;

IF OBJECT_ID('input.UserMst', 'U') IS NOT NULL
BEGIN
    DROP TABLE input.UserMst;
END;

IF OBJECT_ID('input.RoleMst', 'U') IS NOT NULL
BEGIN
    DROP TABLE input.RoleMst;
END;

CREATE TABLE input.RoleMst (
    RoleId    INT             NOT NULL,
    RoleName  VARCHAR(255)    NOT NULL,
    Status    VARCHAR(1)      NULL
);

CREATE TABLE input.UserMst (
    UserId       INT             NOT NULL,
    Email        VARCHAR(255)    NOT NULL,
    FirstName    VARCHAR(255)    NULL,
    LastName     VARCHAR(255)    NULL,
    IsActive     BIT             NOT NULL,
    DateCreated  DATETIME2(6)    NOT NULL,
    DateModified DATETIME2(6)    NULL,
    RoleId       INT             NOT NULL
);
