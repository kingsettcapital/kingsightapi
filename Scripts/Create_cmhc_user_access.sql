-- CMHC dashboard role-based access.
-- One row per KingSight user (user_master.user_id) who has CMHC sub-roles.
--   cmhc_roles  : comma-separated, any of Underwriter, Reviewer, Originator (e.g. 'Underwriter,Originator')
--   linked_name : the name exactly as it appears in the DealPath loan data
--                 (Funder / Underwriter for Underwriters, Originating Originator for Originators)
-- Admins (role_master Admin) see every page and every loan and need no row here.
-- CMHC-role users with no row / no sub-roles are CMHC section admins: every page, every loan.
-- Run in the Fabric warehouse SQL editor (wh_gold) for each environment.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'subjective_input'
      AND TABLE_NAME = 'cmhc_user_access')
BEGIN
    CREATE TABLE [subjective_input].[cmhc_user_access]
    (
        [user_id]          INT           NOT NULL,
        [cmhc_roles]       VARCHAR(100)  NULL,
        [linked_name]      VARCHAR(256)  NULL,
        [updated_by]       VARCHAR(256)  NULL,
        [updated_datetime] DATETIME2(6)  NULL
    );
END
GO

-- Example (replace the user ids / names):
-- INSERT INTO [subjective_input].[cmhc_user_access] (user_id, cmhc_roles, linked_name, updated_by, updated_datetime)
-- VALUES (12, 'Underwriter', 'Jane Smith', 'setup script', SYSUTCDATETIME()),
--        (13, 'Reviewer',    NULL,         'setup script', SYSUTCDATETIME()),
--        (14, 'Originator',  'Tom Chen',   'setup script', SYSUTCDATETIME());

-- Check
SELECT a.user_id, u.email, a.cmhc_roles, a.linked_name, a.updated_by, a.updated_datetime
FROM [subjective_input].[cmhc_user_access] a
LEFT JOIN [subjective_input].[user_master] u ON u.user_id = a.user_id
ORDER BY a.user_id;
