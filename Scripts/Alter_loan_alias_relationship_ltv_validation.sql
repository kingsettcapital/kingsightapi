-- LTV Validation: optional columns on wh_gold1.subjective_input.loan_alias_relationship.
-- Run in Fabric warehouse SQL editor, then restart the API so column probing picks up the new fields.

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'current_loan_to_value') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [current_loan_to_value] DECIMAL(18, 4) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'prior_loan_to_value') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [prior_loan_to_value] DECIMAL(18, 4) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'loan_to_value') IS NULL
   AND COL_LENGTH('subjective_input.loan_alias_relationship', 'current_loan_to_value') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [loan_to_value] DECIMAL(18, 4) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'user_update_reason') IS NULL
   AND COL_LENGTH('subjective_input.loan_alias_relationship', 'update_reason') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [user_update_reason] VARCHAR(500) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'user_update_comments') IS NULL
   AND COL_LENGTH('subjective_input.loan_alias_relationship', 'update_comment') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [user_update_comments] VARCHAR(500) NULL;
END
GO

IF COL_LENGTH('subjective_input.loan_alias_relationship', 'ai_confidence_score') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [ai_confidence_score] DECIMAL(18, 4) NULL;
END
GO

-- Confirm LTV flag consumed by reporting (Confirm button sets 'Y').
IF COL_LENGTH('subjective_input.loan_alias_relationship', 'is_confirmed') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [is_confirmed] VARCHAR(1) NULL;
END
GO
