using System.Security.Cryptography;
using System.Text;
using AmigoSecretoDigital.Application;
using AmigoSecretoDigital.Domain;

namespace AmigoSecretoDigital.Web.Services;

/// <summary>Clickable prototype data only. Tokens, phrases, and assignments are not production security.</summary>
public sealed class DemoFamilyService
{
    private readonly Dictionary<string, DemoExchangeState> _exchanges = new(StringComparer.Ordinal);

    public DemoFamilyService()
    {
        var christmas = CreateState("navidad-familia", "Navidad en familia", new(2026, 12, 24), 25, 8,
            "Un regalo bonito, hecho o comprado con cariño. Tope de $25.", "demo-navidad-familia-2026");
        AddSeedParticipant(christmas, "María", 32, Sex.Female, "MR");
        AddSeedParticipant(christmas, "Carlos", 36, Sex.Male, "CM");
        AddSeedParticipant(christmas, "Abuela Rosa", 68, Sex.Female, "AR");
        AddSeedParticipant(christmas, "Claudia", 41, Sex.Female, "CL");
        AddSeedParticipant(christmas, "Diego", 17, Sex.Male, "DG");
        AddSeedParticipant(christmas, "Sofía", 25, Sex.Female, "SF");
        _exchanges.Add(christmas.Id, christmas);

        var newYear = CreateState("nochevieja", "Amigo Secreto de Año Nuevo", new(2026, 12, 31), 20, 6,
            "Un detalle hecho con cariño. Presupuesto máximo de $20.", "demo-intercambio-ano-nuevo-2026");
        AddSeedParticipant(newYear, "Roberto", 44, Sex.Male, "RB");
        AddSeedParticipant(newYear, "Elena", 39, Sex.Female, "EL");
        AddSeedParticipant(newYear, "Mateo", 21, Sex.Male, "MT");
        AddSeedParticipant(newYear, "Lucía", 19, Sex.Female, "LU");
        _exchanges.Add(newYear.Id, newYear);

        var lastYear = CreateState("navidad-2025", "Navidad en familia 2025", new(2025, 12, 24), 15, 6,
            "Intercambio familiar de diciembre.", "demo-navidad-familia-2025");
        AddSeedParticipant(lastYear, "María", 32, Sex.Female, "MR");
        AddSeedParticipant(lastYear, "Carlos", 36, Sex.Male, "CM");
        AddSeedParticipant(lastYear, "Rosa", 68, Sex.Female, "RO");
        AddSeedParticipant(lastYear, "Claudia", 41, Sex.Female, "CL");
        AddSeedParticipant(lastYear, "Diego", 17, Sex.Male, "DG");
        AddSeedParticipant(lastYear, "Sofía", 25, Sex.Female, "SF");
        lastYear.RegistrationClosed = true;
        for (var index = 0; index < lastYear.Participants.Count; index++)
            lastYear.Assignments[lastYear.Participants[index].Id] = lastYear.Participants[(index + 1) % lastYear.Participants.Count].Id;
        _exchanges.Add(lastYear.Id, lastYear);

        ActiveExchangeId = christmas.Id;
    }

    private DemoExchangeState Active => _exchanges[ActiveExchangeId];

    public string ActiveExchangeId { get; private set; } = string.Empty;
    public string ExchangeName => Active.Name;
    public string OrganizerName => "María";
    public string ExchangeInviteCode => Active.InviteCode;
    public string ExchangeInvitePath => $"/join/{Active.InviteCode}";
    public DateOnly EventDate => Active.EventDate;
    public decimal Budget => Active.Budget;
    public string Conditions => Active.Conditions;
    public bool RegistrationClosed => Active.RegistrationClosed;
    public bool IsDrawn => Active.Assignments.Count > 0;
    public int ExpectedParticipants => Active.ExpectedParticipants;
    public IReadOnlyList<DemoParticipant> Participants => Active.Participants;
    public int RegisteredCount => Active.Participants.Count(person => person.IsRegistered);
    public int DaysUntilEvent => Math.Max(0, (EventDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber));
    public int ProgressPercent => ExpectedParticipants == 0
        ? 0
        : Math.Min(100, (int)Math.Round(RegisteredCount * 100d / ExpectedParticipants));

