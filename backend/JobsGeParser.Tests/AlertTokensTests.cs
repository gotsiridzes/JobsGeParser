using JobsGeParser.Alerts;
using JobsGeParser.Alerts.Channels;
using JobsGeParser.Data;

namespace JobsGeParser.Tests;

public class AlertTokensTests
{
	private const string Secret = "test-secret-value-长-enough";

	[Fact]
	public void CreateRandom_Is22UrlSafeCharacters()
	{
		var token = AlertTokens.CreateRandom();

		Assert.Equal(22, token.Length);
		Assert.DoesNotContain('+', token);
		Assert.DoesNotContain('/', token);
		Assert.DoesNotContain('=', token);
		Assert.True(AlertTokens.IsWellFormed(token));
	}

	[Fact]
	public void CreateRandom_DoesNotRepeat()
	{
		var tokens = Enumerable.Range(0, 200).Select(_ => AlertTokens.CreateRandom()).ToHashSet();

		Assert.Equal(200, tokens.Count);
	}

	[Fact]
	public void Hash_IsDeterministicAndNotTheToken()
	{
		var token = AlertTokens.CreateRandom();

		Assert.Equal(AlertTokens.Hash(token), AlertTokens.Hash(token));
		Assert.NotEqual(token, Convert.ToBase64String(AlertTokens.Hash(token)));
		Assert.Equal(32, AlertTokens.Hash(token).Length);
	}

	[Fact]
	public void DeriveManageToken_IsStableForTheSameSubscriber()
	{
		var id = Guid.CreateVersion7();

		Assert.Equal(
			AlertTokens.DeriveManageToken(id, Secret),
			AlertTokens.DeriveManageToken(id, Secret));
	}

	[Fact]
	public void DeriveManageToken_DiffersPerSubscriberAndPerSecret()
	{
		var a = Guid.CreateVersion7();
		var b = Guid.CreateVersion7();

		Assert.NotEqual(AlertTokens.DeriveManageToken(a, Secret), AlertTokens.DeriveManageToken(b, Secret));
		Assert.NotEqual(AlertTokens.DeriveManageToken(a, Secret), AlertTokens.DeriveManageToken(a, Secret + "x"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("short")]
	[InlineData("has spaces in it aaaaaaaa")]
	public void IsWellFormed_RejectsJunk(string? token)
	{
		Assert.False(AlertTokens.IsWellFormed(token));
	}
}

public class AlertAddressTests
{
	[Theory]
	[InlineData("  User@Example.COM ", "user@example.com")]
	[InlineData("a.b+tag@sub.example.co.uk", "a.b+tag@sub.example.co.uk")]
	public void Email_IsNormalizedToLowercase(string raw, string expected)
	{
		Assert.True(EmailAddress.TryNormalize(raw, out var normalized, out _));
		Assert.Equal(expected, normalized);
	}

	[Theory]
	[InlineData("not-an-email")]
	[InlineData("no@tld")]
	[InlineData("")]
	public void Email_RejectsInvalid(string raw)
	{
		Assert.False(EmailAddress.TryNormalize(raw, out _, out var error));
		Assert.False(string.IsNullOrWhiteSpace(error));
	}

	[Theory]
	[InlineData("555 12 34 56", "+995555123456")]
	[InlineData("+995 555 123456", "+995555123456")]
	[InlineData("+1 (415) 555-2671", "+14155552671")]
	public void Phone_IsNormalizedToE164(string raw, string expected)
	{
		Assert.True(PhoneAddress.TryNormalize(raw, "+995", out var normalized, out _));
		Assert.Equal(expected, normalized);
	}

	[Theory]
	[InlineData("123")]
	[InlineData("abc")]
	[InlineData("+1234567890123456789")]
	public void Phone_RejectsImplausibleNumbers(string raw)
	{
		Assert.False(PhoneAddress.TryNormalize(raw, "+995", out _, out var error));
		Assert.False(string.IsNullOrWhiteSpace(error));
	}

	[Fact]
	public void Mask_KeepsEnoughToRecognizeWithoutEchoingTheAddress()
	{
		var masked = AlertAddressMasking.Mask(AlertChannelKind.Email, "gotsiridze.s@outlook.com");

		Assert.StartsWith("got", masked, StringComparison.Ordinal);
		Assert.EndsWith("@outlook.com", masked, StringComparison.Ordinal);
		Assert.DoesNotContain("gotsiridze.s", masked, StringComparison.Ordinal);
	}

	[Fact]
	public void Mask_HidesTheMiddleOfAPhoneNumber()
	{
		var masked = AlertAddressMasking.Mask(AlertChannelKind.Sms, "+995555123456");

		Assert.Contains("***", masked, StringComparison.Ordinal);
		Assert.EndsWith("3456", masked, StringComparison.Ordinal);
	}
}
