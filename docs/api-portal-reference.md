# Kingsight API Reference — Funds, Assets & Capital Investors

Warehouse objects are referenced as `dbo.*` (Fabric: `wh_enterprise_gold.dbo`). JSON is mostly **camelCase**; properties with `[JsonPropertyName]` are listed with their **exact wire name**.

## Common wrappers

| Wrapper | Keys |
|---------|------|
| `PagedResult<T>` | `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage` |
| `PortalListPageResult<TItem, TSummary>` | above + `summary` |

## Time views

Query parameter `view`: `ltd` | `quarterly` | `daily`

- **LTD / quarterly portfolio metrics** → `dbo.fact_investor_portfolio_ltd` or `dbo.fact_investor_portfolio_quarterly`
- **Quarterly period**: `dateKey` (single quarter) or `calendarYear` (all quarters in year) on transaction endpoints
- **Current dimension rows**: `is_current = 1` on `dim_fund`, `dim_investor`, `dim_property` where applied

---

## Warehouse tables used

| Constant | Table |
|----------|-------|
| `DimDate` | `dbo.dim_date` |
| `DimFund` | `dbo.dim_fund` |
| `DimInvestor` | `dbo.dim_investor` |
| `DimProperty` | `dbo.dim_property` |
| `DimTransactionType` | `dbo.dim_transaction_type` |
| `FactInvestorPortfolioLtd` | `dbo.fact_investor_portfolio_ltd` |
| `FactInvestorPortfolioQuarterly` | `dbo.fact_investor_portfolio_quarterly` |
| `FactCommitted` | `dbo.fact_commitment` |
| `FactInvestment` | `dbo.fact_investment` |
| `FactDistribution` | `dbo.fact_distribution` |
| `FactFundNav` | `dbo.fact_fund_nav` |
| `FactAssetMetrics` | `dbo.fact_asset_metrics` (latest row per property by max `date_key`) |

### Portfolio fact columns

Aggregated across list, profile, and transaction APIs:

| Response key | Source column(s) |
|--------------|------------------|
| `commitment_amount` / `totalCommitment` | `commitment_amount` |
| `net_invested_capital_amount` / `netInvestedCapital` | `net_invested_capital_amount` |
| `net_distributed_amount` / `netDistributed` | `preferred_return_amount` + `sales_gain_amount` + `excess_cash_amount` |
| `reserved_amount` / `reservedUncalled` | `reserved_amount` |
| `unfunded_amount` / `unfunded` | `unfunded_amount`, or `commitment_amount − capital_called_amount` if unfunded column empty |
| `released_capital_amount` / `releasedCapital` | `released_capital_amount` |
| `capital_deployed` / `capitalDeployed` | `capital_called_amount` (sum) |

---

# 1. Assets (`/api/Assets`)

## `GET /api/Assets/filter-options`

| Key | DB source |
|-----|-----------|
| `assetTypes[]` | `dim_property.asset_type` (distinct, current) |
| `investmentTypes[]` | `dim_property.investment_type` |
| `geographies[]` | `dim_property.geography` |
| `statuses[]` | `dim_property.property_status` |

Each option: `{ value, label }`.

---

## `GET /api/Assets`

Query: `search`, `assetType`, `investmentType`, `geography`, `status`, `fundCode`, `sortBy`, `sortDir`, `page`, `pageSize`

### `summary`

| Key | DB source |
|-----|-----------|
| `total_gla_sf` | `fact_asset_metrics.gross_leasable_area_sqft` (latest per property), summed |
| `active_properties` | `dim_property` count (active status filter) |
| `total_properties` | `dim_property` count |
| `total_committed_sf` | `fact_asset_metrics.committed_area_sqft` |
| `total_vacant_sf` | `fact_asset_metrics.vacant_area_sqft` |

### `items[]`

