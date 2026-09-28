using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(@"c:\Code\kingsightapi")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();
var cs = config.GetConnectionString("FabricConnectionString")
    ?? throw new Exception("missing cs");
var db = config["FabricWarehouse:Database"] ?? "wh_gold";

await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
await using var cmd = conn.CreateCommand();
cmd.CommandTimeout = 180;
cmd.CommandText = $@"
select
    adv.date_of_advance,
    k.default_date,
    k.maturity_date,
    k.interest_off_date,
    k.days_in_default
from {db}.mortgage.fn_management_detail_key_dates(
    '2025-08-31', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'DEFAULT') k
outer apply (
    select min(per_loan.advance_dt) as date_of_advance
    from (
        select
            coalesce(
                min(case
                        when s.history_status = 'Initial Draw'
                         and isnull(s.actual_principal_amount, 0) <> 0
                        then s.accrual_post_date
                    end),
                min(case
                        when s.history_status = 'Draw'
                         and isnull(s.actual_principal_amount, 0) <> 0
                        then s.accrual_post_date
                    end)
            ) as advance_dt
        from {db}.mortgage.vw_loan_attributes v
        inner join {db}.mortgage.fact_amortization_schedule s
            on s.loan_code = v.loan_code
        where v.loan_alias_name = k.loan_alias_name
        group by v.loan_code
    ) per_loan
) adv
where k.loan_alias_name = 'Minoru';
";
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine($"date_of_advance={reader["date_of_advance"]}");
    Console.WriteLine($"default_date={reader["default_date"]}");
    Console.WriteLine($"maturity_date={reader["maturity_date"]}");
    Console.WriteLine($"interest_off={reader["interest_off_date"]}");
    Console.WriteLine($"days={reader["days_in_default"]}");
}

// Also LN5275-C alone
Console.WriteLine("--- LN5275-C per-loan ---");
await reader.DisposeAsync();
cmd.CommandText = $@"
select
    coalesce(
        min(case when history_status = 'Initial Draw' and isnull(actual_principal_amount,0) <> 0 then accrual_post_date end),
        min(case when history_status = 'Draw' and isnull(actual_principal_amount,0) <> 0 then accrual_post_date end)
    ) as advance_dt
from {db}.mortgage.fact_amortization_schedule
where loan_code = 'LN5275-C';
";
var dt = await cmd.ExecuteScalarAsync();
Console.WriteLine($"LN5275-C advance={dt}");
