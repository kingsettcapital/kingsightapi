-- Audit columns for investor alias CRUD (matches subjective_input.investor_alias_master).

IF COL_LENGTH('subjective_input.investor_alias_master', 'created_by') IS NULL

BEGIN

    ALTER TABLE [subjective_input].[investor_alias_master]

    ADD [created_by] VARCHAR(256) NULL;

END

GO



IF COL_LENGTH('subjective_input.investor_alias_master', 'created_datetime') IS NULL

BEGIN

    ALTER TABLE [subjective_input].[investor_alias_master]

    ADD [created_datetime] DATETIME2(6) NULL;

END

GO



IF COL_LENGTH('subjective_input.investor_alias_master', 'updated_by') IS NULL

BEGIN

    ALTER TABLE [subjective_input].[investor_alias_master]

    ADD [updated_by] VARCHAR(256) NULL;

END

GO



IF COL_LENGTH('subjective_input.investor_alias_master', 'updated_datetime') IS NULL

BEGIN

    ALTER TABLE [subjective_input].[investor_alias_master]

    ADD [updated_datetime] DATETIME2(6) NULL;

END

GO