| Key | DB source |
|-----|-----------|
| `propertyKey` | `dim_property.property_key` |
| `property_code` | `dim_property.property_code` |
| `propertyName` | `dim_property.property_name` |
| `geography`, `city`, `province` | `dim_property.*` |
| `assetType` | `dim_property.asset_type` |
| `investment_type` | `dim_property.investment_type` |
| `development_type` | `dim_property.development_type` |
| `property_status` | `dim_property.property_status` |
| `gla_sf` | `fact_asset_metrics.gross_leasable_area_sqft` |
| `occupied_sf` | `fact_asset_metrics.occupied_area_sqft` |
| `committed_sf` | `fact_asset_metrics.committed_area_sqft` |
| `vacant_sf` | `fact_asset_metrics.vacant_area_sqft` |
| `isPortfolio` | `dim_property.portfolio` |

---

## `GET /api/Assets/{propertyKey}`

| Key | DB source |
|-----|-----------|
| `property_key`, `property_code`, `property_name` | `dim_property` |
| `geography`, `city`, `province` | `dim_property` |
| `asset_type`, `investment_type`, `development_type` | `dim_property` |
| `status` | `dim_property.property_status` |
| `is_portfolio` | `dim_property.portfolio` |
| `acquisition_date` | `dim_property.property_acquisition` |
| `total_gla_sf` | `fact_asset_metrics.gross_leasable_area_sqft` |
| `occupied_area_sf` | `fact_asset_metrics.occupied_area_sqft` |
| `committed_area_sf` | `fact_asset_metrics.committed_area_sqft` |
| `vacant_area_sf` | `fact_asset_metrics.vacant_area_sqft` |
| `occupancy_rate`, `vacancy_rate` | computed from GLA / occupied / vacant |
| `est_market_value` | metrics or property columns (`market_value`, `fair_market_value`, etc.) |
| `est_annual_noi` | metrics NOI columns |
| `investment_count` | count of linked funds via property–fund join |

---

## `GET /api/Assets/{propertyKey}/leasing-summary`

All from **latest** `fact_asset_metrics` for the property (+ `property_key`, `date_key`):

| Key | DB column |
|-----|-----------|
| `property_key` | `dim_property.property_key` |
| `date_key` | `fact_asset_metrics.date_key` |
| `last_refreshed_date` | derived from `date_key` |
| `gross_leasable_area_sqft` | `gross_leasable_area_sqft` |
| `occupied_area_sqft` | `occupied_area_sqft` |
| `committed_area_sqft` | `committed_area_sqft` |
| `vacant_area_sqft` | `vacant_area_sqft` |
| `total_units`, `occupied_units`, `vacant_units` | `fact_asset_metrics` unit columns |
| `weighted_avg_lease_term_months` | `weighted_avg_lease_term_months` |
| `weighted_avg_lease_term_rent_months` | `weighted_avg_lease_term_rent_months` |
| `gla_available_to_lease_sqft` | `gla_available_to_lease_sqft` |
| `total_leasing_committed_sqft` | `total_leasing_committed_sqft` |
| `new_leasing_committed_sqft` | `new_leasing_committed_sqft` |
| `renewal_leasing_committed_sqft` | `renewal_leasing_committed_sqft` |
| `gla_available_to_lease_units` | `gla_available_to_lease_units` |
| `total_leasing_committed_units` | `total_leasing_committed_units` |
| `new_leasing_committed_units` | `new_leasing_committed_units` |
| `renewal_leasing_committed_units` | `renewal_leasing_committed_units` |
| `occupancy_rate`, `vacancy_rate` | computed |

---

## `GET /api/Assets/{propertyKey}/investments`

`items[]` — one row per fund linked to the property:

| Key | DB source |
|-----|-----------|
| `fundKey` | `dim_fund.fund_key` |
| `fundName` | `dim_fund.fund_name` |
| `fundType` | `dim_fund.fund_type_name` |
| `fundStrategy` | `dim_fund.fund_strategy_name` |
| `status` | derived from `dim_fund.dissolution_date`, `is_current` |
| `fundStartDate` | `dim_fund.fund_start_date` |
| `totalValue` | `fact_investment`: units or amount FMV aggregate by fund |
| `totalReturnPercent` | computed from `fact_investment.invested_amount` vs `invested_amount_fmv` |

Supporting tables: `dim_property` → fund join, `fact_commitment.committed_amount`, `fact_investment.*`.

---

# 2. Funds (`/api/Funds`)

