# Kingsight Portal — SQL Reference by Endpoint

This document maps each **Funds** and **Capital Investors** API screen to the warehouse SQL executed by the API. SQL is shown in readable form (expanded fragments, no `StringBuilder` noise).

**Source of truth:** `FundPortalService.cs`, `InvestorPortalService.cs`, `InvestorPortalService.Metrics.cs`, `WarehouseSql.cs`, `WarehouseTables.cs`.

---

## 1. Shared concepts

### 1.1 Warehouse tables

| Alias | Table |
|-------|-------|
| `dim_date` | `dbo.dim_date` |
| `dim_fund` | `dbo.dim_fund` |
| `dim_investor` | `dbo.dim_investor` |
| `dim_property` | `dbo.dim_property` |
| `dim_transaction_type` | `dbo.dim_transaction_type` |
| `fact_commitment` | `dbo.fact_commitment` |
| `fact_investment` | `dbo.fact_investment` |
| `fact_distribution` | `dbo.fact_distribution` |
| `fact_investor_portfolio_ltd` | `dbo.fact_investor_portfolio_ltd` |
| `fact_investor_portfolio_quarterly` | `dbo.fact_investor_portfolio_quarterly` |
| `fact_fund_nav` | `dbo.fact_fund_nav` |

### 1.2 Common query parameters

| Parameter | Used on | Meaning |
|-----------|---------|---------|
| `@search` | List endpoints | `NULL` = no filter; else `LIKE '%search%'` on name |
| `@page` / `@pageSize` | All paged endpoints | Normalized server-side; SQL uses `@offset` + `FETCH NEXT` |
| `@fundKey` | Fund-scoped routes | Route `{fundKey}` |
| `@investorKey` | Investor-scoped routes | Route `{investorKey}` |
| `view` | Metric + period routes | `ltd` \| `quarterly` \| `daily` |
| `source` | Period dropdown only | `commitments` \| `nav` \| `unfunded-commitments` \| `investments` \| `distributions` |
| `@dateKey` | Quarterly/daily metrics | Optional; `yyyyMMdd` from period dropdown. Omitted = all periods in view |

### 1.3 Reusable filter fragments

**Current fund (SCD):**
```sql
(
  ISNULL(f.is_current, 1) = 1
  OR (
    f.is_current IS NULL
    AND GETDATE() BETWEEN f.valid_from
      AND ISNULL(f.valid_to, CAST('9999-12-31' AS datetime2))
  )
)
```

**Current investor:**
```sql
ISNULL(i.is_current, 1) = 1
```

**Current property:**
```sql
ISNULL(p.is_current, 1) = 1
```

**Fund name search (`@search`):**
```sql
AND (
  @search IS NULL
  OR LOWER(ISNULL(f.fund_name, '')) LIKE '%' + LOWER(@search) + '%'
)
```

**Investor name search (`@search`):**
```sql
AND (
  @search IS NULL
  OR LOWER(ISNULL(i.investor_name, '')) LIKE '%' + LOWER(@search) + '%'
)
```

**Property belongs to fund (text match on `dim_property.fund`):**
```sql
AND ISNULL(p.fund, '') <> ''
AND (
  ISNULL(p.fund, '') = ISNULL(f.fund_code, '')
  OR ISNULL(p.fund, '') = ISNULL(f.fund_name, '')
  OR ISNULL(p.fund, '') = ISNULL(f.js_fund_name, '')
)
```

**Property fund level (assets only):**
```sql
AND ISNULL(p.fund_level, '') IN ('000 Property', '000 - Property')
```

**Quarterly period filter (when `@dateKey` provided):**
```sql
AND quarter_year = (
  SELECT quarter_year FROM dbo.dim_date WHERE date_key = @dateKey
)
```
*Or via `dim_date` alias `d`:*
```sql
AND d.quarter_year = (
  SELECT quarter_year FROM dbo.dim_date WHERE date_key = @dateKey
)
```

