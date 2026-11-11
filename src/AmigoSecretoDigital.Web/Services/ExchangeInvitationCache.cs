using System.Collections.Concurrent;
using AmigoSecretoDigital.Application.Abstractions;

namespace AmigoSecretoDigital.Web.Services;

/// <summary>Retains issued invitation links in server memory so organizers can reopen their exchange page.</summary>
public sealed class ExchangeInvitationCache
{
    private readonly ConcurrentDictionary<(Guid OrganizerId, Guid ExchangeId), Invitation> _links = new();

    public void Set(Guid organizerId, Guid exchangeId, Invitation invitation) => _links[(organizerId, exchangeId)] = invitation;

    public bool TryGet(Guid organizerId, Guid exchangeId, out Invitation invitation) =>
        _links.TryGetValue((organizerId, exchangeId), out invitation!);
}
