using HtmlAgilityPack;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace JobsGeParser;

public class JobsGeClient
{
	private static HttpClient _client;
	private Repository _repository;
	private List<int> _jobs;

	public JobsGeClient()
	{
		InitializeHttpClient();
		_repository = new Repository();
	}

	private static void InitializeHttpClient()
	{
		_client = new HttpClient();
		_client.BaseAddress = new Uri(Constants.BaseAddress);
	}

	private static async Task<string> GetContent()
	{
		var response = await _client.GetAsync(Constants.RequestUri);

		var content = await response.Content.ReadAsStringAsync();

		return content;
	}

	private static Task<HtmlDocument> LoadDocument(string document)
	{
		var htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml(document);
		return Task.FromResult(htmlDocument);
	}

	public async Task<IEnumerable<JobApplication>> GetJobApplicationsAsync()
	{
		var content = await GetContent();
		var document = await LoadDocument(content);
		var table = await ParseHtmlDocument(document);
		var applications = await ReadJobsTable(table);

		return applications;
	}

	private async Task<IEnumerable<JobApplication>> ReadJobsTable(IEnumerable<IEnumerable<string>> table)
	{
		var jobs = new List<JobApplication>();
		int i = 0;
		foreach (var row in table)
		{
			var nameAndLink = row.ElementAt(0).Split('|');
			var companyAndLink = row.ElementAt(1).Split('|');

			var application = new JobApplication
			{
				Id = int.Parse(nameAndLink[1].Split("id=")[1]),
				Name = nameAndLink[0],
				Link = nameAndLink[1],
				Company = companyAndLink[0],
				CompanyLink = companyAndLink.Length == 1 ? null : companyAndLink[1],
				Published = row.ElementAt(2).GetDate(),
				EndDate = row.ElementAt(3).GetDate(),
			};
			Console.ForegroundColor = ConsoleColor.White;

			Console.WriteLine($"{i++} Got {application.Id} Description");

			await Task.Delay(400);
			application.Description = await ReadDescription(application);

			await _repository.Insert(application);
			Console.WriteLine("\tChecking for active status...");
			await _repository.CheckJobs(application);

			jobs.Add(application);
		}

		return jobs.ToList();
	}

	private async Task<string> ReadDescription(JobApplication application)
	{
		var response = await _client.GetAsync(application.Link);
		var content = await response.Content.ReadAsStringAsync();

		var document = new HtmlDocument();
		document.LoadHtml(content);
		return document.DocumentNode.SelectSingleNode("//table[@class='ad']").Descendants("tr").ElementAt(3).InnerText.Trim();
	}

	private async Task<IEnumerable<IEnumerable<string>>> ParseHtmlDocument(HtmlDocument document)
	{
		var table = document.DocumentNode
			.SelectSingleNode($"//html//body//div[@class='{Constants.ClassToGet}']//table")
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

		return await Task.FromResult(table);
	}
}