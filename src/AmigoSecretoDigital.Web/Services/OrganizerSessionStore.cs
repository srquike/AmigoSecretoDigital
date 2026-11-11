using System.Collections.Concurrent;

namespace AmigoSecretoDigital.Web.Services;

/// <summary>
/// Keeps Supabase refreshable sessions on the application server. The browser only receives
/// an opaque session id inside the protected ASP.NET Core authentication cookie.
/// </summary>
public sealed class OrganizerSessionStore
{
    private readonly ConcurrentDictionary<string, OrganizerSession> _sessions = new(StringComparer.Ordinal);

    public string Create(string userId, string? email, string accessToken, string refreshToken)
    {
        var sessionId = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        _sessions[sessionId] = new OrganizerSession(userId, email, accessToken, refreshToken);
        return sessionId;
    }

    public bool TryGet(string? sessionId, out OrganizerSession session)
    {
        if (sessionId is not null && _sessions.TryGetValue(sessionId, out var existing))
        {
            session = existing;
            return true;
        }

        session = default!;
        return false;
    }

    public bool Contains(string? sessionId) => sessionId is not null && _sessions.ContainsKey(sessionId);

    public void Update(string sessionId, string accessToken, string refreshToken)
    {
        if (_sessions.TryGetValue(sessionId, out var current))
            _sessions[sessionId] = current with { AccessToken = accessToken, RefreshToken = refreshToken };
    }

    public void Remove(string? sessionId)
    {
        if (sessionId is not null)
            _sessions.TryRemove(sessionId, out _);
    }
}

public sealed record OrganizerSession(
    string UserId,
    string? Email,
    string AccessToken,
    string RefreshToken);
