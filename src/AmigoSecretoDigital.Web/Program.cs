using AmigoSecretoDigital.Web.Components;
using AmigoSecretoDigital.Web.Services;
using AmigoSecretoDigital.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;
using Supabase.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
{
    var logDirectory = Path.Combine(context.HostingEnvironment.ContentRootPath, "logs");

    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "AmigoSecretoDigital")
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logDirectory, "amigo-secreto-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            shared: true);
});

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<OrganizerSessionStore>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("organizer-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddScoped<DemoFamilyService>();

var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabasePublishableKey = builder.Configuration["Supabase:PublishableKey"];
var hasSupabaseUrl = !string.IsNullOrWhiteSpace(supabaseUrl);
var hasSupabaseKey = !string.IsNullOrWhiteSpace(supabasePublishableKey);

if (hasSupabaseUrl != hasSupabaseKey)
{
    throw new InvalidOperationException(
        "Configure both Supabase:Url and Supabase:PublishableKey, or leave both unset while using demo data.");
}

if (!hasSupabaseUrl && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Supabase must be configured outside the Development environment.");
}

if (hasSupabaseUrl && hasSupabaseKey)
{
    if (!Uri.TryCreate(supabaseUrl, UriKind.Absolute, out var supabaseUri) ||
        (supabaseUri.Scheme != Uri.UriSchemeHttps && supabaseUri.Scheme != Uri.UriSchemeHttp))
    {
        throw new InvalidOperationException("Supabase:Url must be a valid HTTP or HTTPS URL.");
    }

    builder.Services.AddSupabase(
        supabaseUrl!,
        supabasePublishableKey!,
        configureOptions: options => options.AutoRefreshToken = true);
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "AmigoSecreto.Organizer";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/admin/login";
            options.AccessDeniedPath = "/admin/login";
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = async context =>
            {
                var sessionStore = context.HttpContext.RequestServices.GetRequiredService<OrganizerSessionStore>();
                var sessionId = context.Principal?.FindFirst(OrganizerClaims.SessionId)?.Value;

                if (!sessionStore.Contains(sessionId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            };
        });
    builder.Services.AddScoped<AuthenticatedSupabaseClient>();
    builder.Services.AddScoped<IExchangeService, SupabaseExchangeService>();
}
else
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie();
    builder.Services.AddScoped<IExchangeService, DemoExchangeService>();
}

builder.Services.AddSingleton<ExchangeInvitationCache>();

var app = builder.Build();

if (!hasSupabaseUrl)
{
    app.Logger.LogInformation("Application started in demo mode without Supabase persistence.");
}
else
{
    app.Logger.LogInformation("Application started with Supabase persistence enabled.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

if (hasSupabaseUrl && hasSupabaseKey)
    app.MapOrganizerAuthentication();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
