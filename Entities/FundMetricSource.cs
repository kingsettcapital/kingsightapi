namespace kingsightapi.Entities;

/// <summary>Which fact table drives period options (commitments vs NAV).</summary>
public enum FundMetricSource
{
    Commitments,
    Nav,
    UnfundedCommitments,
    Investments,
    Distributions
}

public static class FundMetricSources
{
    public const string QueryValues = "commitments, nav, unfunded-commitments, investments, distributions";

    public static bool TryParse(string? value, out FundMetricSource source)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "commitments":
            case "commitment":
                source = FundMetricSource.Commitments;
                return true;
            case "nav":
                source = FundMetricSource.Nav;
                return true;
            case "unfunded-commitments":
            case "unfunded-commitment":
            case "unfunded":
                source = FundMetricSource.UnfundedCommitments;
                return true;
            case "investments":
            case "investment":
                source = FundMetricSource.Investments;
                return true;
            case "distributions":
            case "distribution":
                source = FundMetricSource.Distributions;
                return true;
            default:
                source = default;
                return false;
        }
    }
}