## `GET /api/Funds/filter-options`

| Key | DB source |
|-----|-----------|
| `fundTypes[]` | `dim_fund.fund_type_name` |
| `strategies[]` | `dim_fund.fund_strategy_name` |
| `calendarYears[]`, `quarterlyPeriods[]` | `fact_investor_portfolio_quarterly` + `dim_date` |

---

## `GET /api/Funds`

Query: `search`, `view`, `dateKey`, `fundType`, `strategy`, `sortBy`, `sortDir`, `page`, `pageSize`

**`summary`**: portfolio fact aggregates across filtered funds (`total_funds`, `total_commitment`, `net_invested_capital`, `net_distributed`, `reserved_uncalled`).

**`items[]`**

| Key | DB source |
|-----|-----------|
| `fundKey` | `dim_fund.fund_key` |
| `fundName` | `dim_fund.fund_name` |
| `fund_type_name` | `dim_fund.fund_type_name` |
| `fund_strategy_name` | `dim_fund.fund_strategy_name` |
| `investors` | distinct count from `fact_commitment` ∪ `fact_investment` |
| `assets` | `dim_property` count (fund-linked, current, fund-level 000) |
| `commitment_amount`, `net_invested_capital_amount`, `net_distributed_amount`, `reserved_amount`, `released_capital_amount` | `fact_investor_portfolio_ltd` or `quarterly` (by `view`) |

---

## `GET /api/Funds/{fundKey}/periods`

Query: `view`, `metric` (e.g. commitments, nav, distributions)

| Key | DB source |
|-----|-----------|
| `date_key` | `dim_date.date_key` |
| `full_date` | `dim_date.full_date` |
| `label` | formatted period label |
| `disabled` | `true` for LTD |
| `quarter_year` | `dim_date.quarter_year` |
| `calendar_year` | `dim_date.calendar_year` |
| `month_year`, `period_start`, `period_end` | `dim_date` |

Periods scoped by fund + metric-specific fact table (`fact_commitment`, `fact_fund_nav`, `fact_distribution`, portfolio facts).

---

## `GET /api/Funds/{fundKey}`

| Key | DB source |
|-----|-----------|
| `fund_key`, `fund_code`, `fund_name` | `dim_fund` |
| `fund_type` | `dim_fund.fund_type_name` |
| `strategy` | `dim_fund.fund_strategy_name` |
| `status` | derived from `dim_fund.is_active` |
| `start_date` | `dim_fund.fund_start_date` |
| `is_sidecar` | `dim_fund.is_sidecar` |
| `total_commitment`, `capital_deployed`, `net_invested_capital`, `net_distributed`, `reserved_uncalled`, `released_capital` | aggregates from `fact_investor_portfolio_ltd` |
| `asset_count` | `dim_property` (fund scope) |
| `investor_count` | distinct investors in `fact_commitment` ∪ `fact_investment` |
| `investors[]` | `{ investor_key, investor_name }` from `fact_investor_portfolio_ltd` + `dim_investor` |

---

## `GET /api/Funds/{fundKey}/assets`

`items[]` from `dim_property` (fund-linked):

| Key | DB source |
|-----|-----------|
| `propertyKey` | `property_key` |
| `property_name`, `city`, `province`, `geography` | `dim_property` |
| `asset_type`, `investment_type`, `property_status` | `dim_property` |
| `property_acquisition`, `property_disposition` | `dim_property` |

---

## `GET /api/Funds/{fundKey}/investors`

`items[]`:

| Key | DB source |
|-----|-----------|
| `investorKey` | `dim_investor.investor_key` |
| `investorName` | `dim_investor.investor_name` |
| `relationship_name` | `dim_investor.relationship_name` |
| `investorType` | `dim_investor.investor_type_name` |
| `contact_first_name`, `contact_last_name` | `dim_investor` |
| `totalInvested`, `totalInvestedFmv` | `fact_investment` aggregates |
| `status` | derived from `dim_investor.is_current` |
| `memberSince`, `joinYear` | min `fact_investment.calculation_date_key` |

Base investor set: `fact_commitment` ∪ `fact_investment` for fund.

