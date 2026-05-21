namespace kingsightapi.Entities
{
    public  class Fund
    {
        public int FundKey { get; init; }
        public int FundId { get; init; }
        public string FundCode { get; init; } = string.Empty;
        public string FundName { get; init; } = string.Empty;
        public string FundTypeName { get; init; } = string.Empty;
        public string FundStrategyName { get; init; } = string.Empty;
        public string JsFundName { get; init; } = string.Empty;
        public bool IsSidecar { get; init; }
        public DateTime? FundStartDate { get; init; }
    }

    public class FundDto
    {
        public int FundKey { get; init; }
        public int FundId { get; init; }
        public string FundCode { get; init; } = string.Empty;
        public string FundName { get; init; } = string.Empty;
        public string FundTypeName { get; init; } = string.Empty;
        public string FundStrategyName { get; init; } = string.Empty;
        public string JsFundName { get; init; } = string.Empty;
        public bool IsSidecar { get; init; }
        public DateTime? FundStartDate { get; init; }
    }
}