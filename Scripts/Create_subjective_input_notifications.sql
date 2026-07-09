-- subjective_input notification tables for Kingsight mortgage alerts.
-- notification_id supports mark-as-read in the SPA.

IF OBJECT_ID(N'[subjective_input].[notification_master]', N'U') IS NULL
BEGIN
    CREATE TABLE [subjective_input].[notification_master](
        [role_id] [int] NULL,
        [screen_name] [varchar](500) NULL,
        [screen_attribute] [varchar](150) NULL,
        [table_name] [varchar](150) NULL,
        [column_name] [varchar](150) NULL,
        [is_active] [int] NULL
    ) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[subjective_input].[notifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [subjective_input].[notifications](
        [notification_id] [bigint] IDENTITY(1, 1) NOT NULL,
        [notification_type] [varchar](150) NULL,
        [notice] [varchar](8000) NULL,
        [is_read] [int] NULL,
        [updated_by] [varchar](150) NULL,
        [updated_date] [datetime2](6) NULL,
        CONSTRAINT [PK_notifications] PRIMARY KEY CLUSTERED ([notification_id] ASC)
    ) ON [PRIMARY];
END
GO

-- Add notification_id when an older table exists without a primary key.
IF COL_LENGTH('subjective_input.notifications', 'notification_id') IS NULL
BEGIN
    ALTER TABLE [subjective_input].[notifications]
    ADD [notification_id] [bigint] IDENTITY(1, 1) NOT NULL;

    ALTER TABLE [subjective_input].[notifications]
    ADD CONSTRAINT [PK_notifications] PRIMARY KEY CLUSTERED ([notification_id] ASC);
END
GO