---

## Granular tabs (paginated `FundGranularRowDto`)

Shared row keys (populated per view):

| Key | When | DB source |
|-----|------|-----------|
| `fund_code` | always | `dim_fund.fund_code` |
| `investor_code` | investor-scoped rows | `dim_investor.investor_code` |
| `period` | LTD / quarterly | label or `quarter_year` from `dim_date` / portfolio |
| `date`, `posted_date_key` | daily | `dim_date` / fact date keys |
| `commitment_amount` | commitments | see table below |
| `invested_amount` | investments | `fact_investment.invested_amount` |
| `amount`, `units` | distributions / NAV | fact amounts |
| `transaction_type` | distributions | `dim_transaction_type` |
| `description` | static / derived label | — |

### `GET .../commitments`

| View | Primary table | Amount column |
|------|---------------|---------------|
| LTD | `fact_investor_portfolio_ltd` | `commitment_amount` (single LTD row) |
| Quarterly | `fact_investor_portfolio_quarterly` + `dim_date` | `commitment_amount` by quarter |
| Daily | `fact_commitment` + `dim_date` | `committed_amount` |

### `GET .../unfunded-commitments`

| View | Table | Logic |
|------|-------|-------|
| LTD / Quarterly | portfolio facts | `unfunded_amount` or commitment − called |
| Daily | `fact_commitment` | `committed_amount − called_amount` |

### `GET .../investments`

| View | Table | Columns |
|------|-------|---------|
| LTD / Quarterly | `fact_investment` + `dim_date` | `invested_amount`, grouped by period |
| Daily | `fact_investment` | per-day `invested_amount` |

### `GET .../distributions`

Returns `FundDistributionGroupDto[]` grouped by `transaction_type`:

| Key | DB source |
|-----|-----------|
| `fund_code`, `investor_code` | dims |
| `transaction_type` | `dim_transaction_type` |
| `periods[]` | `{ period, date, posted_date_key, amount, units, description }` from `fact_distribution` |
| `total_amount`, `total_units` | sums per group |

### `GET .../nav`

| View | Table | Columns |
|------|-------|---------|
| LTD | `fact_fund_nav` | latest snapshot |
| Quarterly | `fact_fund_nav` + `dim_date` | by quarter |
| Daily | `fact_fund_nav` + `dim_date` | by day (`nav_amount`, `nav_units`) |

---

## Transaction tables (fund → investor rows)

Query: `view`, `dateKey`, `calendarYear`, investor filters, `sortBy`, `sortDir`, `page`, `pageSize`

**Fact table**: `fact_investor_portfolio_ltd` or `fact_investor_portfolio_quarterly`  
**Dims**: `dim_investor`, `dim_date` (quarterly period filter)

### `GET .../capital-activities` → `FundInvestorCapitalActivitiesDto`

| Key | DB column |
|-----|-----------|
| `investor_code`, `investor_name` | `dim_investor` |
| `quarter_year` | `fact.quarter_year` (quarterly grouping) |
| `type` | `dim_investor.investor_type_name` |
| `called` | `capital_called_amount` |
| `transfer_in` | `investment_transferred_in_amount` |
| `transfer_out` | `investment_transferred_out_amount` |
| `redemption` | `redemption_amount` |

### `GET .../distributions-table` → `FundInvestorDistributionsDto`

| Key | DB column |
|-----|-----------|
| `investor_code`, `investor_name`, `quarter_year`, `type` | dims + `quarter_year` |
| `committed` | `commitment_amount` |
| `unfunded` | commitment − called (computed) |
| `cash_dist` | `excess_cash_amount` |
| `gain_dist` | `sales_gain_amount` |
| `preferred_return` | `preferred_return_amount` |
| `return_of_capital` | `return_of_capital_amount` |
| `released` | `released_capital_amount` |

### `GET .../irr` → `FundInvestorIrrDto`

| Key | DB column |
|-----|-----------|
| `investor_code`, `investor_name`, `quarter_year`, `type` | dims |
| `irr_1_year_pct` … `irr_ltd_pct` | `irr_*_pct` on portfolio fact |

