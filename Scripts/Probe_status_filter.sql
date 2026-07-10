-- Status filter diagnostic for wh_gold1 mortgage input screens.
-- Lookup: wh_gold1.shared.dim_status
-- Loan FK:  wh_gold1.shared.dim_loan.funding_status_code = dim_status.status_key

SELECT status_key, status_code, status_name, status_type, sort_order, is_active
FROM wh_gold1.shared.dim_status
WHERE ISNULL(is_active, 1) = 1
  AND ISNULL(status_type, 'FUNDING') = 'FUNDING'
ORDER BY ISNULL(sort_order, 999999), status_name;

SELECT CAST(l.funding_status_code AS varchar(20)) AS funding_status_code,
       s.status_code,
       s.status_name,
       COUNT(*) AS loan_count
FROM wh_gold1.shared.dim_loan l
LEFT JOIN wh_gold1.shared.dim_status s ON s.status_key = l.funding_status_code
WHERE CAST(l.scd_cur_ind AS varchar(10)) IN ('1', 'Y', 'y', 'true', 'TRUE')
GROUP BY l.funding_status_code, s.status_code, s.status_name
ORDER BY loan_count DESC;

SELECT
  COUNT(DISTINCT r.loan_code) AS relationship_loans,
  COUNT(DISTINCT CASE WHEN l.funding_status_code = 0 THEN r.loan_code END) AS unfunded,
  COUNT(DISTINCT CASE WHEN l.funding_status_code = 1 THEN r.loan_code END) AS funded,
  COUNT(DISTINCT CASE WHEN l.funding_status_code = 2 THEN r.loan_code END) AS default_status,
  COUNT(DISTINCT CASE WHEN l.funding_status_code = 3 THEN r.loan_code END) AS repaid
FROM wh_gold1.subjective_input.loan_alias_relationship r
INNER JOIN wh_gold1.shared.dim_loan l
  ON CAST(r.loan_code AS varchar(100)) COLLATE database_default
   = CAST(l.loan_code AS varchar(100)) COLLATE database_default
 AND CAST(l.scd_cur_ind AS varchar(10)) IN ('1', 'Y', 'y', 'true', 'TRUE');