**Daily period filter (when `@dateKey` provided):**
```sql
AND <fact>.posted_date_key = @dateKey   -- commitments, investments, distributions
AND n.date_key = @dateKey               -- NAV
```

**Fund code column (investor metrics — from join):**
```sql
ISNULL(df.fund_code, '') AS fund_code
```

**Fund code scalar (fund metrics — single fund):**
```sql
fund_code = (
  SELECT TOP 1 ISNULL(fund_code, '')
  FROM dbo.dim_fund f
  WHERE f.fund_key = @fundKey
    AND <current fund filter on f>
)
```

**Distribution totals HAVING:**
```sql
HAVING SUM(ISNULL(fd.distributed_amount, 0)) != 0
    OR SUM(ISNULL(fd.distributed_units, 0)) != 0
```

**Transaction type join:**
```sql
INNER JOIN dbo.dim_transaction_type tt
  ON tt.transaction_type_key = fd.transaction_type_key
 AND ISNULL(tt.is_current, 1) = 1
```

**Investor NAV fund scope:**
```sql
WHERE n.fund_key IN (
  SELECT fund_key FROM dbo.fact_commitment WHERE investor_key = @investorKey
  UNION
  SELECT fund_key FROM dbo.fact_investment WHERE investor_key = @investorKey
)
```

---

## 2. Funds portal (`FundsController`)

### 2.1 Fund list — `GET /api/funds`

**Screen:** Investments sidebar (fund list)  
**Filters:** `@search` (fund name), pagination

**Count:**
```sql
SELECT COUNT(*)
FROM (
  SELECT b.fund_name
  FROM dbo.fact_investor_portfolio_ltd a
  INNER JOIN dbo.dim_fund b ON a.fund_key = b.fund_key
  WHERE b.is_current = 1
    AND <fund name search>
  GROUP BY b.fund_name
) fund_rows;
```

**Page:**
```sql
SELECT
  MIN(b.fund_key) AS fund_key,
  b.fund_name,
  MAX(ISNULL(b.fund_strategy_name, ISNULL(b.fund_type_name, ''))) AS category,
  SUM(ISNULL(a.net_invested_capital_amount, 0)) AS net_invested_capital_amount
FROM dbo.fact_investor_portfolio_ltd a
INNER JOIN dbo.dim_fund b ON a.fund_key = b.fund_key
WHERE b.is_current = 1
  AND <fund name search>
GROUP BY b.fund_name
ORDER BY b.fund_name
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
```

---

### 2.2 Fund detail — `GET /api/funds/{fundKey}`

**Screen:** Fund profile / overview  
**Filters:** `@fundKey`, current fund SCD

**Summary query:**
```sql
SELECT
  f.fund_key,
  f.fund_id,
  ISNULL(f.fund_code, '') AS fund_code,
  f.fund_name,
  ISNULL(f.fund_type_name, 'Fund') AS fund_type_name,
  CASE WHEN ISNULL(f.is_active, 0) = 1 THEN 'Active' ELSE 'Inactive' END AS fund_status,
  ISNULL(port.commitment, 0) AS commitment,
  ISNULL(port.called, 0) AS called,
  ISNULL(port.netinvestedamount, 0) AS netinvestedamount,
  ISNULL(port.netinvestedunits, 0) AS netinvestedunits,
  ISNULL(port.reserveamount, 0) AS reserveamount,
  ISNULL(assets.assets_count, 0) AS assets_count,
  ISNULL(inv.investors_count, 0) AS investors_count
FROM dbo.dim_fund f
OUTER APPLY (
  SELECT
    commitment = SUM(commitment_amount),
    called = SUM(capital_called_amount),
    netinvestedamount = SUM(net_invested_capital_amount),
    netinvestedunits = SUM(net_invested_capital_units),
    reserveamount = SUM(reserved_amount)
  FROM dbo.fact_investor_portfolio_ltd
  WHERE fund_key = f.fund_key
) port
OUTER APPLY (
  SELECT COUNT(*) AS assets_count
  FROM dbo.dim_property p
  WHERE <current property>
    AND <property belongs to fund f>
    AND <fund level 000 Property>
) assets
OUTER APPLY (
  SELECT COUNT(*) AS investors_count
  FROM (
    SELECT DISTINCT investor_key FROM dbo.fact_commitment WHERE fund_key = f.fund_key
    UNION
    SELECT DISTINCT investor_key FROM dbo.fact_investment WHERE fund_key = f.fund_key
  ) invkeys
) inv
WHERE f.fund_key = @fundKey
  AND <current fund filter on f>;
```

