using AmigoSecretoDigital.Domain;

namespace AmigoSecretoDigital.Application;

public static class ExchangeRules
{
    public const int MinimumParticipants = 3;
    public const int MaximumParticipants = 100;
    public const int MaximumNameLength = 80;
    public const int MaximumConditionsLength = 2_000;
    public const int MinimumParticipantAge = 1;
    public const int MaximumParticipantAge = 120;
    public const int MinimumAccessPhraseLength = 5;
    public const int MaximumAccessPhraseLength = 10;

    public static IReadOnlyList<string> ValidateCreation(
        string? name,
        DateOnly eventDate,
        string? conditions,
        decimal? budget,
        int expectedParticipants,
        DateOnly today)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add("El intercambio necesita un nombre.");
        else if (name.Trim().Length > MaximumNameLength)
            errors.Add($"El nombre no puede exceder {MaximumNameLength} caracteres.");

        if (eventDate < today)
            errors.Add("La fecha del intercambio no puede estar en el pasado.");

        if (conditions?.Length > MaximumConditionsLength)
            errors.Add($"Las condiciones no pueden exceder {MaximumConditionsLength} caracteres.");

        if (budget is < 0)
            errors.Add("El presupuesto no puede ser negativo.");

        if (expectedParticipants is < MinimumParticipants or > MaximumParticipants)
            errors.Add($"La cantidad esperada debe estar entre {MinimumParticipants} y {MaximumParticipants} participantes.");

        return errors;
    }

    public static IReadOnlyList<string> ValidateParticipant(
        string? displayName,
        int age,
        Sex sex)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(displayName))
            errors.Add("Escribe tu nombre o apodo.");
        else if (displayName.Trim().Length > 60)
            errors.Add("El nombre o apodo no puede exceder 60 caracteres.");

        if (age is < MinimumParticipantAge or > MaximumParticipantAge)
            errors.Add("La edad debe estar entre 1 y 120 años.");

        if (!Enum.IsDefined(sex))
            errors.Add("Selecciona una opción válida para el sexo.");

        return errors;
    }

    public static IReadOnlyList<string> ValidateParticipantAccessPhrase(string? accessPhrase)
    {
        if (string.IsNullOrWhiteSpace(accessPhrase) ||
            accessPhrase.Trim().Length is < MinimumAccessPhraseLength or > MaximumAccessPhraseLength)
        {
            return [$"La frase privada debe tener entre {MinimumAccessPhraseLength} y {MaximumAccessPhraseLength} caracteres."];
        }

        return [];
    }

    public static IReadOnlyList<string> ValidateCanDraw(
        ExchangeStatus status,
        int participantCount)
    {
        var errors = new List<string>();

        if (status != ExchangeStatus.ReadyForDraw)
            errors.Add("Primero hay que cerrar la inscripción del intercambio.");

        if (participantCount < MinimumParticipants)
            errors.Add($"Se necesitan al menos {MinimumParticipants} participantes para sortear.");

        return errors;
    }
}
