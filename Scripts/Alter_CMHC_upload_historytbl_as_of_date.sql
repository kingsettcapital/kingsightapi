-- Add document as-of date for CMHC / QR slide uploads (reporting period).
IF COL_LENGTH('mort.CMHC_upload_historytbl', 'as_of_date') IS NULL
BEGIN
    ALTER TABLE [mort].[CMHC_upload_historytbl]
    ADD [as_of_date] DATE NULL;
END
GO
