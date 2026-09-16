using JobsGeParser.Data;

namespace JobsGeParser.Alerts;

/// <summary>
/// Shows enough of an address for the owner to recognize it, without echoing it back in full to
/// anyone holding the link.
/// </summary>
public static class AlertAddressMasking
{
	public static string Mask(AlertChannelKind channel, string address) =>
		channel == AlertChannelKind.Sms ? MaskPhone(address) : MaskEmail(address);

	private static string MaskEmail(string address)
	{
		var at = address.IndexOf('@');
		if (at <= 0)
			return "***";

		var local = address[..at];
		var domain = address[at..];
		var keep = local.Length == 1 ? 1 : Math.Min(local.Length - 1, 3);

		return $"{local[..keep]}***{domain}";
	}

	private static string MaskPhone(string address)
	{
		if (address.Length <= 6)
			return "***";

		return $"{address[..Math.Min(5, address.Length - 4)]}***{address[^4..]}";
	}
}
