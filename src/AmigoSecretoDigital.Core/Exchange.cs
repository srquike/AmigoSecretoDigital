namespace AmigoSecretoDigital.Domain;

public sealed record Exchange(
    Guid Id,
    string Name,
    DateOnly EventDate,
    string Conditions,
    decimal? Budget,
    int ExpectedParticipants,
    Guid OrganizerId,
    ExchangeStatus Status,
    DateTimeOffset CreatedAt);
