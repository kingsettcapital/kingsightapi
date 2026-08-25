using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Configuration
{
    public static class MortgageApproverExtensions
    {
        public const string ForbiddenMessage =
            "Only Admin or Mortgage Approver users may perform this action.";

        /// <summary>
        /// Admin has full LTV rights. Mortgage Approver may save/lock/unlock.
        /// All other roles are rejected.
        /// </summary>
        public static async Task<ActionResult?> RequireMortgageApproverAsync(
            this ICurrentUserResolver resolver,
            IUserService userService,
            CancellationToken cancellationToken = default)
        {
            var email = resolver.GetJwtEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                return new BadRequestObjectResult(CurrentUserResolver.NotRegisteredMessage);
            }

            var user = await userService.GetByEmailAsync(email, cancellationToken);
            if (user is null)
            {
                return new BadRequestObjectResult(CurrentUserResolver.NotRegisteredMessage);
            }

            if (!user.IsActive)
            {
                return new ObjectResult("Your user account is inactive.")
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
            }

            // Admin always allowed — independent of Mortgage Approver / other role gates.
            if (IsAdminRole(user.RoleName))
            {
                return null;
            }

            if (IsMortgageApproverRole(user.RoleName))
            {
                return null;
            }

            return new ObjectResult(ForbiddenMessage) { StatusCode = StatusCodes.Status403Forbidden };
        }

        public static bool CanEditLtvValidation(string? roleName) =>
            IsAdminRole(roleName) || IsMortgageApproverRole(roleName);

        public static bool IsAdminRole(string? roleName)
        {
            var normalized = NormalizeRoleName(roleName);
            if (normalized is "admin" or "administrator")
            {
                return true;
            }

            // Tolerate variants such as "KS Admin" / "System Administrator".
            return normalized.EndsWith(" admin", StringComparison.Ordinal)
                || normalized.StartsWith("admin ", StringComparison.Ordinal)
                || normalized.Contains("administrator", StringComparison.Ordinal);
        }

        public static bool IsMortgageApproverRole(string? roleName) =>
            NormalizeRoleName(roleName) == "mortgage approver";

        private static string NormalizeRoleName(string? roleName)
        {
            var normalized = (roleName ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace('_', ' ')
                .Replace('-', ' ');

            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized;
        }
    }
}
