-- Loan Alias Assignment "In Review" flag on wh_gold.subjective_input.loan_alias_relationship.
-- 'Y' = loan is under review (its loan alias shows red in the Management Summary Detailed Loan Summary),
-- 'N' or NULL = not in review.
-- Run in the Fabric warehouse SQL editor (wh_gold), then restart the API so column probing picks up the new fields.
-- Fabric does not support COL_LENGTH, so existence is checked through INFORMATION_SCHEMA.COLUMNS.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'subjective_input'
      AND TABLE_NAME = 'loan_alias_relationship'
      AND COLUMN_NAME = 'is_in_review')
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [is_in_review] VARCHAR(1) NULL;
END
GO

-- Who last changed the review flag, and when (separate from the alias audit columns).
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'subjective_input'
      AND TABLE_NAME = 'loan_alias_relationship'
      AND COLUMN_NAME = 'in_review_updated_by')
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [in_review_updated_by] VARCHAR(256) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'subjective_input'
      AND TABLE_NAME = 'loan_alias_relationship'
      AND COLUMN_NAME = 'in_review_updated_datetime')
BEGIN
    ALTER TABLE [subjective_input].[loan_alias_relationship]
    ADD [in_review_updated_datetime] DATETIME2(6) NULL;
END
GO

-- Check: should list the three new columns.
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'subjective_input'
  AND TABLE_NAME = 'loan_alias_relationship'
  AND COLUMN_NAME IN ('is_in_review', 'in_review_updated_by', 'in_review_updated_datetime');
