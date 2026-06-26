-- LTV Validation: optional columns on wh_gold1.subjective_input.loan_alias_relationship.
-- Run in Fabric warehouse SQL editor, then restart the API so column probing picks up the new fields.

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'update_reason') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [update_reason] VARCHAR(500) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'update_comment') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [update_comment] VARCHAR(500) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'ai_confidence_score') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [ai_confidence_score] DECIMAL(5, 4) NULL;
END
GO
