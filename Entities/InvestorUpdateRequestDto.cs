namespace kingsightapi.Entities
{
    // Single item received from Angular
    public sealed class InvestorUpdateRequestDto
    {
        // Use long to match your existing InvestorDto.InvestorKey
        public long InvestorKey { get; init; }

        // Previously InvestorAliasName; now use a nullable int Id (alias key)
        public int? InvestorAliasKey { get; init; }

        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    // Optional wrapper if Angular sends { "investors": [ ... ] }
    public sealed class InvestorUpdateBatchRequest
    {
        public List<InvestorUpdateRequestDto> Investors { get; init; } = new();
    }
}