-- Seed capital SharePoint library URLs for Interim/Annual Reports.
-- Confirmed browser links from ops (2026-09-09).
-- Note: CTEs only scope to the immediate next statement in Fabric/T-SQL.

DECLARE @category varchar(200) = 'Interim/Annual Reports';
DECLARE @now datetime2(6) = SYSUTCDATETIME();

-- 1) Update existing mappings
;WITH seed AS (
    SELECT *
    FROM (VALUES
        (N'Growth LPs', N'https://kingsettcapital.sharepoint.com/Reporting/Growth%20LPs/Forms/AllItems.aspx', N'Growth LPs'),
        (N'UIF2', N'https://kingsettcapital.sharepoint.com/Reporting/UIF%202/Forms/By%20Year.aspx', N'UIF 2'),
        (N'UIF', N'https://kingsettcapital.sharepoint.com/Reporting/Urban%20Infill%20Reporting/Forms/By%20Year%20View.aspx', N'UIF (Urban Infill Reporting)'),
        (N'SMF', N'https://kingsettcapital.sharepoint.com/Reporting/SMF/Forms/By%20Year%20View.aspx', N'SMF'),
        (N'HYF', N'https://kingsettcapital.sharepoint.com/Reporting/HYF%20Investor%20Reporting/Forms/AllItems.aspx', N'HYF Investor Reporting'),
        (N'CREIF', N'https://kingsettcapital.sharepoint.com/Reporting/CREIFInvestorCommunications/Forms/AllItems.aspx', N'CREIF Investor Communications')
    ) AS v(fund_code_hint, sharepoint_url, notes)
),
resolved AS (
    SELECT
        f.fund_key,
        f.fund_code,
        s.sharepoint_url,
        s.notes,
        ROW_NUMBER() OVER (
            PARTITION BY s.fund_code_hint
            ORDER BY f.fund_key
        ) AS rn
    FROM seed s
    INNER JOIN [shared].[dim_fund] f
        ON (
            f.fund_code = s.fund_code_hint
            OR (s.fund_code_hint = N'UIF2' AND f.fund_code IN (N'UIF2', N'UIF 2'))
            OR (s.fund_code_hint = N'Growth LPs' AND (
                f.fund_code IN (N'Growth LPs', N'GROWTHLPS', N'GLP')
                OR f.fund_name LIKE N'%Growth%LP%'
            ))
            OR (s.fund_code_hint = N'UIF' AND f.fund_code IN (N'UIF', N'Urban Infill')
                AND f.fund_code NOT IN (N'UIF2', N'UIF 2'))
            OR (s.fund_code_hint = N'HYF' AND (
                f.fund_code IN (N'HYF', N'HYF Investor Reporting')
                OR f.fund_name LIKE N'HYF%'
            ))
            OR (s.fund_code_hint = N'CREIF' AND (
                f.fund_code IN (N'CREIF', N'CREIFInvestorCommunications')
                OR f.fund_name LIKE N'CREIF%'
            ))
            OR (s.fund_code_hint = N'SMF' AND (
                f.fund_code = N'SMF'
                OR f.fund_name LIKE N'SMF%'
            ))
        )
    WHERE ISNULL(f.is_current, 1) = 1
       OR (
            f.is_current IS NULL
            AND GETDATE() BETWEEN f.valid_from AND ISNULL(f.valid_to, CAST('9999-12-31' AS datetime2))
          )
)
UPDATE t
SET
    fund_code = r.fund_code,
    sharepoint_url = r.sharepoint_url,
    site_url = NULL,
    library_server_relative_url = NULL,
    folder_server_relative_url = NULL,
    library_title = NULL,
    is_active = 'Y',
    notes = r.notes,
    updated_by = N'seed',
    updated_datetime = @now
FROM [investor_servicing].[fund_sharepoint_library] t
INNER JOIN resolved r
    ON t.fund_key = r.fund_key
   AND t.category = @category
WHERE r.rn = 1;

