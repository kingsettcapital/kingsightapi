-- Non-KS Current LTV as-at the As At Date (subjective_input.external_serviced_loan).
-- Run in Fabric warehouse SQL editor, then restart the API so column probing picks it up.

IF COL_LENGTH('subjective_input.external_serviced_loan', 'current_ltv') IS NULL
   AND COL_LENGTH('subjective_input.external_serviced_loan', 'ltv') IS NULL
   AND COL_LENGTH('subjective_input.external_serviced_loan', 'loan_to_value') IS NULL
   AND COL_LENGTH('subjective_input.external_serviced_loan', 'current_loan_to_value') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[external_serviced_loan]
    ADD [current_ltv] DECIMAL(18, 4) NULL;
END
GO
