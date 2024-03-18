using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace JobsGeParser;

public class HtmlProcessor
{
	private static HtmlDocument LoadDocument(string document)
	{
		var htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml(document);

		return htmlDocument;
	}

	private IEnumerable<IEnumerable<string>> ParseHtmlDocument(HtmlDocument document)
	{
		var table = document.DocumentNode
			.SelectSingleNode($"//html//body//div[@class='regularEntries']//table")
			.Descendants("tr")
			.Skip(1)
			.Where(tr => tr.Elements("td").Count() > 1)
			.Select(tr => tr.Elements("td").Select(td =>
			{
				var text = td.InnerText.Trim();
				var links = td.SelectNodes("a");
				var linkValue = string.Empty;
				if (links != null)
					linkValue = links[0].Attributes[0].Value;

				return string.IsNullOrEmpty(text) ? null : string.Concat(text, "|", linkValue).TrimEnd('|');
			}).Where(x => !string.IsNullOrEmpty(x)).ToList())
			.ToList();

		return table;
	}

	public IEnumerable<JobApplication> ParseHtmlAndGetJobApplicationsList(string content)
	{
		var document = LoadDocument(content);

		var table = ParseHtmlDocument(document);

		foreach (var row in table)
		{
			var nameAndLink = row.ElementAt(0).Split('|');
			var companyAndLink = row.ElementAt(1).Split('|');
			int id = int.Parse(nameAndLink[1].Split("id=")[1]);

			var application = new JobApplication(
				id,
				nameAndLink[0],
				companyAndLink[0],
				row.ElementAt(2).GetDate(),
				row.ElementAt(3).GetDate());

			application.SetLink(nameAndLink[1]);
			application.SetCompanyLink(companyAndLink.Length == 1 ? null : companyAndLink[1]);

			yield return application;
		}
	}

	public string ParseDescription(string content)
	{
		var document = LoadDocument(content);

		return document
			.GetElementbyId("job")
			.SelectSingleNode("//table[@class='dtable']")
			.Descendants("tr")
			.ElementAt(3)
			.InnerText
			.Trim();
	}
}