using kingsightapi.Entities;

namespace kingsightapi.Services
{
    internal static class NonKsServicedLoansValidation
    {
        public static string? ValidateCreateItem(NonKsServicedLoanCreateItem item)
        {
            if (string.IsNullOrWhiteSpace(item.UserUpdatedBy))
            {
                return "User updated by is required.";
            }

            return ValidateRatesAndLengths(item);
        }

        public static string? ValidateUpdateItem(NonKsServicedLoanUpdateItem item)
        {
            if (string.IsNullOrWhiteSpace(item.LoanId))
            {
                return "Loan ID is required for update.";
            }

            return ValidateCreateItem(item);
        }

        private static string? ValidateRatesAndLengths(NonKsServicedLoanCreateItem item)
        {
            if (item.InterestRate is < 0 or > 100)
            {
                return "Interest rate must be between 0 and 100.";
            }

            if (item.LoanId is { Length: > 100 })
            {
                return "Loan ID must be 100 characters or fewer.";
            }

            if (item.ServicerId is { Length: > 100 })
            {
                return "Servicer ID must be 100 characters or fewer.";
            }

            if (item.LoanName is { Length: > 200 } || item.LoanAliasName is { Length: > 200 })
            {
                return "Loan alias must be 200 characters or fewer.";
            }

            return null;
        }
    }
}
