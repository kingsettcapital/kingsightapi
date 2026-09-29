using System.Security.Claims;
using kingsightapi.Entities;

namespace kingsightapi.Services
{
    public interface ICurrentUserResolver
    {
        string? GetJwtEmail();

        Task<string?> ResolveDisplayNameAsync(CancellationToken cancellationToken = default);

        void LogIfClientDiffers(string? clientValue, string serverDisplayName, string fieldName);
    }

    public sealed class CurrentUserResolver : ICurrentUserResolver
    {
        public const string NotRegisteredMessage =
            "Unable to resolve the current user. Sign in with a Kingsight account registered in User Management.";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserService _userService;
        private readonly ILogger<CurrentUserResolver> _logger;

        public CurrentUserResolver(
            IHttpContextAccessor httpContextAccessor,
            IUserService userService,
            ILogger<CurrentUserResolver> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _userService = userService;
            _logger = logger;
        }

        public string? GetJwtEmail()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return null;
            }

            return user.FindFirstValue("preferred_username")
                ?? user.FindFirstValue(ClaimTypes.Upn)
                ?? user.Identity?.Name;
        }

        public async Task<string?> ResolveDisplayNameAsync(CancellationToken cancellationToken = default)
        {
            var email = GetJwtEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var user = await _userService.GetByEmailAsync(email, cancellationToken);
            return user is null ? null : UserDisplayNameFormatter.Format(user);
        }

        public void LogIfClientDiffers(string? clientValue, string serverDisplayName, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(clientValue))
            {
                return;
            }

            if (string.Equals(clientValue.Trim(), serverDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _logger.LogWarning(
                "Ignoring client {FieldName} '{ClientValue}'; JWT user display name '{ServerDisplayName}' will be used.",
                fieldName,
                clientValue.Trim(),
                serverDisplayName);
        }
    }

    internal static class UserDisplayNameFormatter
    {
        public static string Format(UserDto user)
        {
            var name = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? user.Email : name;
        }
    }
}