-- 2) Insert missing mappings (own CTE — previous CTE is out of scope)
;WITH seed AS (
    SELECT *
    FROM (VALUES
        (N'Growth LPs', N'https://kingsettcapital.sharepoint.com/Reporting/Growth%20LPs/Forms/AllItems.aspx', N'Growth LPs'),
        (N'UIF2', N'https://kingsettcapital.sharepoint.com/Reporting/UIF%202/Forms/By%20Year.aspx', N'UIF 2'),
        (N'UIF', N'https://kingsettcapital.sharepoint.com/Reporting/Urban%20Infill%20Reporting/Forms/By%20Year%20View.aspx', N'UIF (Urban Infill Reporting)'),
        (N'SMF', N'https://kingsettcapital.sharepoint.com/Reporting/SMF/Forms/By%20Year%20View.aspx', N'SMF'),
        (N'HYF', N'https://kingsettcapital.sharepoint.com/Reporting/HYF%20Investor%20Reporting/Forms/AllItems.aspx', N'HYF Investor Reporting'),
        (N'CREIF', N'https://kingsettcapital.sharepoint.com/Reporting/CREIFInvestorCommunications/Forms/AllItems.aspx', N'CREIF Investor Communications')
    ) AS v(fund_code_hint, sharepoint_url, notes)
),
resolved AS (
    SELECT
        f.fund_key,
        f.fund_code,
        s.sharepoint_url,
        s.notes,
        ROW_NUMBER() OVER (
            PARTITION BY s.fund_code_hint
            ORDER BY f.fund_key
        ) AS rn
    FROM seed s
    INNER JOIN [shared].[dim_fund] f
        ON (
            f.fund_code = s.fund_code_hint
            OR (s.fund_code_hint = N'UIF2' AND f.fund_code IN (N'UIF2', N'UIF 2'))
            OR (s.fund_code_hint = N'Growth LPs' AND (
                f.fund_code IN (N'Growth LPs', N'GROWTHLPS', N'GLP')
                OR f.fund_name LIKE N'%Growth%LP%'
            ))
            OR (s.fund_code_hint = N'UIF' AND f.fund_code IN (N'UIF', N'Urban Infill')
                AND f.fund_code NOT IN (N'UIF2', N'UIF 2'))
            OR (s.fund_code_hint = N'HYF' AND (
                f.fund_code IN (N'HYF', N'HYF Investor Reporting')
                OR f.fund_name LIKE N'HYF%'
            ))
            OR (s.fund_code_hint = N'CREIF' AND (
                f.fund_code IN (N'CREIF', N'CREIFInvestorCommunications')
                OR f.fund_name LIKE N'CREIF%'
            ))
            OR (s.fund_code_hint = N'SMF' AND (
                f.fund_code = N'SMF'
                OR f.fund_name LIKE N'SMF%'
            ))
        )
    WHERE ISNULL(f.is_current, 1) = 1
       OR (
            f.is_current IS NULL
            AND GETDATE() BETWEEN f.valid_from AND ISNULL(f.valid_to, CAST('9999-12-31' AS datetime2))
          )
)
INSERT INTO [investor_servicing].[fund_sharepoint_library] (
    fund_key, fund_code, category, sharepoint_url,
    site_url, library_server_relative_url, folder_server_relative_url, library_title,
    is_active, notes, created_by, created_datetime, updated_by, updated_datetime
)
SELECT
    r.fund_key,
    r.fund_code,
    @category,
    r.sharepoint_url,
    NULL, NULL, NULL, NULL,
    'Y',
    r.notes,
    N'seed',
    @now,
    N'seed',
    @now
FROM resolved r
WHERE r.rn = 1
  AND NOT EXISTS (
        SELECT 1
        FROM [investor_servicing].[fund_sharepoint_library] t
        WHERE t.fund_key = r.fund_key
          AND t.category = @category
    );

-- 3) Sanity check
SELECT
    fund_key,
    fund_code,
    notes,
    sharepoint_url
FROM [investor_servicing].[fund_sharepoint_library]
WHERE category = @category
  AND upper(isnull(is_active, 'Y')) IN ('Y', '1', 'T')
ORDER BY fund_code;
GO
