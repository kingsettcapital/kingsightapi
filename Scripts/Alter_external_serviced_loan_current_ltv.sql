-- Non-KS Current LTV on subjective_input.external_serviced_loan.
-- Preferred physical name: loan_to_value (also accepts current_ltv / ltv).
-- Restart the API (or wait for re-probe) after adding the column.

IF COL_LENGTH('subjective_input.external_serviced_loan', 'loan_to_value') IS NULL
   AND COL_LENGTH('subjective_input.external_serviced_loan', 'current_ltv') IS NULL
   AND COL_LENGTH('subjective_input.external_serviced_loan', 'ltv') IS NULL
   AND COL_LENGTH('subjective_input.external_serviced_loan', 'current_loan_to_value') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[external_serviced_loan]
    ADD [loan_to_value] DECIMAL(18, 4) NULL;
END
GO
