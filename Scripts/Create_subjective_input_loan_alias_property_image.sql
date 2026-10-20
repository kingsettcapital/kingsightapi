-- subjective_input.loan_alias_property_image: property photo per loan alias (Loan Detail report).
-- Files live in OneLake under Files/external_files/property_images/{loan_alias_id}/.
-- One active row per loan_alias_id; re-upload sets the previous row is_active = 0.
IF OBJECT_ID(N'[subjective_input].[loan_alias_property_image]', N'U') IS NULL
BEGIN
    CREATE TABLE [subjective_input].[loan_alias_property_image](
        [property_image_id] [bigint] NOT NULL,
        [loan_alias_id] [bigint] NOT NULL,
        [file_name] [varchar](500) NOT NULL,
        [original_file_name] [varchar](500) NOT NULL,
        [content_type] [varchar](100) NOT NULL,
        [file_size_bytes] [bigint] NOT NULL,
        [is_active] [bit] NOT NULL,
        [created_by] [varchar](256) NULL,
        [created_datetime] [datetime2](6) NULL,
        [updated_by] [varchar](256) NULL,
        [updated_datetime] [datetime2](6) NULL
    );
END
GO