    public IReadOnlyList<DemoExchangeCard> GetExchangeCards() => _exchanges.Values
        .Select(state => new DemoExchangeCard(
            state.Id,
            state.Name,
            state.EventDate,
            state.Budget,
            state.Participants.Count(person => person.IsRegistered),
            state.ExpectedParticipants,
            GetStatusLabel(state),
            GetStatusClass(state),
            state.Id == ActiveExchangeId))
        .OrderBy(card => card.EventDate < DateOnly.FromDateTime(DateTime.Today) ? 1 : 0)
        .ThenBy(card => card.EventDate)
        .ToList();

    public bool SelectExchange(string? exchangeId)
    {
        if (exchangeId is null || !_exchanges.ContainsKey(exchangeId))
            return false;

        ActiveExchangeId = exchangeId;
        return true;
    }

    public bool SelectExchangeByInviteCode(string? inviteCode)
    {
        var state = _exchanges.Values.FirstOrDefault(exchange => exchange.InviteCode == inviteCode);
        if (state is null)
            return false;

        ActiveExchangeId = state.Id;
        return true;
    }

    public string AvatarTone(Guid participantId)
    {
        var palette = new[] { "avatar-green", "avatar-sand", "avatar-rose", "avatar-blue" };
        var index = Active.Participants.FindIndex(person => person.Id == participantId);
        return palette[Math.Max(0, index) % palette.Length];
    }