**Investment details section (second query):**
```sql
SELECT
  ISNULL(f.fund_type_name, 'Fund') AS fund_type_name,
  ISNULL(f.fund_strategy_name, '') AS fund_strategy_name,
  CASE WHEN ISNULL(f.is_active, 0) = 1 THEN 'Active' ELSE 'Inactive' END AS fund_status,
  f.fund_start_date,
  ISNULL(f.is_sidecar, 0) AS is_sidecar
FROM dbo.dim_fund f
WHERE f.fund_key = @fundKey
  AND <current fund filter on f>;
```

---

### 2.3 Fund assets — `GET /api/funds/{fundKey}/assets`

**Screen:** Fund detail → Assets tab  
**Filters:** `@fundKey`, current fund/property, property↔fund match, fund level 000

**Count & page (same WHERE):**
```sql
SELECT
  p.property_key,
  ISNULL(p.property_name, '') AS property_name,
  ISNULL(p.city, '') AS city,
  ISNULL(p.province, '') AS province,
  ISNULL(p.geography, '') AS geography,
  ISNULL(p.asset_type, '') AS asset_type,
  ISNULL(p.investment_type, '') AS investment_type,
  ISNULL(p.property_status, '') AS property_status,
  p.property_acquisition,
  p.property_disposition
FROM dbo.dim_property p
INNER JOIN dbo.dim_fund f ON f.fund_key = @fundKey
  AND <current fund filter on f>
WHERE <current property on p>
  AND <property belongs to fund f>
  AND <fund level 000 Property>
ORDER BY p.property_name
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
```

---

### 2.4 Fund investors — `GET /api/funds/{fundKey}/investors`

**Screen:** Fund detail → Investors tab  
**Filters:** `@fundKey`, `@search` (investor name), current investor

**Investor list:**
```sql
SELECT
  i.investor_key,
  i.investor_name,
  ISNULL(i.relationship_name, '') AS relationship_name,
  ISNULL(i.investor_type_name, '') AS investor_type_name,
  ISNULL(i.contact_first_name, '') AS contact_first_name,
  ISNULL(i.contact_last_name, '') AS contact_last_name,
  CASE WHEN ISNULL(i.is_current, 1) = 1 THEN 'Active' ELSE 'Inactive' END AS investor_status,
  i.valid_from AS member_since,
  YEAR(i.valid_from) AS join_year
FROM (
  SELECT DISTINCT investor_key FROM dbo.fact_commitment WHERE fund_key = @fundKey
  UNION
  SELECT DISTINCT investor_key FROM dbo.fact_investment WHERE fund_key = @fundKey
) x
INNER JOIN dbo.dim_investor i ON i.investor_key = x.investor_key
  AND <current investor on i>
  AND <investor name search>
INNER JOIN dbo.dim_fund df ON df.fund_key = @fundKey
  AND <current fund filter on df>
ORDER BY i.investor_name
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
```

**Per-page totals (committed + FMV):** second query aggregates by `investor_key` for keys on the current page.

---

### 2.5 Period dropdown — `GET /api/funds/{fundKey}/periods`

**Screen:** Period selector on metric tabs  
**Required:** `view`, `source`  
**Filters:** `@fundKey`; `view=ltd` returns a single disabled “All Periods” row (no SQL)

#### `view=quarterly` — by `source`

