-- Default Subjective Analytics (Capture Default Subjective Analytics screen)
-- Run in Fabric / wh_gold when subjective columns are missing on mort.dim_loan.

IF COL_LENGTH('mort.dim_loan', 'default_subjective_status') IS NULL
BEGIN
    ALTER TABLE mort.dim_loan ADD default_subjective_status VARCHAR(50) NULL;
END;

IF COL_LENGTH('mort.dim_loan', 'subjective_exit_plan') IS NULL
BEGIN
    ALTER TABLE mort.dim_loan ADD subjective_exit_plan VARCHAR(50) NULL;
END;

IF COL_LENGTH('mort.dim_loan', 'subjective_exit_date') IS NULL
BEGIN
    ALTER TABLE mort.dim_loan ADD subjective_exit_date VARCHAR(100) NULL;
END;

IF COL_LENGTH('mort.dim_loan', 'maturity_additional_detail') IS NULL
BEGIN
    ALTER TABLE mort.dim_loan ADD maturity_additional_detail VARCHAR(500) NULL;
END;

-- Maturity date is usually sourced from Yardi/ETL; add only if not already present.
IF COL_LENGTH('mort.dim_loan', 'maturity_date') IS NULL
BEGIN
    ALTER TABLE mort.dim_loan ADD maturity_date DATE NULL;
END;
