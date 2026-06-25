-- Audit columns for loan alias assignment updates on loan_alias_relationship.
IF COL_LENGTH('subjective_input.loan_alias_relationship', 'updated_by') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [updated_by] VARCHAR(256) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'updated_datetime') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [updated_datetime] DATETIME2(6) NULL;
END
GO
