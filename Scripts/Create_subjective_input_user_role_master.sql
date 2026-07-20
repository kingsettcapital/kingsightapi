-- User Management tables in wh_gold1.subjective_input (snake_case columns).

IF OBJECT_ID(N'[subjective_input].[role_master]', N'U') IS NULL
BEGIN
    CREATE TABLE [subjective_input].[role_master](
        [role_id] [int] NULL,
        [role_name] [varchar](255) NULL,
        [is_active] [char](1) NULL,
        [created_datetime] [datetime2](6) NULL,
        [created_by] [varchar](100) NULL,
        [updated_datetime] [datetime2](6) NULL,
        [updated_by] [varchar](100) NULL
    ) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[subjective_input].[user_master]', N'U') IS NULL
BEGIN
    CREATE TABLE [subjective_input].[user_master](
        [user_id] [int] NULL,
        [role_id] [int] NULL,
        [email] [varchar](255) NULL,
        [first_name] [varchar](255) NULL,
        [last_name] [varchar](255) NULL,
        [is_active] [char](1) NULL,
        [created_datetime] [datetime2](6) NULL,
        [created_by] [varchar](100) NULL,
        [updated_datetime] [datetime2](6) NULL,
        [updated_by] [varchar](100) NULL
    ) ON [PRIMARY];
END
GO
