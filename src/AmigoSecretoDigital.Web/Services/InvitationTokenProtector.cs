using System.Security.Cryptography;
using System.Text;

namespace AmigoSecretoDigital.Web.Services;

/// <summary>Encrypts invitation tokens before they are persisted in Supabase.</summary>
public sealed class InvitationTokenProtector
{
    private readonly byte[] _key;

    public InvitationTokenProtector(string encodedKey)
    {
        try
        {
            _key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "InvitationTokens:EncryptionKey must be a base64-encoded 32-byte key.", exception);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException("InvitationTokens:EncryptionKey must be a base64-encoded 32-byte key.");
    }

    public string Protect(Guid exchangeId, string token)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(token);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, exchangeId.ToByteArray());

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, nonce.Length);
        ciphertext.CopyTo(payload, nonce.Length + tag.Length);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(Guid exchangeId, string protectedToken)
    {
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(protectedToken);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("The stored invitation token is invalid.", exception);
        }

        const int nonceLength = 12;
        const int tagLength = 16;
        if (payload.Length <= nonceLength + tagLength)
            throw new CryptographicException("The stored invitation token is invalid.");

        var plaintext = new byte[payload.Length - nonceLength - tagLength];
        using var aes = new AesGcm(_key, tagLength);
        aes.Decrypt(
            payload.AsSpan(0, nonceLength),
            payload.AsSpan(nonceLength + tagLength),
            payload.AsSpan(nonceLength, tagLength),
            plaintext,
            exchangeId.ToByteArray());

        return Encoding.UTF8.GetString(plaintext);
    }
}
