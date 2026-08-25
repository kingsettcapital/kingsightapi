using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Configuration
{
    public static class MortgageApproverExtensions
    {
        public const string LtvForbiddenMessage =
            "Mortgage User accounts are read-only for LTV Validation.";

        public const string AliasAssignmentForbiddenMessage =
            "Only Mortgage Super User accounts may perform this action.";

        /// <summary>
        /// LTV save/lock/unlock: allowed for every active role except Mortgage User.
        /// </summary>
        public static async Task<ActionResult?> RequireLtvEditorAsync(
            this ICurrentUserResolver resolver,
            IUserService userService,
            CancellationToken cancellationToken = default)
        {
            var (user, error) = await ResolveActiveUserAsync(resolver, userService, cancellationToken);
            if (error is not null)
            {
                return error;
            }

            if (IsMortgageUserRole(user!.RoleName))
            {
                return new ObjectResult(LtvForbiddenMessage)
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
            }

            return null;
        }

        /// <summary>
        /// Loan / Investor Alias Assignment mutations: Mortgage Super User only.
        /// </summary>
        public static async Task<ActionResult?> RequireMortgageSuperUserAsync(
            this ICurrentUserResolver resolver,
            IUserService userService,
            CancellationToken cancellationToken = default)
        {
            var (user, error) = await ResolveActiveUserAsync(resolver, userService, cancellationToken);
            if (error is not null)
            {
                return error;
            }

            if (!IsMortgageSuperUserRole(user!.RoleName))
            {
                return new ObjectResult(AliasAssignmentForbiddenMessage)
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
            }

            return null;
        }

        /// <summary>Backward-compatible alias for LTV editor gate. </summary>
        public static Task<ActionResult?> RequireMortgageApproverAsync(
            this ICurrentUserResolver resolver,
            IUserService userService,
            CancellationToken cancellationToken = default) =>
            resolver.RequireLtvEditorAsync(userService, cancellationToken);

        public static bool CanEditLtvValidation(string? roleName) =>
            !string.IsNullOrWhiteSpace(roleName) && !IsMortgageUserRole(roleName);

        public static bool CanEditAliasAssignment(string? roleName) =>
            IsMortgageSuperUserRole(roleName);

        public static bool IsAdminRole(string? roleName)
        {
            var normalized = NormalizeRoleName(roleName);
            if (normalized is "admin" or "administrator")
            {
                return true;
            }

            return normalized.EndsWith(" admin", StringComparison.Ordinal)
                || normalized.StartsWith("admin ", StringComparison.Ordinal)
                || normalized.Contains("administrator", StringComparison.Ordinal);
        }

        public static bool IsMortgageUserRole(string? roleName) =>
            NormalizeRoleName(roleName) == "mortgage user";

        public static bool IsMortgageSuperUserRole(string? roleName) =>
            NormalizeRoleName(roleName) == "mortgage super user";

        public static bool IsMortgageApproverRole(string? roleName) =>
            NormalizeRoleName(roleName) == "mortgage approver";

        private static async Task<(UserDto? user, ActionResult? error)> ResolveActiveUserAsync(
            ICurrentUserResolver resolver,
            IUserService userService,
            CancellationToken cancellationToken)
        {
            var email = resolver.GetJwtEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                return (null, new BadRequestObjectResult(CurrentUserResolver.NotRegisteredMessage));
            }

            var user = await userService.GetByEmailAsync(email, cancellationToken);
            if (user is null)
            {
                return (null, new BadRequestObjectResult(CurrentUserResolver.NotRegisteredMessage));
            }

            if (!user.IsActive)
            {
                return (
                    null,
                    new ObjectResult("Your user account is inactive.")
                    {
                        StatusCode = StatusCodes.Status403Forbidden,
                    });
            }

            return (user, null);
        }

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
