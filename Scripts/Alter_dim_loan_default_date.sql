-- Default Date Capture: persist user-entered default date on leaf, current loans.
-- Run in Fabric / wh_gold when mort.dim_loan.default_date does not exist.

IF COL_LENGTH('mort.dim_loan', 'default_date') IS NULL
BEGIN
    ALTER TABLE mort.dim_loan ADD default_date DATE NULL;
END;

-- Optional: populate read-only loan term default date from ETL / view when available.
-- IF COL_LENGTH('mort.dim_loan', 'loan_term_default_date') IS NULL
-- BEGIN
--     ALTER TABLE mort.dim_loan ADD loan_term_default_date DATE NULL;
-- END;
