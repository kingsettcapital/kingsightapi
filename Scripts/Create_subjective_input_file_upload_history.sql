-- subjective_input.file_upload_history for CMHC / QR slide uploads.
IF OBJECT_ID(N'[subjective_input].[file_upload_history]', N'U') IS NULL
BEGIN
    CREATE TABLE [subjective_input].[file_upload_history](
        [file_id] [bigint] NOT NULL,
        [filename] [varchar](500) NOT NULL,
        [file_type] [varchar](100) NOT NULL,
        [as_of_date] [date] NOT NULL,
        [uploaded_date] [datetime2](6) NOT NULL,
        [uploaded_by] [uniqueidentifier] NOT NULL
    ) ON [PRIMARY];
END
GO
