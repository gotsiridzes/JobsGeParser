using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace JobsGeParser.Alerts;

/// <summary>
/// Capability tokens. 16 bytes is 128 bits of entropy, far past brute force, and encodes to 22
/// URL safe characters. A longer token would push an SMS link past 70 characters and eat a
/// quarter of a message segment for no security gain.
/// </summary>
public static class AlertTokens
{
	public const int TokenBytes = 16;

	/// <summary>
	/// A random single-use token, such as a confirmation link. Returned once to the caller and
	/// never stored: only <see cref="Hash"/> of it goes to the database.
	/// </summary>
	public static string CreateRandom() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

	/// <summary>
	/// The long lived manage and unsubscribe token, derived from the subscriber id with a server
	/// secret rather than generated and stored. Every message footer needs this link long after
	/// the subscriber was created, and deriving it means there is no raw token in the database to
	/// leak: the row holds only the hash, which is used to look the subscriber up.
	/// </summary>
	public static string DeriveManageToken(Guid subscriberId, string secret)
	{
		var mac = HMACSHA256.HashData(
			Encoding.UTF8.GetBytes(secret),
			subscriberId.ToByteArray());

		return Base64Url.EncodeToString(mac.AsSpan(0, TokenBytes));
	}

	/// <summary>
	/// Only the hash is persisted, so a database leak does not hand out working links.
	/// </summary>
	public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

	/// <summary>
	/// Rejects anything that is not a plausible token before it reaches a database lookup.
	/// </summary>
	public static bool IsWellFormed(string? token) =>
		!string.IsNullOrWhiteSpace(token)
		&& token.Length is >= 20 and <= 64
		&& Base64Url.IsValid(token);
}
