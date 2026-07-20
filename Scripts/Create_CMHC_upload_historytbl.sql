-- Microsoft Fabric Warehouse — matches deployed table
CREATE TABLE [mort].[CMHC_upload_historytbl] (
    [file_id]        BIGINT           NOT NULL,
    [filename]       VARCHAR(500)     NOT NULL,
    [uploaded_date]  DATETIME2(6)     NOT NULL,
    [uploaded_by]    UNIQUEIDENTIFIER NOT NULL
);
GO
