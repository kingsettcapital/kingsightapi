using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Configuration
{
    public static class WriteAuditUserExtensions
    {
        public static async Task<(string? DisplayName, ActionResult? Error)> RequireAuditDisplayNameAsync(
            this ICurrentUserResolver resolver,
            string? clientValue,
            string fieldName,
            CancellationToken cancellationToken = default)
        {
            var displayName = await resolver.ResolveDisplayNameAsync(cancellationToken);
            if (displayName is null)
            {
                return (null, new BadRequestObjectResult(CurrentUserResolver.NotRegisteredMessage));
            }

            resolver.LogIfClientDiffers(clientValue, displayName, fieldName);
            return (displayName, null);
        }
    }
}
