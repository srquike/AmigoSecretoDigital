using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Supabase;

namespace AmigoSecretoDigital.Web.Services;

public static class OrganizerAuthenticationEndpoints
{
    public static void MapOrganizerAuthentication(this WebApplication app)
    {
        app.MapPost("/auth/sign-in", SignInAsync)
            .AllowAnonymous()
            .RequireRateLimiting("organizer-login");
        app.MapPost("/auth/sign-out", SignOutAsync).RequireAuthorization();
    }

    private static async Task<IResult> SignInAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        Client supabase,
        OrganizerSessionStore sessions,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("OrganizerAuthentication");
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            var form = await httpContext.Request.ReadFormAsync();
            var email = form["email"].ToString().Trim();
            var password = form["password"].ToString();

            if (email.Length is 0 or > 254 || password.Length is 0 or > 256)
                return Results.Redirect("/admin/login?error=1");

            var session = await supabase.Auth.SignIn(email, password);
            if (session?.User?.Id is null ||
                string.IsNullOrWhiteSpace(session.AccessToken) ||
                string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                return Results.Redirect("/admin/login?error=1");
            }

            var sessionId = sessions.Create(
                session.User.Id.ToString()!,
                session.User.Email,
                session.AccessToken,
                session.RefreshToken);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.User.Id.ToString()!),
                new(ClaimTypes.Name, session.User.Email ?? email),
                new(OrganizerClaims.SessionId, sessionId)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var properties = new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            };

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
            logger.LogInformation("Organizer signed in successfully.");
            return Results.Redirect("/admin");
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest("La sesión del formulario venció. Recarga la página e intenta de nuevo.");
        }
        catch (Exception exception)
        {
            logger.LogWarning("Organizer sign-in failed ({ExceptionType}).", exception.GetType().Name);
            return Results.Redirect("/admin/login?error=1");
        }
    }

    private static async Task<IResult> SignOutAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        OrganizerSessionStore sessions,
        ILoggerFactory loggerFactory)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest("La sesión del formulario venció. Recarga la página e intenta de nuevo.");
        }

        sessions.Remove(httpContext.User.FindFirstValue(OrganizerClaims.SessionId));
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        loggerFactory.CreateLogger("OrganizerAuthentication").LogInformation("Organizer signed out.");
        return Results.Redirect("/admin/login");
    }
}
