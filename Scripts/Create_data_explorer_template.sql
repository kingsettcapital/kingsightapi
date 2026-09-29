-- Data Explorer saved templates (Microsoft Fabric Warehouse).
-- IDENTITY: BIGINT only (not INT). No PRIMARY KEY / OUTPUT clause in CREATE TABLE or INSERT.

CREATE TABLE dbo.data_explorer_template
(
    template_id     BIGINT IDENTITY,
    template_name   VARCHAR(200)     NOT NULL,
    description     VARCHAR(1000)    NULL,
    source_view     VARCHAR(256)     NOT NULL,
    match_type      VARCHAR(3)       NOT NULL,
    group_by_field  VARCHAR(256)     NULL,
    created_by      VARCHAR(256)     NULL,
    created_at      DATETIME2(3)     NOT NULL,
    modified_by     VARCHAR(256)     NULL,
    modified_at     DATETIME2(3)     NULL,
    is_active       BIT              NOT NULL
);
GO

CREATE TABLE dbo.data_explorer_template_column
(
    template_id    BIGINT           NOT NULL,
    column_name    VARCHAR(256)     NOT NULL,
    display_order  INT              NOT NULL
);
GO

CREATE TABLE dbo.data_explorer_template_filter
(
    filter_id      BIGINT IDENTITY,
    template_id    BIGINT           NOT NULL,
    column_name    VARCHAR(256)     NOT NULL,
    [operator]     VARCHAR(20)      NOT NULL,
    filter_value   VARCHAR(4000)    NULL,
    filter_order   INT              NOT NULL
);
GO
