using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    /// <summary>
    /// Physical columns on wh_gold1.subjective_input.external_serviced_loan.
    /// Loan Alias and Investor Alias dropdown values are persisted on this table only
    /// (e.g. loan_name / loan_alias_name, investor / investor_alias_name).
    /// Do not join or write loan_alias_relationship / investor_alias_relationship.
    /// </summary>
    internal sealed class ExternalServicedLoanColumnMap
    {
        public required string TableName { get; init; }
        public required string ExtLoanCode { get; init; }

        public string? LoanAliasName { get; init; }
        public string? AsAtDate { get; init; }
        public string? ServicerId { get; init; }
        public string? Description { get; init; }
        public string? InvestorAliasName { get; init; }
        public string? InvestorCode { get; init; }
        public string? DefaultDate { get; init; }
        public string? MaturityDate { get; init; }
        public string? InterestOffDate { get; init; }
        public string? TaxMemoDate { get; init; }
        public string? SecurityValue { get; init; }
        public string? Units { get; init; }
        public string? NetAcres { get; init; }
        public string? SquareFeet { get; init; }
        public string? InterestRate { get; init; }
        public string? PrincipalBalance { get; init; }
        public string? OutstandingInterest { get; init; }
        public string? AccruedInterest { get; init; }
        public string? LateInterest { get; init; }
        public string? OutstandingInvoices { get; init; }
        public string? EstRealizationCosts { get; init; }
        public string? CostToComplete { get; init; }
        public string? TaxArrears { get; init; }
        public string? InterestAsOfTaxMemo { get; init; }
        public string? InterestAdjustment { get; init; }

        public SubjectiveInputMasterAuditColumns Audit { get; init; } = new();

        public static async Task<ExternalServicedLoanColumnMap> ProbeAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            var extLoanCode = await RequireFirstAsync(
                connectionString, tableName, ["ext_loan_code"], "ext_loan_code", cancellationToken);

            var audit = await SubjectiveInputMasterAuditColumns.ProbeAsync(
                connectionString, tableName, cancellationToken);

            return new ExternalServicedLoanColumnMap
            {
                TableName = tableName,
                ExtLoanCode = extLoanCode,
                LoanAliasName = await FindFirstAsync(
                    connectionString, tableName, ["loan_alias_name"], cancellationToken),
                AsAtDate = await FindFirstAsync(
                    connectionString, tableName, ["as_at_date", "as_of_date"], cancellationToken),
                ServicerId = await FindFirstAsync(
                    connectionString, tableName, ["servicer_id", "syndicate_loan_code"], cancellationToken),
                Description = await FindFirstAsync(
                    connectionString, tableName, ["description", "loan_description"], cancellationToken),
                InvestorAliasName = await FindFirstAsync(
                    connectionString, tableName, ["investor_alias_name", "investor"], cancellationToken),
                InvestorCode = await FindFirstAsync(
                    connectionString, tableName, ["investor_code"], cancellationToken),
                DefaultDate = await FindFirstAsync(
                    connectionString, tableName, ["default_date", "date_of_default"], cancellationToken),
                MaturityDate = await FindFirstAsync(
                    connectionString, tableName, ["maturity_date"], cancellationToken),
                InterestOffDate = await FindFirstAsync(
                    connectionString, tableName, ["interest_off_date"], cancellationToken),
                TaxMemoDate = await FindFirstAsync(
                    connectionString, tableName, ["tax_memo_date"], cancellationToken),
                SecurityValue = await FindFirstAsync(
                    connectionString, tableName, ["security_value"], cancellationToken),
                Units = await FindFirstAsync(
                    connectionString, tableName, ["units"], cancellationToken),
                NetAcres = await FindFirstAsync(
                    connectionString, tableName, ["net_acres", "acres"], cancellationToken),
                SquareFeet = await FindFirstAsync(
                    connectionString, tableName, ["square_feet", "sf"], cancellationToken),
                InterestRate = await FindFirstAsync(
                    connectionString, tableName, ["interest_rate"], cancellationToken),
                PrincipalBalance = await FindFirstAsync(
                    connectionString, tableName, ["principal_balance", "principal"], cancellationToken),
                OutstandingInterest = await FindFirstAsync(
                    connectionString, tableName, ["outstanding_interest"], cancellationToken),
                AccruedInterest = await FindFirstAsync(
                    connectionString, tableName, ["accrued_interest"], cancellationToken),
                LateInterest = await FindFirstAsync(
                    connectionString, tableName, ["late_interest"], cancellationToken),
                OutstandingInvoices = await FindFirstAsync(
                    connectionString, tableName, ["outstanding_invoice", "outstanding_invoices"], cancellationToken),
                EstRealizationCosts = await FindFirstAsync(
                    connectionString, tableName, ["estimated_realization_costs", "est_realization_costs"], cancellationToken),
                CostToComplete = await FindFirstAsync(
                    connectionString, tableName, ["cost_to_complete"], cancellationToken),
                TaxArrears = await FindFirstAsync(
                    connectionString, tableName, ["tax_arrears", "arrears_as_of"], cancellationToken),
                InterestAsOfTaxMemo = await FindFirstAsync(
                    connectionString, tableName, ["interest_as_of_tax_memo", "interest_as_of_memo"], cancellationToken),
                InterestAdjustment = await FindFirstAsync(
                    connectionString, tableName, ["interest_adjustment"], cancellationToken),
                Audit = audit
            };
        }

        public string BuildListSql()
        {
            var select = BuildSelectList();
            var orderBy = AsAtDate is not null
                ? $"[{ExtLoanCode}], [{AsAtDate}]"
                : $"[{ExtLoanCode}]";

            return $"""
                select {select}
                from {TableName}
                order by {orderBy}
                """;
        }

        public string BuildSelectByKeySql()
        {
            var select = BuildSelectList();
            var asAtPredicate = AsAtDate is not null
                ? $"""
                  and ((@as_at_date is null and [{AsAtDate}] is null)
                       or [{AsAtDate}] = @as_at_date)
                  """
                : string.Empty;

            return $"""
                select {select}
                from {TableName}
                where [{ExtLoanCode}] = @ext_loan_code
                {asAtPredicate}
                """;
        }

        public string BuildInsertSql()
        {
            var columns = new List<string> { ExtLoanCode };
            var values = new List<string> { "@ext_loan_code" };

            AddWriteColumn(columns, values, LoanAliasName, "@loan_alias_name");
            AddWriteColumn(columns, values, AsAtDate, "@as_at_date");
            AddWriteColumn(columns, values, ServicerId, "@servicer_id");
            AddWriteColumn(columns, values, Description, "@description");
                AddWriteColumn(columns, values, InvestorAliasName, "@investor_alias_name");
            AddWriteColumn(columns, values, InvestorCode, "@investor_code");
            AddWriteColumn(columns, values, DefaultDate, "@default_date");
            AddWriteColumn(columns, values, MaturityDate, "@maturity_date");
            AddWriteColumn(columns, values, InterestOffDate, "@interest_off_date");
            AddWriteColumn(columns, values, TaxMemoDate, "@tax_memo_date");
            AddWriteColumn(columns, values, SecurityValue, "@security_value");
            AddWriteColumn(columns, values, Units, "@units");
            AddWriteColumn(columns, values, NetAcres, "@net_acres");
            AddWriteColumn(columns, values, SquareFeet, "@square_feet");
            AddWriteColumn(columns, values, InterestRate, "@interest_rate");
            AddWriteColumn(columns, values, PrincipalBalance, "@principal_balance");
            AddWriteColumn(columns, values, OutstandingInterest, "@outstanding_interest");
            AddWriteColumn(columns, values, AccruedInterest, "@accrued_interest");
            AddWriteColumn(columns, values, LateInterest, "@late_interest");
            AddWriteColumn(columns, values, OutstandingInvoices, "@outstanding_invoices");
            AddWriteColumn(columns, values, EstRealizationCosts, "@est_realization_costs");
            AddWriteColumn(columns, values, CostToComplete, "@cost_to_complete");
            AddWriteColumn(columns, values, TaxArrears, "@tax_arrears");
            AddWriteColumn(columns, values, InterestAsOfTaxMemo, "@interest_as_of_tax_memo");
            AddWriteColumn(columns, values, InterestAdjustment, "@interest_adjustment");

            var columnList = string.Join(",\n                    ", columns.Select(Bracket));
            var valueList = string.Join(",\n                    ", values) + Audit.BuildInsertValueList();

            return $"""
                insert into {TableName} (
                    {columnList}{Audit.BuildInsertColumnList()})
                values (
                    {valueList})
                """;
        }

        public string BuildUpdateSql()
        {
            var sets = new List<string>();

            AddUpdateSet(sets, LoanAliasName, "@loan_alias_name");
            AddUpdateSet(sets, AsAtDate, "@as_at_date");
            AddUpdateSet(sets, ServicerId, "@servicer_id");
            AddUpdateSet(sets, Description, "@description");
            AddUpdateSet(sets, InvestorAliasName, "@investor_alias_name");
            AddUpdateSet(sets, InvestorCode, "@investor_code");
            AddUpdateSet(sets, DefaultDate, "@default_date");
            AddUpdateSet(sets, MaturityDate, "@maturity_date");
            AddUpdateSet(sets, InterestOffDate, "@interest_off_date");
            AddUpdateSet(sets, TaxMemoDate, "@tax_memo_date");
            AddUpdateSet(sets, SecurityValue, "@security_value");
            AddUpdateSet(sets, Units, "@units");
            AddUpdateSet(sets, NetAcres, "@net_acres");
            AddUpdateSet(sets, SquareFeet, "@square_feet");
            AddUpdateSet(sets, InterestRate, "@interest_rate");
            AddUpdateSet(sets, PrincipalBalance, "@principal_balance");
            AddUpdateSet(sets, OutstandingInterest, "@outstanding_interest");
            AddUpdateSet(sets, AccruedInterest, "@accrued_interest");
            AddUpdateSet(sets, LateInterest, "@late_interest");
            AddUpdateSet(sets, OutstandingInvoices, "@outstanding_invoices");
            AddUpdateSet(sets, EstRealizationCosts, "@est_realization_costs");
            AddUpdateSet(sets, CostToComplete, "@cost_to_complete");
            AddUpdateSet(sets, TaxArrears, "@tax_arrears");
            AddUpdateSet(sets, InterestAsOfTaxMemo, "@interest_as_of_tax_memo");
            AddUpdateSet(sets, InterestAdjustment, "@interest_adjustment");

            var auditUpdate = Audit.BuildUpdateSetClause().Trim();
            if (auditUpdate.StartsWith(','))
            {
                auditUpdate = auditUpdate[1..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(auditUpdate))
            {
                sets.Add(auditUpdate);
            }

            var asAtPredicate = AsAtDate is not null
                ? $"""
                  and ((@original_as_at_date is null and [{AsAtDate}] is null)
                       or [{AsAtDate}] = @original_as_at_date)
                  """
                : string.Empty;

            return $"""
                update {TableName}
                set {string.Join(",\n                    ", sets)}
                where [{ExtLoanCode}] = @ext_loan_code
                {asAtPredicate}
                """;
        }

        public void AddInsertAuditParameters(SqlCommand command, string auditDisplayName, DateTime auditUtc) =>
            Audit.AddInsertParameters(command, auditDisplayName, auditUtc);

        public void AddUpdateAuditParameters(SqlCommand command, string auditDisplayName, DateTime auditUtc) =>
            Audit.AddUpdateParameters(command, auditDisplayName, auditUtc);

        private string BuildSelectList()
        {
            var parts = new List<string>
            {
                SelectAlias(ExtLoanCode, "ext_loan_code"),
                SelectAliasOrNull(LoanAliasName, "loan_alias_name", "varchar(200)"),
                SelectAliasOrNull(AsAtDate, "as_at_date", "date"),
                SelectAliasOrNull(ServicerId, "servicer_id", "varchar(100)"),
                SelectAliasOrNull(Description, "description", "varchar(500)"),
                SelectAliasOrNull(InvestorAliasName, "investor_alias_name", "varchar(200)"),
                SelectAliasOrNull(InvestorCode, "investor_code", "varchar(100)"),
                SelectAliasOrNull(DefaultDate, "default_date", "date"),
                SelectAliasOrNull(MaturityDate, "maturity_date", "date"),
                SelectAliasOrNull(InterestOffDate, "interest_off_date", "date"),
                SelectAliasOrNull(TaxMemoDate, "tax_memo_date", "date"),
                SelectAliasOrNull(SecurityValue, "security_value", "decimal(18, 2)"),
                SelectAliasOrNull(Units, "units", "int"),
                SelectAliasOrNull(NetAcres, "net_acres", "decimal(18, 4)"),
                SelectAliasOrNull(SquareFeet, "square_feet", "decimal(18, 2)"),
                SelectAliasOrNull(InterestRate, "interest_rate", "decimal(9, 4)"),
                SelectAliasOrNull(PrincipalBalance, "principal_balance", "decimal(18, 2)"),
                SelectAliasOrNull(OutstandingInterest, "outstanding_interest", "decimal(18, 2)"),
                SelectAliasOrNull(AccruedInterest, "accrued_interest", "decimal(18, 2)"),
                SelectAliasOrNull(LateInterest, "late_interest", "decimal(18, 2)"),
                SelectAliasOrNull(OutstandingInvoices, "outstanding_invoice", "decimal(18, 2)"),
                SelectAliasOrNull(EstRealizationCosts, "estimated_realization_costs", "decimal(18, 2)"),
                SelectAliasOrNull(CostToComplete, "cost_to_complete", "decimal(18, 2)"),
                SelectAliasOrNull(TaxArrears, "tax_arrears", "decimal(18, 2)"),
                SelectAliasOrNull(InterestAsOfTaxMemo, "interest_as_of_tax_memo", "decimal(18, 2)"),
                SelectAliasOrNull(InterestAdjustment, "interest_adjustment", "decimal(18, 2)"),
            };

            if (Audit.ReadUpdatedByColumn is not null)
            {
                parts.Add(SelectAlias(Audit.ReadUpdatedByColumn, "updated_by"));
            }
            else if (Audit.ReadCreatedByColumn is not null)
            {
                parts.Add(SelectAlias(Audit.ReadCreatedByColumn, "updated_by"));
            }
            else
            {
                parts.Add("cast('' as varchar(100)) as updated_by");
            }

            if (Audit.ReadUpdatedDtmColumn is not null)
            {
                parts.Add(SelectAlias(Audit.ReadUpdatedDtmColumn, "updated_datetime"));
            }
            else if (Audit.ReadCreatedDtmColumn is not null)
            {
                parts.Add(SelectAlias(Audit.ReadCreatedDtmColumn, "updated_datetime"));
            }
            else
            {
                parts.Add("cast(null as datetime2) as updated_datetime");
            }

            if (Audit.ReadCreatedByColumn is not null
                && !string.Equals(Audit.ReadCreatedByColumn, Audit.ReadUpdatedByColumn, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(SelectAlias(Audit.ReadCreatedByColumn, "created_by"));
            }
            else
            {
                parts.Add("cast('' as varchar(100)) as created_by");
            }

            if (Audit.ReadCreatedDtmColumn is not null
                && !string.Equals(Audit.ReadCreatedDtmColumn, Audit.ReadUpdatedDtmColumn, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(SelectAlias(Audit.ReadCreatedDtmColumn, "created_datetime"));
            }
            else
            {
                parts.Add("cast(null as datetime2) as created_datetime");
            }

            return string.Join(",\n                       ", parts);
        }

        private static string SelectAlias(string column, string alias) => $"{Bracket(column)} as {alias}";

        private static string SelectAliasOrNull(string? column, string alias, string sqlType) =>
            column is null
                ? $"cast(null as {sqlType}) as {alias}"
                : SelectAlias(column, alias);

        private static void AddWriteColumn(List<string> columns, List<string> values, string? column, string parameter)
        {
            if (column is not null)
            {
                columns.Add(column);
                values.Add(parameter);
            }
        }

        private static void AddUpdateSet(List<string> sets, string? column, string parameter)
        {
            if (column is not null)
            {
                sets.Add($"{Bracket(column)} = {parameter}");
            }
        }

        private static string Bracket(string column) => $"[{column}]";

        private static async Task<string> RequireFirstAsync(
            string connectionString,
            string tableName,
            IReadOnlyList<string> candidates,
            string label,
            CancellationToken cancellationToken)
        {
            var found = await FindFirstAsync(connectionString, tableName, candidates, cancellationToken);
            if (found is null)
            {
                throw new InvalidOperationException(
                    $"subjective_input.external_serviced_loan is missing a column for {label} (tried: {string.Join(", ", candidates)}).");
            }

            return found;
        }

        private static Task<string?> FindFirstAsync(
            string connectionString,
            string tableName,
            IReadOnlyList<string> candidates,
            CancellationToken cancellationToken) =>
            DimLoanColumnProbe.FindFirstAsync(connectionString, tableName, candidates, cancellationToken);
    }
}