| source | Fact table | Extra filter |
|--------|------------|--------------|
| commitments, unfunded-commitments | `fact_investor_portfolio_quarterly` | `q.fund_key = @fundKey` |
| investments | `fact_investment` | `fi.fund_key = @fundKey`, `HAVING sum(invested_amount) != 0` |
| distributions | `fact_distribution` + `dim_fund` | `fd.fund_key = @fundKey`, distribution HAVING |
| nav | `fact_fund_nav` | `n.fund_key = @fundKey`, `nav != 0` |

**Template:**
```sql
SELECT
  d.quarter_year,
  d.calendar_year,
  MIN(d.date_key) AS min_date_key,
  MAX(d.date_key) AS max_date_key,
  MIN(d.first_date_of_quater) AS period_start,
  MAX(d.last_date_of_quater) AS period_end,
  MAX(d.month_year) AS month_year
FROM <fact> ...
INNER JOIN dbo.dim_date d ON ...
WHERE <fund_key = @fundKey>
GROUP BY d.quarter_year, d.calendar_year
-- + source-specific HAVING
ORDER BY calendar_year, quarter_year
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
```

#### `view=daily` — by `source`

| source | Fact | Daily filter |
|--------|------|--------------|
| commitments | `fact_commitment` | `HAVING sum(committed_amount) != 0` |
| unfunded-commitments | `fact_commitment` | `HAVING sum(committed_amount_fmv) != 0` |
| investments | `fact_investment` | `HAVING sum(invested_amount) != 0` |
| distributions | `fact_distribution` | distribution HAVING |
| nav | `fact_fund_nav` | `nav != 0` |

Grouped by `posted_date_key` (or `date_key` for NAV) + date attributes.

---

### 2.6 Fund commitments — `GET /api/funds/{fundKey}/commitments`

**Required:** `view`  
**Optional:** `dateKey` (quarterly/daily only)

| view | Fact | Group by | Amount column |
|------|------|----------|---------------|
| **ltd** | `fact_investor_portfolio_ltd` | (single row) | `sum(commitment_amount)` |
| **quarterly** | `fact_investor_portfolio_quarterly` | `quarter_year` | `sum(commitment_amount)` |
| **daily** | `fact_commitment` | `posted_date_key` | `sum(committed_amount)` |

**LTD page:**
```sql
SELECT
  <fund_code scalar>,
  Period = 'Life To Date',
  commitment_amount = SUM(commitment_amount),
  Description = 'Total Commitment as of Date'
FROM dbo.fact_investor_portfolio_ltd
WHERE fund_key = @fundKey;
```

**Quarterly page (+ optional quarter filter):**
```sql
SELECT
  <fund_code scalar>,
  Period = quarter_year,
  commitment_amount = SUM(commitment_amount)
FROM dbo.fact_investor_portfolio_quarterly
WHERE fund_key = @fundKey
  AND <quarter filter if @dateKey>
GROUP BY quarter_year
ORDER BY quarter_year;
```

**Daily page (+ optional `@dateKey`):**
```sql
SELECT
  <fund_code scalar>,
  fc.posted_date_key,
  TRY_CONVERT(date, CAST(fc.posted_date_key AS varchar(8)), 112) AS full_date,
  commitment_amount = SUM(fc.committed_amount)
FROM dbo.fact_commitment fc
WHERE fc.fund_key = @fundKey
  AND <posted_date_key = @dateKey if provided>
GROUP BY fc.fund_key, fc.posted_date_key
HAVING SUM(fc.committed_amount) != 0
ORDER BY fc.posted_date_key;
```

---

### 2.7 Fund unfunded commitments — `GET /api/funds/{fundKey}/unfunded-commitments`

Same `view` / `dateKey` pattern as commitments.

| view | Fact | Amount |
|------|------|--------|
| **ltd** | `fact_investor_portfolio_ltd` | `sum(unfunded_amount)` |
| **quarterly** | `fact_investor_portfolio_quarterly` | `sum(unfunded_amount)` |
| **daily** | `fact_commitment` | `sum(committed_amount_fmv)` |

---

### 2.8 Fund investments — `GET /api/funds/{fundKey}/investments`

