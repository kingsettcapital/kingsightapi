using kingsightapi.Entities;

namespace kingsightapi.Services
{
    internal static class DefaultSubjectiveAnalyticsValidation
    {
        private static readonly HashSet<string> DefaultStatusSet = new(
            DefaultSubjectiveAnalyticsTokens.DefaultStatusOptions,
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ExitPlanSet = new(
            DefaultSubjectiveAnalyticsTokens.ExitPlanOptions,
            StringComparer.OrdinalIgnoreCase);

        /// <summary>Known SPA typos mapped to mockup values before validate/save.</summary>
        private static readonly Dictionary<string, string> ExitPlanAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Siting"] = "Selling"
        };

        public static string? CanonicalizeDefaultStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        public static string? CanonicalizeExitPlan(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return ExitPlanAliases.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
        }

        public static string? CanonicalizeExitDate(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static string? ValidateUpdateItem(DefaultSubjectiveAnalyticsUpdateItem item)
        {
            if (item.LoanKey <= 0)
            {
                return "Loan key is required.";
            }

            if (string.IsNullOrWhiteSpace(item.UserUpdatedBy))
            {
                return "User updated by is required.";
            }

            var defaultStatus = CanonicalizeDefaultStatus(item.ResolvedDefaultStatus);
            if (defaultStatus is not null && !DefaultStatusSet.Contains(defaultStatus))
            {
                return $"Invalid default status: {defaultStatus}. Allowed: {string.Join(", ", DefaultSubjectiveAnalyticsTokens.DefaultStatusOptions)}.";
            }

            var exitPlan = CanonicalizeExitPlan(item.ResolvedExitPlan);
            if (exitPlan is not null && !ExitPlanSet.Contains(exitPlan))
            {
                return $"Invalid exit plan: {item.ResolvedExitPlan}. Allowed: {string.Join(", ", DefaultSubjectiveAnalyticsTokens.ExitPlanOptions)}.";
            }

            var exitDate = CanonicalizeExitDate(item.ResolvedExitDate);
            if (exitDate is { Length: > 100 })
            {
                return "Exit date must be 100 characters or fewer.";
            }

            if (item.MaturityAdditionalDetail is { Length: > 500 })
            {
                return "Maturity additional detail must be 500 characters or fewer.";
            }

            return null;
        }

        public static IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto> ToOptions(
            IReadOnlyList<string> values) =>
            values.Select(v => new DefaultSubjectiveAnalyticsOptionDto
            {
                Value = v,
                DisplayLabel = v
            }).ToList();
    }
}