### `GET .../capital-obligations` (quarterly only) → `FundInvestorObligationDto`

**Table**: `fact_investor_portfolio_quarterly` only (unpivoted)

| Key | DB source |
|-----|-----------|
| `investor_code`, `investor_name`, `quarter_year` | dims + fact |
| `type` | literal: Commitment / Unfunded / Reserve / Release |
| `amount` | `commitment_amount` / `unfunded_amount` / `reserved_amount` / `released_capital_amount` |

### `GET .../net-assets` (quarterly only) → `FundInvestorNetAssetsDto`

| Key | DB source |
|-----|-----------|
| `investor_code`, `investor_name`, `quarter_year` | dims + fact |
| `type` | literal: 1 Year, 3 Year, 5 Year, 7 Year, 10 Year, ITD |
| `ret` | `irr_1_year_pct` … `irr_ltd_pct` |

### Filter endpoints

`.../capital-activities/filters`, `.../distributions-table/filters`, `.../irr/filters`, `.../capital-obligations/filters`, `.../net-assets/filters`

| Key | DB source |
|-----|-----------|
| `items[]` | `{ value: investor_code, label: investor_name }` from portfolio fact + `dim_investor` |

---

# 3. Capital Investors (`/api/CapitalInvestors`)

## `GET /api/CapitalInvestors/filter-options`

| Key | DB source |
|-----|-----------|
| `investorTypes[]` | `dim_investor.investor_type_name` |
| `relationships[]` | `dim_investor.relationship_name` |
| `calendarYears[]`, `quarterlyPeriods[]` | quarterly portfolio + `dim_date` |

---

## `GET /api/CapitalInvestors`

Query: `search`, `view`, `dateKey`, `investorType`, `relationship`, `sortBy`, `sortDir`, `page`, `pageSize`

**`summary`**: `total_investors`, portfolio KPI totals (same aggregate logic as fund list).

**`items[]`**

| Key | DB source |
|-----|-----------|
| `investor_key`, `investor_name` | `dim_investor` |
| `investor_type` | `investor_type_name` |
| `relationship_name` | `dim_investor` |
| `contact_first_name`, `contact_last_name` | `dim_investor` |
| `fund_count` | distinct `fund_key` in portfolio fact |
| `commitment_amount`, `net_invested_capital_amount`, `net_distributed_amount`, `reserved_amount`, `unfunded_amount`, `released_capital_amount` | portfolio fact (by `view`) |

---

## `GET /api/CapitalInvestors/{investorKey}/periods`

Same shape as fund periods; scoped to investor + metric.

---

## `GET /api/CapitalInvestors/{investorKey}`

| Key | DB source |
|-----|-----------|
| `investor_name`, `investor_type`, `relationship`, `status`, `contact` | `dim_investor` (+ contact fields) |
| `total_commitment`, `net_invested_capital`, `net_distributed`, `reserved_uncalled`, `released_capital` | portfolio fact aggregates |
| `fund_count` | distinct funds in portfolio fact |
| `capital_deployed` | sum `capital_called_amount` |
| `funds[]` | `{ fund_key, fund_code, fund_name }` from portfolio fact + `dim_fund` |

---

## `GET /api/CapitalInvestors/{investorKey}/funds`

`InvestorInvestmentDto[]`:

| Key | DB source |
|-----|-----------|
| `fundKey`, `fund_code`, `fundName` | `dim_fund` |
| `fundType` | `fund_type_name` |
| `fundCategory` | `fund_strategy_name` |
| `status` | derived from `dim_fund` |
| `investedAmount`, `investedAmountFmv` | `fact_investment` aggregates per fund |
| `totalReturnPercent` | computed from investment amounts |

Fund list: distinct funds from `fact_commitment` ∪ `fact_investment` ∪ `fact_investor_portfolio_ltd`.

---

## `GET /api/CapitalInvestors/{investorKey}/fund-holdings`

| Key | DB source |
|-----|-----------|
| `date_key` | max `date_key` on `fact_investor_portfolio_ltd` for investor |
| `items[].fund_key`, `fund_name` | `dim_fund` |
| `items[].since` | min `fact_investment.calculation_date_key` per fund |
| `items[].commitment` | `commitment_amount` |
| `items[].unfunded` | unfunded expression |
| `items[].net_invested` | `net_invested_capital_amount` |
| `items[].distributed` | preferred + sales_gain + excess_cash |

