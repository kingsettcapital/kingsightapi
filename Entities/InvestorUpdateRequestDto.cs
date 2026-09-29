namespace kingsightapi.Entities
{
    // Single item received from Angular
    public sealed class InvestorUpdateRequestDto
    {
        public long InvestorKey { get; init; }

        public string InvestorCode { get; init; } = string.Empty;

        public int? InvestorAliasKey { get; init; }

        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    // Optional wrapper if Angular sends { "investors": [ ... ] }
    public sealed class InvestorUpdateBatchRequest
    {
        public List<InvestorUpdateRequestDto> Investors { get; init; } = new();
    }

    public sealed class InvestorCreateRequest
    {
        public string InvestorName { get; init; } = string.Empty;
        public string? CreatedBy { get; init; }
    }
}