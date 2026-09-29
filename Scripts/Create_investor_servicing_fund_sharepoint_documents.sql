-- Capital portal: fund SharePoint document mapping + cache (Interim/Annual Reports).
-- Schema: investor_servicing (Fabric Warehouse — no ON [PRIMARY]).

IF OBJECT_ID(N'[investor_servicing].[fund_sharepoint_library]', N'U') IS NULL
BEGIN
    CREATE TABLE [investor_servicing].[fund_sharepoint_library](
        [fund_key] [int] NOT NULL,
        [fund_code] [varchar](50) NULL,
        [category] [varchar](200) NOT NULL,
        [sharepoint_url] [varchar](1000) NOT NULL,
        [site_url] [varchar](500) NULL,
        [library_server_relative_url] [varchar](500) NULL,
        [folder_server_relative_url] [varchar](500) NULL,
        [library_title] [varchar](200) NULL,
        [is_active] [char](1) NOT NULL,
        [notes] [varchar](500) NULL,
        [created_by] [varchar](150) NULL,
        [created_datetime] [datetime2](6) NULL,
        [updated_by] [varchar](150) NULL,
        [updated_datetime] [datetime2](6) NULL
    );
END
GO

IF OBJECT_ID(N'[investor_servicing].[fund_document]', N'U') IS NULL
BEGIN
    CREATE TABLE [investor_servicing].[fund_document](
        [fund_document_id] [bigint] NOT NULL,
        [fund_key] [int] NOT NULL,
        [sharepoint_item_id] [varchar](64) NOT NULL,
        [file_name] [varchar](500) NOT NULL,
        [category] [varchar](200) NULL,
        [quarter] [varchar](10) NULL,
        [year] [int] NULL,
        [modified_on] [datetime2](6) NULL,
        [modified_by] [varchar](200) NULL,
        [size_bytes] [bigint] NULL,
        [web_url] [varchar](1000) NULL,
        [server_relative_url] [varchar](1000) NULL,
        [synced_at] [datetime2](6) NOT NULL,
        [is_active] [char](1) NOT NULL
    );
END
GO
