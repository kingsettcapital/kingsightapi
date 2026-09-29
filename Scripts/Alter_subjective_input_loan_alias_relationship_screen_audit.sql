-- Screen-specific audit columns on [subjective_input].[loan_alias_relationship].
-- Each assignment/capture screen reads and writes only its own pair.
-- Safe to re-run: adds only missing columns.
-- Run against the Fabric / warehouse database that hosts subjective_input.

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'loan_alias_updated_by') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [loan_alias_updated_by] VARCHAR(256) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'loan_alias_updated_datetime') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [loan_alias_updated_datetime] DATETIME2(6) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'loan_attribute_updated_by') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [loan_attribute_updated_by] VARCHAR(256) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'loan_attribute_updated_datetime') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [loan_attribute_updated_datetime] DATETIME2(6) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'other_cost_updated_by') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [other_cost_updated_by] VARCHAR(256) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'other_cost_updated_datetime') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [other_cost_updated_datetime] DATETIME2(6) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'default_date_updated_by') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [default_date_updated_by] VARCHAR(256) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'default_date_updated_datetime') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [default_date_updated_datetime] DATETIME2(6) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'default_si_updated_by') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [default_si_updated_by] VARCHAR(256) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'default_si_updated_datetime') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [default_si_updated_datetime] DATETIME2(6) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'ltv_updated_by') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [ltv_updated_by] VARCHAR(256) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'ltv_updated_datetime') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [ltv_updated_datetime] DATETIME2(6) NULL;
END
GO