| view | Fact | Amount |
|------|------|--------|
| **ltd** | `fact_investment` | `sum(invested_amount)` |
| **quarterly** | `fact_investment` + `dim_date` | `sum(invested_amount)` by `d.quarter_year` |
| **daily** | `fact_investment` | `sum(invested_amount)` by `posted_date_key` |

Includes `<fund_code scalar>` on page queries.

---

### 2.9 Fund distributions — `GET /api/funds/{fundKey}/distributions`

**API response:** grouped by `transaction_type` (pagination on groups, not flat rows).

| view | Group by | Period column |
|------|----------|---------------|
| **ltd** | `transaction_type` | `Period = 'ITD'` |
| **quarterly** | `transaction_type`, `d.quarter_year` | `Period = d.quarter_year` |
| **daily** | `transaction_type`, `posted_date_key` | date from `posted_date_key` |

**LTD page SQL:**
```sql
SELECT
  <fund_code scalar>,
  transaction_type = ISNULL(tt.transaction_type_name, ''),
  Period = 'ITD',
  MAX(fd.posted_date_key) AS posted_date_key,
  TRY_CONVERT(date, CAST(MAX(fd.posted_date_key) AS varchar(8)), 112) AS full_date,
  units = SUM(ISNULL(fd.distributed_units, 0)),
  amount = SUM(ISNULL(fd.distributed_amount, 0))
FROM dbo.fact_distribution fd
INNER JOIN dbo.dim_transaction_type tt ON ...
WHERE fd.fund_key = @fundKey
GROUP BY tt.transaction_type_name
HAVING <distribution totals>
ORDER BY tt.transaction_type_name;
```

---

### 2.10 Fund NAV — `GET /api/funds/{fundKey}/nav`

| view | Fact | Amount |
|------|------|--------|
| **ltd** | `fact_fund_nav` | `sum(nav)` — single row |
| **quarterly** | `fact_fund_nav` + `dim_date` | `sum(nav)` by `d.quarter_year` |
| **daily** | `fact_fund_nav` | `n.nav` per `date_key` |

---

## 3. Capital Investors portal (`CapitalInvestorsController`)

### 3.1 Investor list — `GET /api/CapitalInvestors`

**Screen:** Investor sidebar  
**Filters:** `@search` (investor name), pagination

**Count:**
```sql
SELECT COUNT(*)
FROM (
  SELECT b.investor_name
  FROM dbo.fact_investor_portfolio_ltd a
  INNER JOIN dbo.dim_investor b ON a.investor_key = b.investor_key
  WHERE b.is_current = 1
    AND <investor name search>
  GROUP BY b.investor_name
) investor_rows;
```

**Page:**
```sql
SELECT
  MIN(b.investor_key) AS investor_key,
  b.investor_name,
  MAX(ISNULL(b.investor_type_name, '')) AS investor_type_name,
  SUM(ISNULL(a.net_invested_capital_amount, 0)) AS total_invested
FROM dbo.fact_investor_portfolio_ltd a
INNER JOIN dbo.dim_investor b ON a.investor_key = b.investor_key
WHERE b.is_current = 1
  AND <investor name search>
GROUP BY b.investor_name
ORDER BY b.investor_name
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
```

---

### 3.2 Investor detail — `GET /api/CapitalInvestors/{investorKey}`

**Screen:** Investor profile / overview  
**Filters:** `@investorKey`, current investor

**Profile query:**
```sql
SELECT
  i.investor_key, i.investor_id, i.investor_name,
  ISNULL(i.investor_short_name, '') AS investor_short_name,
  ISNULL(i.relationship_name, '') AS relationship_name,
  ISNULL(i.investor_type_name, '') AS investor_type_name,
  CASE WHEN ISNULL(i.is_current, 1) = 1 THEN 'Active' ELSE 'Inactive' END AS investor_status,
  i.address_line1, i.address_line2, i.city, i.province, i.country,
  i.contact_first_name, i.contact_last_name, i.contact_email,
  i.valid_from AS member_since
FROM dbo.dim_investor i
WHERE i.investor_key = @investorKey
  AND <current investor on i>;
```

