-- Non-KS Serviced Loans — quarterly subjective input (not in Yardi).
-- Microsoft Fabric Warehouse: non_ks_serviced_loan_key assigned by API (max + 1).
-- Fabric: no DEFAULT / IDENTITY on CREATE TABLE.
--
-- Msg 15869 "STATISTICS is not supported for SET" is from the SQL *client*
-- (Visual Studio Server Explorer, SSMS with client statistics enabled), not this script.
-- Run here in Fabric portal → Warehouse → SQL query, or disable SET STATISTICS in your client.

CREATE TABLE [mort].[non_ks_serviced_loan] (
    [non_ks_serviced_loan_key] BIGINT           NOT NULL,
    [loan_name]                VARCHAR(200)     NULL,
    [as_at_date]               DATE             NULL,
    [loan_id]                  VARCHAR(100)     NULL,
    [servicer_id]              VARCHAR(100)     NULL,
    [description]              VARCHAR(500)     NULL,
    [investor]                 VARCHAR(200)     NULL,
    [date_of_default]          DATE             NULL,
    [maturity_date]            DATE             NULL,
    [interest_off_date]        DATE             NULL,
    [tax_memo_date]            DATE             NULL,
    [security_value]           DECIMAL(18, 2)   NULL,
    [units]                    INT              NULL,
    [net_acres]                DECIMAL(18, 4)   NULL,
    [square_feet]              DECIMAL(18, 2)   NULL,
    [interest_rate]            DECIMAL(9, 4)    NULL,
    [principal_balance]        DECIMAL(18, 2)   NULL,
    [outstanding_interest]     DECIMAL(18, 2)   NULL,
    [accrued_interest]         DECIMAL(18, 2)   NULL,
    [late_interest]            DECIMAL(18, 2)   NULL,
    [outstanding_invoices]     DECIMAL(18, 2)   NULL,
    [est_realization_costs]    DECIMAL(18, 2)   NULL,
    [cost_to_complete]         DECIMAL(18, 2)   NULL,
    [tax_arrears]              DECIMAL(18, 2)   NULL,
    [interest_as_of_tax_memo]  DECIMAL(18, 2)   NULL,
    [interest_adjustment]      DECIMAL(18, 2)   NULL,
    [user_updated_by]          VARCHAR(100)     NULL,
    [user_updated_date]        DATETIME2(6)     NULL
);
GO
