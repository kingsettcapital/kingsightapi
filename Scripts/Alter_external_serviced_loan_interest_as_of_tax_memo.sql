-- Non-KS Serviced Loans: "Int. as of Memo" column for wh_gold1.subjective_input.external_serviced_loan.
-- Run in Fabric warehouse SQL editor, then restart the API so column probing picks up the new field.

IF COL_LENGTH('subjective_input.external_serviced_loan', 'interest_as_of_tax_memo') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[external_serviced_loan]
    ADD [interest_as_of_tax_memo] DECIMAL(18, 2) NULL;
END
GO
