using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase;

namespace AmigoSecretoDigital.Web.Services;

/// <summary>Restores the current organizer's Supabase JWT into this Blazor circuit's client.</summary>
public sealed class AuthenticatedSupabaseClient(
    Client client,
    AuthenticationStateProvider authenticationStateProvider,
    OrganizerSessionStore sessions)
{
    private string? _restoredSessionId;
    private bool _refreshListenerRegistered;

    public Task<Client> GetPublicAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(client);
    }

    public async Task<Client> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var sessionId = authenticationState.User.FindFirstValue(OrganizerClaims.SessionId);

        if (sessionId is null ||
            authenticationState.User.Identity?.IsAuthenticated != true ||
            !sessions.TryGet(sessionId, out var session))
        {
            throw new UnauthorizedAccessException("Se requiere iniciar sesión como organizador.");
        }

        if (_restoredSessionId != sessionId)
        {
            await client.Auth.SetSession(session.AccessToken, session.RefreshToken);
            _restoredSessionId = sessionId;

            if (!_refreshListenerRegistered)
            {
                client.Auth.AddStateChangedListener((_, state) =>
                {
                    if (state.ToString() != "TokenRefreshed")
                        return;

                    var refreshedSession = client.Auth.CurrentSession;
                    if (refreshedSession is not null &&
                        !string.IsNullOrWhiteSpace(refreshedSession.AccessToken) &&
                        !string.IsNullOrWhiteSpace(refreshedSession.RefreshToken))
                        sessions.Update(sessionId, refreshedSession.AccessToken, refreshedSession.RefreshToken);
                });

                _refreshListenerRegistered = true;
            }
        }

        return client;
    }
}

public static class OrganizerClaims
{
    public const string SessionId = "amigo_secreto_session_id";
}
