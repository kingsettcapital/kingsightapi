-- CMHC Dashboard shared report memory (funding-date history, pool baselines, etc.).
-- Run against DEALPATH on yellowknife.
USE DEALPATH;
GO

IF OBJECT_ID(N'dbo.ReportStateTbl', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportStateTbl (
        StateKey   varchar(64)       NOT NULL
            CONSTRAINT PK_ReportStateTbl PRIMARY KEY,
        JsonValue  nvarchar(max)     NOT NULL
            CONSTRAINT CK_ReportStateTbl_Json CHECK (ISJSON(JsonValue) = 1),
        Version    bigint            NOT NULL
            CONSTRAINT DF_ReportStateTbl_Version DEFAULT (1),
        UpdatedAt  datetimeoffset(0) NOT NULL
            CONSTRAINT DF_ReportStateTbl_UpdatedAt DEFAULT (SYSDATETIMEOFFSET()),
        UpdatedBy  nvarchar(256)     NULL
    );
END
GO

-- Grant the API's service identity access (replace the placeholder):
-- GRANT SELECT, INSERT, UPDATE ON dbo.ReportStateTbl TO [<api identity>];
-- It also needs SELECT on dbo.DealTbl and dbo.MasterDealTbl.