**Table**: latest snapshot on `fact_investor_portfolio_ltd`.

---

## Granular tabs (investor-scoped)

Same `view` / table mapping as fund granular endpoints, but rows include `fund_code` and filter by `investor_key`:

| Endpoint | Row type | Notes |
|----------|----------|-------|
| `.../commitments` | `FundGranularRowDto` | `fund_code`; same LTD/quarterly/daily sources |
| `.../unfunded-commitments` | `FundGranularRowDto` | per fund |
| `.../investments` | `FundGranularRowDto` | `fact_investment` |
| `.../distributions` | `FundDistributionGroupDto` | grouped by `transaction_type` |
| `.../nav` | `FundGranularRowDto` | `fact_fund_nav` |

---

## Transaction tables (investor → fund rows)

**Fact**: `fact_investor_portfolio_ltd` or `quarterly` + `dim_fund`

### `GET .../capital-activities` → `InvestorFundCapitalActivitiesDto`

| Key | DB column |
|-----|-----------|
| `fund_key`, `fund_code`, `fund_name` | `dim_fund` |
| `quarter_year`, `type` | `quarter_year`, `dim_fund.fund_type_name` |
| `called`, `transfer_in`, `transfer_out`, `redemption` | same capital-activity columns as fund side |

### `GET .../distributions-table` → `InvestorFundDistributionsDto`

Wide distribution metrics + `net_invested_capital_amount`, `net_distributed_amount`, `reserved_amount`.

### `GET .../irr` → `InvestorFundIrrDto`

`fund_key`, `fund_code`, `fund_name`, `quarter_year`, `type`, `irr_*_pct`.

### `GET .../capital-obligations` → `InvestorFundObligationDto`

Quarterly only; unpivoted `type` + `amount` per fund.

### `GET .../net-assets` → `InvestorFundNetAssetsDto`

Quarterly only; unpivoted IRR horizons as `type` + `ret`.

### Filter endpoints

`items[]`: `{ value: fund_code, label: fund_name }` from portfolio fact + `dim_fund`.

---

## `GET /api/CapitalInvestors/{investorKey}/fund-exposure`

`InvestorFundExposureDto[]` — portfolio metrics per fund:

| Key | DB source |
|-----|-----------|
| `fund_key`, `fund_code`, `fund_name` | `dim_fund` |
| `commitment_amount`, `net_invested_capital_amount`, `net_distributed_amount`, `reserved_amount`, `unfunded_amount`, `released_capital_amount` | portfolio fact |

---

## `GET /api/CapitalInvestors/{investorKey}/assets`

`InvestorUnderlyingAssetDto[]` — properties in funds where investor has exposure:

| Key | DB source |
|-----|-----------|
| `property_key`, `property_name`, `asset_type`, `city` | `dim_property` |
| `fund_code`, `fund_name` | `dim_fund` (property–fund join) |
| `gla_sf` | `fact_asset_metrics.gross_leasable_area_sqft` |
| `occupancy_pct` | computed: `occupied_area_sqft / gla` |
| `market_value`, `cap_rate` | always `null` (not wired in SQL) |
| `status` | `dim_property.property_status` |

Investor scope: funds from portfolio fact ∪ commitment ∪ investment.

---

## Endpoint count

| Controller | Routes |
|------------|--------|
| **Assets** | 5 |
| **Funds** | 22 |
| **CapitalInvestors** | 24 |
| **Total** | **51** |

---

## Notes

1. **`distributions` vs `distributions-table`**: `distributions` is the expandable UI grouped by transaction type; `distributions-table` is the wide portfolio-fact grid (capital activities / IRR follow the same split).
2. **Obligations & net-assets** are **quarterly-only** (`fact_investor_portfolio_quarterly`).
3. **Legacy aliases** on some DTOs (`category`, `currentValue`, `totalInvested`, `status` on property list) mirror newer property names in JSON.
