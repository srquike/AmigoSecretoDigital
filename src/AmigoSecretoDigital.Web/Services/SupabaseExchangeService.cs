using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AmigoSecretoDigital.Application;
using AmigoSecretoDigital.Application.Abstractions;
using AmigoSecretoDigital.Domain;
using Supabase.Postgrest.Exceptions;

namespace AmigoSecretoDigital.Web.Services;

public sealed class SupabaseExchangeService(
    AuthenticatedSupabaseClient authenticatedClient,
    InvitationTokenProtector invitationTokenProtector,
    ILogger<SupabaseExchangeService> logger) : IExchangeService
{
    public async Task<Exchange> CreateAsync(CreateExchangeRequest request, CancellationToken cancellationToken = default)
    {
        var errors = ExchangeRules.ValidateCreation(
            request.Name,
            request.EventDate,
            request.Conditions,
            request.Budget,
            request.ExpectedParticipants,
            DateOnly.FromDateTime(DateTime.Today));

        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        var client = await authenticatedClient.GetAsync(cancellationToken);
        var row = await QuerySingleAsync<ExchangeRow>(client, "create_exchange", new
        {
            p_name = request.Name.Trim(),
            p_event_date = request.EventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            p_conditions = request.Conditions.Trim(),
            p_budget = request.Budget,
            p_expected_participants = request.ExpectedParticipants
        });

        var exchange = MapExchange(row);
        logger.LogInformation(
            "Exchange {ExchangeId} created with expected participant count {ExpectedParticipantCount}.",
            exchange.Id,
            exchange.ExpectedParticipants);
        return exchange;
    }

    public async Task<IReadOnlyList<ExchangeSummary>> GetExchangesAsync(CancellationToken cancellationToken = default)
    {
        var client = await authenticatedClient.GetAsync(cancellationToken);
        var rows = await QueryAsync<ExchangeRow>(client, "list_admin_exchanges", new { });
        return rows.Select(row => new ExchangeSummary(MapExchange(row), row.ParticipantCount)).ToList();
    }

    public async Task<Exchange?> GetExchangeAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        var client = await authenticatedClient.GetAsync(cancellationToken);
        var row = await QuerySingleOrDefaultAsync<ExchangeRow>(client, "get_admin_exchange", new { p_exchange_id = exchangeId });
        return row is null ? null : MapExchange(row);
    }

    public async Task DeleteAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        var client = await authenticatedClient.GetAsync(cancellationToken);
        await client.Rpc("delete_exchange", new { p_exchange_id = exchangeId });
        logger.LogInformation("Exchange {ExchangeId} and its related records deleted.", exchangeId);
    }

    public async Task<Invitation> CreateInvitationAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        var token = CreateToken();
        var client = await authenticatedClient.GetAsync(cancellationToken);
        await client.Rpc("rotate_invitation_token", new
        {
            p_exchange_id = exchangeId,
            p_token_hash = HashToken(token),
            p_token_ciphertext = invitationTokenProtector.Protect(exchangeId, token)
        });

        logger.LogInformation("Shared invitation link rotated for exchange {ExchangeId}.", exchangeId);
        return new Invitation($"/join/{token}", DateTimeOffset.UtcNow.AddDays(90));
    }

    public async Task<Invitation?> GetCurrentInvitationAsync(
        Guid exchangeId,
        CancellationToken cancellationToken = default)
    {
        var client = await authenticatedClient.GetAsync(cancellationToken);
        var row = await QuerySingleOrDefaultAsync<AdminInvitationTokenRow>(
            client,
            "get_admin_invitation_token",
            new { p_exchange_id = exchangeId });

        if (row is null)
            return null;

        var token = invitationTokenProtector.Unprotect(exchangeId, row.TokenCiphertext);
        return new Invitation($"/join/{token}", row.ExpiresAt);
    }

    public async Task<InvitationContext?> GetInvitationContextAsync(
        string invitationToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invitationToken))
            return null;

        var client = await authenticatedClient.GetPublicAsync(cancellationToken);
        var row = await QuerySingleOrDefaultAsync<InvitationContextRow>(
            client,
            "get_invitation_context",
            new { p_invitation_token = invitationToken });

        return row is null
            ? null
            : new InvitationContext(
                row.ExchangeId,
                row.ExchangeName,
                ParseDate(row.EventDate),
                row.Conditions,
                row.Budget,
                row.ExpectedParticipants,
                row.ParticipantCount);
    }

    public async Task<ParticipantProfile> JoinAsync(JoinExchangeRequest request, CancellationToken cancellationToken = default)
    {
        var errors = ExchangeRules.ValidateParticipant(request.DisplayName, request.Age, request.Sex).ToList();
        if (string.IsNullOrWhiteSpace(request.InvitationToken))
            errors.Add("Este enlace de intercambio no es válido.");
        errors.AddRange(ExchangeRules.ValidateParticipantAccessPhrase(request.ParticipantAccessPhrase));

        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        var client = await authenticatedClient.GetPublicAsync(cancellationToken);
        var row = await QuerySingleAsync<ParticipantIdRow>(client, "register_participant", new
        {
            p_invitation_token = request.InvitationToken,
            p_display_name = request.DisplayName.Trim(),
            p_age = request.Age,
            p_sex = request.Sex.ToString(),
            p_private_phrase = request.ParticipantAccessPhrase.Trim()
        });

        logger.LogInformation("Participant registered for exchange {ExchangeId}.", row.ExchangeId);
        return new ParticipantProfile(
            row.ParticipantId,
            row.ExchangeId,
            request.DisplayName.Trim(),
            request.Age,
            request.Sex,
            row.RegisteredAt);
    }

    public async Task<IReadOnlyList<AdminParticipant>> GetParticipantsAsync(
        Guid exchangeId,
        CancellationToken cancellationToken = default)
    {
        var client = await authenticatedClient.GetAsync(cancellationToken);
        var rows = await QueryAsync<ParticipantRow>(client, "list_admin_participants", new { p_exchange_id = exchangeId });
        return rows.Select(row => new AdminParticipant(row.ParticipantId, row.DisplayName, row.RegisteredAt)).ToList();
    }

    public async Task CloseRegistrationAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        var client = await authenticatedClient.GetAsync(cancellationToken);
        await client.Rpc("close_registration", new { p_exchange_id = exchangeId });
        logger.LogInformation("Registration closed for exchange {ExchangeId}.", exchangeId);
    }

    public async Task DrawAsync(Guid exchangeId, CancellationToken cancellationToken = default)
    {
        var client = await authenticatedClient.GetAsync(cancellationToken);
        await client.Rpc("draw_exchange", new { p_exchange_id = exchangeId });
        logger.LogInformation("Draw completed for exchange {ExchangeId}.", exchangeId);
    }

    public async Task<IReadOnlyList<AdminAssignment>> GetAssignmentsAsync(
        Guid exchangeId,
        CancellationToken cancellationToken = default)
    {
        var client = await authenticatedClient.GetAsync(cancellationToken);
        var rows = await QueryAsync<AdminAssignmentRow>(client, "get_admin_assignments", new { p_exchange_id = exchangeId });
        return rows.Select(row => new AdminAssignment(row.ParticipantId, row.ParticipantName, row.RecipientName)).ToList();
    }

    public async Task<string> CreateRevealLinkAsync(
        Guid exchangeId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var token = CreateToken();
        var client = await authenticatedClient.GetAsync(cancellationToken);
        await client.Rpc("create_reveal_link", new
        {
            p_exchange_id = exchangeId,
            p_participant_id = participantId,
            p_token_hash = HashToken(token)
        });

        logger.LogInformation("Private reveal link created for exchange {ExchangeId}.", exchangeId);
        return $"/reveal/{token}";
    }

    public async Task<PrivateAssignment?> GetMyAssignmentAsync(
        string revealToken,
        string participantAccessPhrase,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(revealToken) || string.IsNullOrWhiteSpace(participantAccessPhrase) ||
            participantAccessPhrase.Trim().Length < ExchangeRules.MinimumAccessPhraseLength)
            return null;

        var client = await authenticatedClient.GetPublicAsync(cancellationToken);
        try
        {
            var row = await QuerySingleOrDefaultAsync<PrivateAssignmentRow>(client, "reveal_assignment", new
            {
                p_reveal_token = revealToken,
                p_private_phrase = participantAccessPhrase.Trim()
            });

            return row is null ? null : new PrivateAssignment(row.RecipientName, GiftPhraseCatalog.Pick());
        }
        catch (PostgrestException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<T>> QueryAsync<T>(Supabase.Client client, string function, object parameters)
        where T : new()
    {
        var rows = await client.Rpc<List<T>>(function, parameters);
        return rows ?? [];
    }

    private static async Task<T> QuerySingleAsync<T>(Supabase.Client client, string function, object parameters)
        where T : new()
    {
        return await QuerySingleOrDefaultAsync<T>(client, function, parameters)
            ?? throw new InvalidOperationException($"Supabase function '{function}' returned no record.");
    }

    private static async Task<T?> QuerySingleOrDefaultAsync<T>(Supabase.Client client, string function, object parameters)
        where T : new()
    {
        var rows = await QueryAsync<T>(client, function, parameters);
        return rows.FirstOrDefault();
    }

    private static Exchange MapExchange(ExchangeRow row)
    {
        if (!Enum.TryParse<ExchangeStatus>(row.Status, out var status))
            throw new InvalidOperationException($"Unknown exchange status '{row.Status}'.");

        return new Exchange(
            row.ExchangeId,
            row.Name,
            ParseDate(row.EventDate),
            row.Conditions,
            row.Budget,
            row.ExpectedParticipants,
            row.OwnerId,
            status,
            row.CreatedAt);
    }

    private static DateOnly ParseDate(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
