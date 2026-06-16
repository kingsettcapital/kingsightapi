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
            if (item.NonKsServicedLoanKey <= 0)
            {
                return "Non-KS serviced loan key is required.";
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

            if (item.LoanName is { Length: > 200 })
            {
                return "Loan name must be 200 characters or fewer.";
            }

            return null;
        }
    }
}
