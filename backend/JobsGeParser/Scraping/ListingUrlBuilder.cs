using Microsoft.AspNetCore.WebUtilities;

namespace JobsGeParser.Scraping;

public static class ListingUrlBuilder
{
	/// <summary>
	/// Builds a listing URL for the given page from a category ListUrl query string.
	/// Page 1 is the full HTML listing; page 2+ appends for_scroll=yes (infinite-scroll batches).
	/// </summary>
	public static string ForPage(string listUrl, int page)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(listUrl);
		ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

		var queryIndex = listUrl.IndexOf('?');
		var path = queryIndex >= 0 ? listUrl[..queryIndex] : listUrl;
		var query = queryIndex >= 0 ? listUrl[(queryIndex + 1)..] : string.Empty;

		var parameters = QueryHelpers.ParseQuery(query)
			.ToDictionary(
				static pair => pair.Key,
				static pair => (string?)pair.Value.ToString(),
				StringComparer.OrdinalIgnoreCase);

		parameters["page"] = page.ToString();
		if (page >= 2)
			parameters["for_scroll"] = "yes";
		else
			parameters.Remove("for_scroll");

		return QueryHelpers.AddQueryString(path, parameters);
	}
}
