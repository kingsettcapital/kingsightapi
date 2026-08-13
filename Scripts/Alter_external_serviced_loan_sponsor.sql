-- Non-KS Sponsor on subjective_input.external_serviced_loan.
-- Run in Fabric warehouse SQL editor, then restart the API so column probing picks it up.

IF COL_LENGTH('subjective_input.external_serviced_loan', 'sponsor') IS NULL
   AND COL_LENGTH('subjective_input.external_serviced_loan', 'sponsor_name') IS NULL
   AND COL_LENGTH('subjective_input.external_serviced_loan', 'borrower_name') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[external_serviced_loan]
    ADD [sponsor] VARCHAR(200) NULL;
END
GO