**Portfolio aggregates (second query):**
```sql
SELECT
  ISNULL((SELECT SUM(ISNULL(fc.committed_amount, 0))
          FROM dbo.fact_commitment fc
          INNER JOIN dbo.dim_fund df ON df.fund_key = fc.fund_key AND <current fund>
          WHERE fc.investor_key = @investorKey), 0) AS total_committed_value,

  ISNULL((SELECT SUM(ISNULL(p.net_invested_capital_amount, 0))
          FROM dbo.fact_investor_portfolio_ltd p
          INNER JOIN dbo.dim_investor i2 ON i2.investor_key = p.investor_key
          WHERE i2.investor_key = @investorKey AND i2.is_current = 1), 0) AS total_invested_value,

  -- investments_count, active_investments_count, first_investment_date ...
;
```

`summary.totalInvested` uses **LTD** `net_invested_capital_amount` (aligned with list).

---

### 3.3 Investor funds — `GET /api/CapitalInvestors/{investorKey}/funds`

**Screen:** Investor detail → Funds / investments tab  
**Filters:** `@investorKey`, pagination  
**Grouping:** `df.fund_code` (one row per fund code)

**Fund list:**
```sql
SELECT
  MIN(df.fund_key) AS fund_key,
  ISNULL(df.fund_code, '') AS fund_code,
  MAX(ISNULL(df.fund_name, '')) AS fund_name,
  MAX(ISNULL(df.fund_type_name, '')) AS fund_type,
  MAX(ISNULL(df.fund_strategy_name, ISNULL(df.fund_type_name, ''))) AS fund_category,
  CASE
    WHEN MAX(CASE WHEN df.dissolution_date IS NOT NULL THEN 1 ELSE 0 END) = 1 THEN 'Dissolved'
    WHEN MAX(CASE WHEN ISNULL(df.is_current, 1) = 1 THEN 1 ELSE 0 END) = 1 THEN 'Active'
    ELSE 'Inactive'
  END AS fund_status
FROM (
  SELECT DISTINCT fund_key FROM dbo.fact_commitment WHERE investor_key = @investorKey
  UNION
  SELECT DISTINCT fund_key FROM dbo.fact_investment WHERE investor_key = @investorKey
) fk
INNER JOIN dbo.dim_fund df ON df.fund_key = fk.fund_key
  AND <current fund filter on df>
GROUP BY df.fund_code
ORDER BY df.fund_code
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
```

**Totals by fund_code (second query):** committed amount + FMV/return grouped by `df.fund_code`.

---

### 3.4 Period dropdown — `GET /api/CapitalInvestors/{investorKey}/periods`

Same structure as fund periods, scoped by **`investor_key`** instead of `fund_key`.

| source | Quarterly fact | Daily fact |
|--------|----------------|------------|
| commitments, unfunded | `fact_investor_portfolio_quarterly` | `fact_commitment` |
| investments | `fact_investment` | `fact_investment` |
| distributions | `fact_distribution` + `dim_fund` | `fact_distribution` + `dim_fund` |
| nav | `fact_fund_nav` (investor fund keys) | `fact_fund_nav` (investor fund keys) |

`view=ltd` → single “All Periods” option (no SQL).

---

### 3.5 Investor metric tabs (commitments, unfunded, investments, distributions, NAV)

**Endpoints:**
- `GET .../commitments?view=&dateKey=`
- `GET .../unfunded-commitments?view=&dateKey=`
- `GET .../investments?view=&dateKey=`
- `GET .../distributions?view=&dateKey=`
- `GET .../nav?view=&dateKey=`

**Required:** `view`  
**Optional:** `dateKey` (quarterly/daily)  
**Scope:** `investor_key = @investorKey`  
**Grouping:** all fund-level rows grouped by **`df.fund_code`** (plus period / transaction type as applicable)

#### Commitments — example `view=ltd`

