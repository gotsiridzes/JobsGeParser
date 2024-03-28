using System.Net.Http;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Text;

namespace JobsGeParser;

public class JobsGeClient
{
	private static HttpClient _client = null!;
	private readonly JobsGeParserOptions _ops;
	private readonly HtmlProcessor _processor;
	private readonly Repo _repo;

	public JobsGeClient(
		JobsGeParserOptions ops,
		IHttpClientFactory httpClientFactory,
		HtmlProcessor processor,
		Repo repo)
	{
		_ops = ops;
		_processor = processor;
		_repo = repo;
		_client = httpClientFactory.CreateClient("JobsGeClient");
	}

	public async Task RetrievePageItemsAsync(Channel<JobApplication> channel)
	{
		var response = await _client.GetAsync(_ops.JobsListUrl);

		var content = await response.Content.ReadAsStringAsync();

		var jobs = _processor.ParseHtmlAndGetJobApplicationsList(content);
		
		var processing = BatchProcessingAsync(channel);
		
		foreach (var item in jobs)
			channel.Writer.WriteAsync(item);

		await processing;
	}

	private async Task BatchProcessingAsync(Channel<JobApplication> channel)
	{
		await channel.Reader.WaitToReadAsync();

		while (channel.Reader.Count > 0)
		{
			var application = await channel.Reader.ReadAsync();

			var response = await _client.GetAsync(application.Link);

			var content = await response.Content.ReadAsStringAsync();
			content.Replace("\/r\/n", Environment.NewLine);
			application.SetDescription(_processor.ParseDescription(content));

			_repo.Save(application);
			await Task.Delay(500);
		}

		channel.Writer.Complete();
	}
}
