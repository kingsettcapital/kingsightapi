namespace kingsightapi.Entities;



/// <summary>Optional period from the dropdown: daily filters by date_key; quarterly resolves quarter_year via dim_date.</summary>

public sealed class FundPeriodFilter

{

    public int? DateKey { get; init; }



    public bool HasDateKey => DateKey is > 0;

}

