namespace kingsightapi.Entities
{
    public static class DefaultSubjectiveAnalyticsTokens
    {
        public const string NotApplicable = "n/a";

        public static readonly IReadOnlyList<string> DefaultStatusOptions =
        [
            "Executing Plan",
            "Formulating Plan",
            "Waiting on Market",
            NotApplicable
        ];

        public static readonly IReadOnlyList<string> ExitPlanOptions =
        [
            "Sitting",
            "Constructing",
            "Pre-Development",
            "Selling",
            "Under Sale Contract",
            NotApplicable
        ];
    }

    public sealed class DefaultSubjectiveAnalyticsOptionDto
    {
        public string Value { get; init; } = string.Empty;
        public string DisplayLabel { get; init; } = string.Empty;
    }

    /// <summary>Combined dropdown lists for GET /api/DefaultSubjectiveAnalytics/lookups.</summary>
    public sealed class DefaultSubjectiveAnalyticsLookupsDto
    {
        public IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto> DefaultStatusOptions { get; init; } = [];
        public IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto> ExitPlanOptions { get; init; } = [];

        /// <summary>SPA alias: plain string list for mat-select.</summary>
        public IReadOnlyList<string> DefaultStatuses { get; init; } = [];
        public IReadOnlyList<string> ExitPlans { get; init; } = [];
    }

    public sealed class DefaultSubjectiveAnalyticsRowDto
    {
        public long LoanKey { get; init; }
        public string LoanId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public DateTime? MaturityDate { get; init; }
        public string? DefaultStatus { get; init; }
        public string? ExitPlan { get; init; }
        public string? ExitDate { get; init; }
        public string? MaturityAdditionalDetail { get; init; }

        /// <summary>SPA alias for <see cref="DefaultStatus"/>.</summary>
        public string? DefaultSubjectiveStatus => DefaultStatus;

        /// <summary>SPA alias for <see cref="ExitPlan"/>.</summary>
        public string? SubjectiveExitPlan => ExitPlan;

        /// <summary>SPA alias for <see cref="ExitDate"/>.</summary>
        public string? SubjectiveExitDate => ExitDate;

        public string? UserUpdatedBy { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
    }

    public sealed class DefaultSubjectiveAnalyticsUpdateItem
    {
        public long LoanKey { get; set; }
        public string LoanCode { get; set; } = string.Empty;
        public string? DefaultStatus { get; set; }
        public string? ExitPlan { get; set; }
        public string? ExitDate { get; set; }
        public string? MaturityAdditionalDetail { get; set; }

        /// <summary>Accepted on PUT when SPA sends column-style name.</summary>
        public string? DefaultSubjectiveStatus { get; set; }

        /// <summary>Accepted on PUT when SPA sends column-style name.</summary>
        public string? SubjectiveExitPlan { get; set; }

        /// <summary>Accepted on PUT when SPA sends column-style name.</summary>
        public string? SubjectiveExitDate { get; set; }

        public string UserUpdatedBy { get; set; } = "system";

        public string? ResolvedDefaultStatus => DefaultStatus ?? DefaultSubjectiveStatus;
        public string? ResolvedExitPlan => ExitPlan ?? SubjectiveExitPlan;
        public string? ResolvedExitDate => ExitDate ?? SubjectiveExitDate;
    }

    public sealed class DefaultSubjectiveAnalyticsBulkUpdateRequest
    {
        public List<DefaultSubjectiveAnalyticsUpdateItem> Loans { get; init; } = [];
    }
}
