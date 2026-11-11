using AmigoSecretoDigital.Domain;

namespace AmigoSecretoDigital.Application.Abstractions;

public interface IExchangeService
{
    Task<Exchange> CreateAsync(CreateExchangeRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExchangeSummary>> GetExchangesAsync(CancellationToken cancellationToken = default);

    Task<Exchange?> GetExchangeAsync(Guid exchangeId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid exchangeId, CancellationToken cancellationToken = default);

    Task<Invitation> CreateInvitationAsync(Guid exchangeId, CancellationToken cancellationToken = default);

    Task<Invitation?> GetCurrentInvitationAsync(Guid exchangeId, CancellationToken cancellationToken = default);

    Task<InvitationContext?> GetInvitationContextAsync(string invitationToken, CancellationToken cancellationToken = default);

    Task<ParticipantProfile> JoinAsync(JoinExchangeRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminParticipant>> GetParticipantsAsync(Guid exchangeId, CancellationToken cancellationToken = default);

    Task CloseRegistrationAsync(Guid exchangeId, CancellationToken cancellationToken = default);

    Task DrawAsync(Guid exchangeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminAssignment>> GetAssignmentsAsync(Guid exchangeId, CancellationToken cancellationToken = default);

    Task<string> CreateRevealLinkAsync(Guid exchangeId, Guid participantId, CancellationToken cancellationToken = default);

    Task<PrivateAssignment?> GetMyAssignmentAsync(
        string revealToken,
        string participantAccessPhrase,
        CancellationToken cancellationToken = default);
}

public sealed record CreateExchangeRequest(
    string Name,
    DateOnly EventDate,
    string Conditions,
    decimal? Budget,
    int ExpectedParticipants);

public sealed record JoinExchangeRequest(
    string InvitationToken,
    string DisplayName,
    int Age,
    Sex Sex,
    string ParticipantAccessPhrase);

public sealed record Invitation(string Url, DateTimeOffset ExpiresAt);

public sealed record ExchangeSummary(Exchange Exchange, int ParticipantCount);

public sealed record InvitationContext(
    Guid ExchangeId,
    string ExchangeName,
    DateOnly EventDate,
    string Conditions,
    decimal? Budget,
    int ExpectedParticipants,
    int ParticipantCount);

public sealed record AdminParticipant(Guid Id, string DisplayName, DateTimeOffset RegisteredAt);

public sealed record AdminAssignment(Guid ParticipantId, string ParticipantName, string RecipientName);

public sealed record PrivateAssignment(string RecipientName, string GiftPhrase);
