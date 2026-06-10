-- Tax Arrears Capture — quarter tax memo records at leaf loan level.
-- Microsoft Fabric Warehouse: tax_arrear_key assigned by API (max + 1), not IDENTITY.

IF OBJECT_ID('mort.tax_arrears', 'U') IS NULL
BEGIN
    CREATE TABLE mort.tax_arrears (
        tax_arrear_key   BIGINT           NOT NULL,
        loan_key         BIGINT           NOT NULL,
        tax_memo_date    DATE             NULL,
        tax_arrears      DECIMAL(18, 2)   NULL,
        tax_year         VARCHAR(10)      NULL,
        notes            VARCHAR(500)     NULL,
        user_updated_by  VARCHAR(100)     NULL,
        user_updated_date DATETIME2(6)    NULL
    );
END;
