using System.Text.Json.Serialization;

namespace AmigoSecretoDigital.Web.Services;

/// <summary>DTOs for the JSON rows returned by the Supabase RPC functions.</summary>
public sealed class ExchangeRow
{
    [JsonPropertyName("exchange_id")]
    public Guid ExchangeId { get; set; }

    [JsonPropertyName("exchange_owner_id")]
    public Guid OwnerId { get; set; }

    [JsonPropertyName("exchange_name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("exchange_event_date")]
    public string EventDate { get; set; } = string.Empty;

    [JsonPropertyName("exchange_conditions")]
    public string Conditions { get; set; } = string.Empty;

    [JsonPropertyName("exchange_budget")]
    public decimal? Budget { get; set; }

    [JsonPropertyName("exchange_expected_participants")]
    public int ExpectedParticipants { get; set; }

    [JsonPropertyName("exchange_status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("exchange_created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("participant_count")]
    public int ParticipantCount { get; set; }
}

public sealed class InvitationContextRow
{
    [JsonPropertyName("exchange_id")]
    public Guid ExchangeId { get; set; }

    [JsonPropertyName("exchange_name")]
    public string ExchangeName { get; set; } = string.Empty;

    [JsonPropertyName("exchange_event_date")]
    public string EventDate { get; set; } = string.Empty;

    [JsonPropertyName("exchange_conditions")]
    public string Conditions { get; set; } = string.Empty;

    [JsonPropertyName("exchange_budget")]
    public decimal? Budget { get; set; }

    [JsonPropertyName("exchange_expected_participants")]
    public int ExpectedParticipants { get; set; }

    [JsonPropertyName("participant_count")]
    public int ParticipantCount { get; set; }
}

public sealed class ParticipantRow
{
    [JsonPropertyName("participant_id")]
    public Guid ParticipantId { get; set; }

    [JsonPropertyName("participant_exchange_id")]
    public Guid ExchangeId { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("registered_at")]
    public DateTimeOffset RegisteredAt { get; set; }
}

public sealed class ParticipantIdRow
{
    [JsonPropertyName("participant_id")]
    public Guid ParticipantId { get; set; }

    [JsonPropertyName("participant_exchange_id")]
    public Guid ExchangeId { get; set; }

    [JsonPropertyName("participant_registered_at")]
    public DateTimeOffset RegisteredAt { get; set; }
}

public sealed class AdminAssignmentRow
{
    [JsonPropertyName("participant_id")]
    public Guid ParticipantId { get; set; }

    [JsonPropertyName("participant_name")]
    public string ParticipantName { get; set; } = string.Empty;

    [JsonPropertyName("recipient_name")]
    public string RecipientName { get; set; } = string.Empty;
}

public sealed class PrivateAssignmentRow
{
    [JsonPropertyName("recipient_name")]
    public string RecipientName { get; set; } = string.Empty;
}
