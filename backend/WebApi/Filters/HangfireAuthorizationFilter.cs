using Hangfire.Dashboard;
using Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace WebApi.Filters
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        private readonly HangfireSettings _settings;

        public HangfireAuthorizationFilter(IOptions<HangfireSettings> settings)
        {
            _settings = settings.Value;
        }

        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // Check for Basic Auth header
            var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                SetUnauthorizedResponse(httpContext);
                return false;
            }

            try
            {
                var authHeaderVal = AuthenticationHeaderValue.Parse(authHeader);
                if (authHeaderVal.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase)
                    && authHeaderVal.Parameter != null)
                {
                    var credentials = Encoding.UTF8
                        .GetString(Convert.FromBase64String(authHeaderVal.Parameter))
                        .Split(':', 2);

                    if (credentials.Length == 2)
                    {
                        var username = credentials[0];
                        var password = credentials[1];

                        if (ValidateCredentials(username, password))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Invalid auth header format
            }

            SetUnauthorizedResponse(httpContext);
            return false;
        }

        private bool ValidateCredentials(string username, string password)
        {
            // Compare using constant-time comparison to prevent timing attacks
            var expectedUsername = _settings.Dashboard.Username;
            var expectedPassword = _settings.Dashboard.Password;

            if (string.IsNullOrEmpty(expectedUsername) || string.IsNullOrEmpty(expectedPassword))
            {
                return false;
            }

            var usernameMatch = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(username),
                Encoding.UTF8.GetBytes(expectedUsername));

            var passwordMatch = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(password),
                Encoding.UTF8.GetBytes(expectedPassword));

            return usernameMatch && passwordMatch;
        }

        private static void SetUnauthorizedResponse(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = 401;
            httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\"";
        }
    }
}