```sql
SELECT
  ISNULL(df.fund_code, '') AS fund_code,
  Period = 'Life To Date',
  commitment_amount = SUM(p.commitment_amount),
  Description = 'Total Commitment as of Date'
FROM dbo.fact_investor_portfolio_ltd p
INNER JOIN dbo.dim_fund df ON df.fund_key = p.fund_key
  AND <current fund filter on df>
WHERE p.investor_key = @investorKey
GROUP BY df.fund_code
ORDER BY df.fund_code;
```

#### Commitments — `view=quarterly` (+ optional quarter filter)

```sql
-- Fact: fact_investor_portfolio_quarterly
WHERE p.investor_key = @investorKey
  AND <quarter filter if @dateKey>
GROUP BY df.fund_code, p.quarter_year
ORDER BY df.fund_code, p.quarter_year;
```

#### Commitments — `view=daily` (+ optional `@dateKey`)

```sql
-- Fact: fact_commitment
WHERE fc.investor_key = @investorKey
  AND <fc.posted_date_key = @dateKey if provided>
GROUP BY df.fund_code, fc.posted_date_key
HAVING SUM(fc.committed_amount) != 0;
```

#### Metric summary matrix (investor scope)

| Metric | LTD fact | Quarterly fact | Daily fact | Group by (investor) |
|--------|----------|----------------|------------|---------------------|
| Commitments | `fact_investor_portfolio_ltd` | `fact_investor_portfolio_quarterly` | `fact_commitment` | `df.fund_code` [+ period] |
| Unfunded | `fact_investor_portfolio_ltd` | `fact_investor_portfolio_quarterly` | `fact_commitment` | `df.fund_code` [+ period] |
| Investments | `fact_investment` | `fact_investment` + `dim_date` | `fact_investment` | `df.fund_code` [+ period] |
| Distributions | `fact_distribution` | `fact_distribution` + `dim_date` | `fact_distribution` | `df.fund_code`, `transaction_type` [+ period] |
| NAV | `fact_fund_nav` | `fact_fund_nav` + `dim_date` | `fact_fund_nav` | `df.fund_code` [+ period] |

All investor metric queries join:
```sql
INNER JOIN dbo.dim_fund df ON df.fund_key = <fact>.fund_key
  AND <current fund filter on df>
```

#### Distributions response shape

Flat SQL rows are **post-grouped in the API** by `(transaction_type, fund_code)` into:

```json
{
  "fund_code": "LP7",
  "transaction_type": "Distribution-Excess Cash",
  "total_amount": 0,
  "total_units": 0,
  "periods": [ { "period": "Q1 2025", "amount": ..., "units": ... } ]
}
```

---

## 4. Quick reference — filter → SQL

| UI filter | SQL parameter / clause |
|-----------|------------------------|
| Search funds | `@search` → fund name LIKE |
| Search investors | `@search` → investor name LIKE |
| Fund context | `@fundKey` → `fund_key = @fundKey` |
| Investor context | `@investorKey` → `investor_key = @investorKey` |
| LTD view | No period filter; portfolio LTD or single aggregate row |
| Quarterly view | Group by `quarter_year` or `d.quarter_year` |
| Daily view | Group by `posted_date_key` or `date_key` |
| Period dropdown selection | `@dateKey` → quarter lookup or exact date key |
| Current fund only | `dim_fund` SCD filter on all fund joins |
| Current investor only | `is_current = 1` on `dim_investor` |
| Assets at property level | `fund_level IN ('000 Property', '000 - Property')` |
| Investor metrics per fund | `GROUP BY df.fund_code` |

---

## 5. Implementation files

| Portal | Controller | Service |
|--------|------------|---------|
| Funds | `Controllers/FundsController.cs` | `Services/FundPortalService.cs` |
| Capital Investors | `Controllers/CapitalInvestorsController.cs` | `Services/InvestorPortalService.cs`, `Services/InvestorPortalService.Metrics.cs` |

*Generated from codebase state. If SQL in services changes, update this document to match.*
