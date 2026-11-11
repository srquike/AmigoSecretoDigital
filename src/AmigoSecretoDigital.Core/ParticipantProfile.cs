namespace AmigoSecretoDigital.Domain;

public enum Sex
{
    NotSpecified,
    Female,
    Male,
    Other
}

public sealed record ParticipantProfile(
    Guid Id,
    Guid ExchangeId,
    string DisplayName,
    int Age,
    Sex Sex,
    DateTimeOffset RegisteredAt);