    public IReadOnlyList<string> Join(
        string? exchangeInviteCode,
        string displayName,
        int age,
        Sex sex,
        string accessPhrase)
    {
        if (!SelectExchangeByInviteCode(exchangeInviteCode))
            return ["Este enlace de intercambio no es válido."];

        var errors = ExchangeRules.ValidateParticipant(displayName, age, sex).ToList();
        if (RegistrationClosed)
            errors.Add("Las inscripciones de este intercambio ya están cerradas.");
        if (RegisteredCount >= ExchangeRules.MaximumParticipants)
            errors.Add($"El intercambio alcanzó el máximo de {ExchangeRules.MaximumParticipants} participantes.");
        if (Active.Participants.Any(person => person.IsRegistered && string.Equals(person.DisplayName, displayName.Trim(), StringComparison.OrdinalIgnoreCase)))
            errors.Add("Ya hay una persona registrada con ese nombre o apodo.");
        errors.AddRange(ExchangeRules.ValidateParticipantAccessPhrase(accessPhrase));
        if (errors.Count != 0)
            return errors;

        var participant = new DemoParticipant(
            Guid.NewGuid(), displayName.Trim(), age, sex, true,
            string.Concat(displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0]))),
            HashPhrase(accessPhrase));
        Active.Participants.Add(participant);
        return [];
    }

    public bool CloseRegistration()
    {
        if (RegistrationClosed || RegisteredCount < ExchangeRules.MinimumParticipants)
            return false;

        Active.RegistrationClosed = true;
        return true;
    }

    public bool Draw()
    {
        if (!RegistrationClosed || RegisteredCount < ExchangeRules.MinimumParticipants || IsDrawn)
            return false;

        var participants = Active.Participants.Where(person => person.IsRegistered).ToList();
        for (var index = participants.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (participants[index], participants[swapIndex]) = (participants[swapIndex], participants[index]);
        }

        for (var index = 0; index < participants.Count; index++)
            Active.Assignments[participants[index].Id] = participants[(index + 1) % participants.Count].Id;

        return true;
    }

    public IReadOnlyList<DemoAssignment> GetAssignments() => Active.Participants
        .Where(person => person.IsRegistered && Active.Assignments.ContainsKey(person.Id))
        .Select(person => new DemoAssignment(person, GetRecipientName(person.Id) ?? ""))
        .ToList();

    public string? CreateRevealLink(Guid participantId)
    {
        if (!IsDrawn || !Active.Assignments.ContainsKey(participantId))
            return null;

        var participant = Active.Participants.First(person => person.Id == participantId);
        participant.RevealCode = CreateToken();
        return $"/reveal/{participant.RevealCode}";
    }

    public DemoParticipant? FindParticipantByRevealCode(string? revealCode)
    {
        foreach (var exchange in _exchanges.Values)
        {
            var participant = exchange.Participants.FirstOrDefault(person => person.IsRegistered && person.RevealCode == revealCode);
            if (participant is null) continue;
            ActiveExchangeId = exchange.Id;
            return participant;
        }

        return null;
    }

    public bool VerifyPrivatePhrase(string? revealCode, string? accessPhrase)
    {
        var participant = FindParticipantByRevealCode(revealCode);
        if (participant?.AccessPhraseHash is null || string.IsNullOrWhiteSpace(accessPhrase))
            return false;

        var candidateHash = HashPhrase(accessPhrase);
        return CryptographicOperations.FixedTimeEquals(candidateHash, participant.AccessPhraseHash);
    }

    public string? GetRecipientName(Guid participantId)
    {
        if (!Active.Assignments.TryGetValue(participantId, out var recipientId))
            return null;

        return Active.Participants.FirstOrDefault(person => person.Id == recipientId)?.DisplayName;
    }

    public string CreateDemoExchange(string name, DateOnly eventDate, string conditions, decimal? budget, int expectedParticipants)
    {
        var id = Guid.NewGuid().ToString("N");
        var state = CreateState(id, name.Trim(), eventDate, budget ?? 0, expectedParticipants, conditions.Trim(), CreateToken());
        AddSeedParticipant(state, "María", 32, Sex.Female, "MR");
        _exchanges.Add(id, state);
        ActiveExchangeId = id;
        return id;
    }

    private static DemoExchangeState CreateState(string id, string name, DateOnly eventDate, decimal budget, int expectedParticipants, string conditions, string inviteCode) =>
        new(id, name, eventDate, budget, expectedParticipants, conditions, inviteCode);

    private static void AddSeedParticipant(DemoExchangeState state, string displayName, int age, Sex sex, string initials) =>
        state.Participants.Add(new DemoParticipant(Guid.NewGuid(), displayName, age, sex, true, initials, HashPhrase("frase-demo-2026")));

    private static string GetStatusLabel(DemoExchangeState state)
    {
        if (state.EventDate < DateOnly.FromDateTime(DateTime.Today)) return "Finalizado";
        if (state.Assignments.Count > 0) return "Sorteo realizado";
        if (state.RegistrationClosed) return "Inscripción cerrada";
        return "Inscripciones abiertas";
    }

    private static string GetStatusClass(DemoExchangeState state)
    {
        if (state.EventDate < DateOnly.FromDateTime(DateTime.Today)) return "status-complete";
        if (state.Assignments.Count > 0) return "status-drawn";
        if (state.RegistrationClosed) return "status-ready";
        return "status-open";
    }

    private static byte[] HashPhrase(string phrase) => SHA256.HashData(Encoding.UTF8.GetBytes(phrase.Trim()));

    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private sealed class DemoExchangeState(
        string id,
        string name,
        DateOnly eventDate,
        decimal budget,
        int expectedParticipants,
        string conditions,
        string inviteCode)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public DateOnly EventDate { get; } = eventDate;
        public decimal Budget { get; } = budget;
        public int ExpectedParticipants { get; } = expectedParticipants;
        public string Conditions { get; } = conditions;
        public string InviteCode { get; } = inviteCode;
        public bool RegistrationClosed { get; set; }
        public List<DemoParticipant> Participants { get; } = [];
        public Dictionary<Guid, Guid> Assignments { get; } = [];
    }
}

public sealed class DemoParticipant(
    Guid id,
    string displayName,
    int age,
    Sex sex,
    bool isRegistered,
    string initials,
    byte[]? accessPhraseHash)
{
    public Guid Id { get; } = id;
    public string DisplayName { get; set; } = displayName;
    public int Age { get; set; } = age;
    public Sex Sex { get; set; } = sex;
    public bool IsRegistered { get; set; } = isRegistered;
    public string Initials { get; } = initials;
    public byte[]? AccessPhraseHash { get; } = accessPhraseHash;
    public string? RevealCode { get; set; }
}

public sealed record DemoAssignment(DemoParticipant Participant, string RecipientName);

public sealed record DemoExchangeCard(
    string Id,
    string Name,
    DateOnly EventDate,
    decimal Budget,
    int RegisteredCount,
    int ExpectedParticipants,
    string StatusLabel,
    string StatusClass,
    bool IsActive);
