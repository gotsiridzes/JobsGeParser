using HtmlAgilityPack;
using JobsGeParser.Models;

namespace JobsGeParser.Scraping;

public class HtmlProcessor
{
	private static HtmlDocument LoadDocument(string document)
	{
		var htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml(document);
		return htmlDocument;
	}

	private static IEnumerable<IReadOnlyList<string>> ParseListingRows(HtmlDocument document)
	{
		var tableNode = document.DocumentNode
			.SelectSingleNode("//html//body//div[@class='regularEntries']//table");

		if (tableNode is null)
			yield break;

		foreach (var tr in tableNode.Descendants("tr").Skip(1))
		{
			if (tr.Elements("td").Count() <= 1)
				continue;

			var cells = tr.Elements("td")
				.Select(ParseCell)
				.Where(x => x is not null)
				.Cast<string>()
				.ToList();

			if (cells.Count >= 4)
				yield return cells;
		}
	}

	private static string? ParseCell(HtmlNode td)
	{
		var text = td.InnerText.Trim();
		if (string.IsNullOrEmpty(text))
			return null;

		var link = td.SelectSingleNode(".//a");
		var linkValue = link?.GetAttributeValue("href", string.Empty) ?? string.Empty;
		return string.IsNullOrEmpty(linkValue)
			? text
			: string.Concat(text, "|", linkValue);
	}

	public IEnumerable<JobApplication> ParseHtmlAndGetJobApplicationsList(string content)
	{
		var document = LoadDocument(content);

		foreach (var row in ParseListingRows(document))
		{
			var job = TryParseJobRow(row);
			if (job is not null)
				yield return job;
		}
	}

	private static JobApplication? TryParseJobRow(IReadOnlyList<string> row)
	{
		try
		{
			var nameAndLink = row[0].Split('|');
			var companyAndLink = row[1].Split('|');

			if (nameAndLink.Length < 2 || !TryExtractJobId(nameAndLink[1], out var id))
				return null;

			var application = new JobApplication(
				id,
				nameAndLink[0],
				companyAndLink[0],
				row[2].GetDate(),
				row[3].GetDate());

			application.SetLink(nameAndLink[1]);
			application.SetCompanyLink(companyAndLink.Length == 1 ? null : companyAndLink[1]);
			return application;
		}
		catch
		{
			return null;
		}
	}

	private static bool TryExtractJobId(string link, out int id)
	{
		id = 0;
		var idMarker = "id=";
		var index = link.IndexOf(idMarker, StringComparison.OrdinalIgnoreCase);
		if (index < 0)
			return false;

		var idPart = link[(index + idMarker.Length)..];
		var end = idPart.IndexOfAny(['&', '#', ' ']);
		if (end >= 0)
			idPart = idPart[..end];

		return int.TryParse(idPart, out id);
	}

	public string? TryParseDescription(string content)
	{
		var document = LoadDocument(content);
		var jobNode = document.GetElementbyId("job");
		if (jobNode is null)
			return null;

		var table = jobNode.SelectSingleNode(".//table[@class='dtable']");
		if (table is null)
			return null;

		var rows = table.Descendants("tr").ToList();
		if (rows.Count <= 3)
			return null;

		return rows[3].InnerText.Trim();
	}
}
