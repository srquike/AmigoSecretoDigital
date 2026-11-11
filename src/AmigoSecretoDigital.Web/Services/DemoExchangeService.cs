using System.Security.Cryptography;
using System.Text;
using AmigoSecretoDigital.Application;
using AmigoSecretoDigital.Application.Abstractions;
using AmigoSecretoDigital.Domain;

namespace AmigoSecretoDigital.Web.Services;

/// <summary>Adapts the in-memory prototype to the same application contract used by Supabase.</summary>
public sealed class DemoExchangeService(DemoFamilyService family) : IExchangeService
{
    public Task<Exchange> CreateAsync(CreateExchangeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = ExchangeRules.ValidateCreation(request.Name, request.EventDate, request.Conditions,
            request.Budget, request.ExpectedParticipants, DateOnly.FromDateTime(DateTime.Today));
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));

        var id = family.CreateDemoExchange(request.Name, request.EventDate, request.Conditions, request.Budget, request.ExpectedParticipants);
        return Task.FromResult(MapActiveExchange(id));
    }

    public Task<IReadOnlyList<ExchangeSummary>> GetExchangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ExchangeSummary> summaries = family.GetExchangeCards()
            .Select(card =>
            {
                family.SelectExchange(card.Id);
                return new ExchangeSummary(MapActiveExchange(card.Id), card.RegisteredCount);
            })
            .ToList();
        return Task.FromResult(summaries);
    }

    public Task<Exchange?> GetExchangeAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var card = family.GetExchangeCards().FirstOrDefault(item => ToGuid(item.Id) == exchangeId);
        if (card is null || !family.SelectExchange(card.Id)) return Task.FromResult<Exchange?>(null);
        return Task.FromResult<Exchange?>(MapActiveExchange(card.Id));
    }

    public Task DeleteAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectExchangeOrThrow(exchangeId);
        if (!family.DeleteActiveExchange())
            throw new KeyNotFoundException("Exchange not found.");

        return Task.CompletedTask;
    }

    public Task<Invitation> CreateInvitationAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectExchangeOrThrow(exchangeId);
        return Task.FromResult(new Invitation(family.ExchangeInvitePath, DateTimeOffset.UtcNow.AddDays(90)));
    }

    public Task<Invitation?> GetCurrentInvitationAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            SelectExchangeOrThrow(exchangeId);
            return Task.FromResult<Invitation?>(new Invitation(
                family.ExchangeInvitePath,
                DateTimeOffset.UtcNow.AddDays(90)));
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult<Invitation?>(null);
        }
    }

    public Task<InvitationContext?> GetInvitationContextAsync(string invitationToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!family.SelectExchangeByInviteCode(invitationToken) || family.RegistrationClosed ||
            family.RegisteredCount >= ExchangeRules.MaximumParticipants)
            return Task.FromResult<InvitationContext?>(null);

        return Task.FromResult<InvitationContext?>(new InvitationContext(
            ToGuid(family.ActiveExchangeId), family.ExchangeName, family.EventDate, family.Conditions,
            family.Budget, family.ExpectedParticipants, family.RegisteredCount));
    }

    public Task<ParticipantProfile> JoinAsync(JoinExchangeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = family.Join(request.InvitationToken, request.DisplayName, request.Age, request.Sex, request.ParticipantAccessPhrase);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
        var participant = family.Participants.Last();
        return Task.FromResult(new ParticipantProfile(participant.Id, GuidFromActive(), participant.DisplayName,
            participant.Age, participant.Sex, DateTimeOffset.UtcNow));
    }

    public Task<IReadOnlyList<AdminParticipant>> GetParticipantsAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectExchangeOrThrow(exchangeId);
        IReadOnlyList<AdminParticipant> participants = family.Participants
            .Where(person => person.IsRegistered)
            .Select(person => new AdminParticipant(person.Id, person.DisplayName, DateTimeOffset.UtcNow))
            .ToList();
        return Task.FromResult(participants);
    }

    public Task CloseRegistrationAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectExchangeOrThrow(exchangeId);
        if (!family.CloseRegistration()) throw new InvalidOperationException("Aún no se puede cerrar la inscripción.");
        return Task.CompletedTask;
    }

    public Task DrawAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectExchangeOrThrow(exchangeId);
        if (!family.Draw()) throw new InvalidOperationException("Todavía no se puede realizar el sorteo.");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AdminAssignment>> GetAssignmentsAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectExchangeOrThrow(exchangeId);
        IReadOnlyList<AdminAssignment> assignments = family.GetAssignments()
            .Select(item => new AdminAssignment(item.Participant.Id, item.Participant.DisplayName, item.RecipientName))
            .ToList();
        return Task.FromResult(assignments);
    }

    public Task<string> CreateRevealLinkAsync(Guid exchangeId, Guid participantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectExchangeOrThrow(exchangeId);
        return Task.FromResult(family.CreateRevealLink(participantId)
            ?? throw new InvalidOperationException("No se pudo generar el enlace de revelación."));
    }

    public Task<PrivateAssignment?> GetMyAssignmentAsync(string revealToken, string participantAccessPhrase,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var participant = family.FindParticipantByRevealCode(revealToken);
        if (participant is null || !family.VerifyPrivatePhrase(revealToken, participantAccessPhrase))
            return Task.FromResult<PrivateAssignment?>(null);

        var recipient = family.GetRecipientName(participant.Id);
        return Task.FromResult(recipient is null
            ? null
            : new PrivateAssignment(recipient, GiftPhraseCatalog.Pick()));
    }

    private void SelectExchangeOrThrow(Guid exchangeId)
    {
        var exchangeIdText = family.GetExchangeCards().FirstOrDefault(card => ToGuid(card.Id) == exchangeId)?.Id;
        if (exchangeIdText is null || !family.SelectExchange(exchangeIdText))
            throw new KeyNotFoundException("Exchange not found.");
    }

    private Exchange MapActiveExchange(string id)
    {
        var status = family.IsDrawn
            ? ExchangeStatus.Drawn
            : family.RegistrationClosed
                ? ExchangeStatus.ReadyForDraw
                : ExchangeStatus.RegistrationOpen;

        return new Exchange(ToGuid(id), family.ExchangeName, family.EventDate, family.Conditions,
            family.Budget, family.ExpectedParticipants, Guid.Empty, status, DateTimeOffset.UtcNow);
    }

    private Guid GuidFromActive() => ToGuid(family.ActiveExchangeId);

    private static Guid ToGuid(string value)
    {
        if (Guid.TryParse(value, out var id)) return id;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"amigo-secreto-demo:{value}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
