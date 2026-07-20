-- LTV Validation — AI-extracted LTV review at leaf loan level.
-- Microsoft Fabric Warehouse: one row per loan_key.
-- Fabric: no DEFAULT on CREATE TABLE — API sets bit flags explicitly on INSERT.

IF OBJECT_ID('mort.ltv_validation', 'U') IS NULL
BEGIN
    CREATE TABLE mort.ltv_validation (
        loan_key            BIGINT           NOT NULL,
        ai_ltv              DECIMAL(5, 2)    NULL,
        ltv                 DECIMAL(5, 2)    NULL,
        ai_commentary       VARCHAR(500)     NULL,
        is_ai_confirmed     BIT              NULL,
        is_user_overridden  BIT              NULL,
        user_updated_by     VARCHAR(100)     NULL,
        user_updated_date   DATETIME2(6)     NULL
    );
END;
