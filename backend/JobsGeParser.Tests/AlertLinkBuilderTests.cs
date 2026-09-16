using JobsGeParser.Alerts;
using JobsGeParser.Configuration;
using Microsoft.Extensions.Options;

namespace JobsGeParser.Tests;

public class AlertLinkBuilderTests
{
	private static readonly AlertLinkBuilder Links = new(Options.Create(new AlertOptions
	{
		PublicBaseUrl = "https://jobs.example/",
		ApiBaseUrl = "https://api.jobs.example/"
	}));

	[Fact]
	public void HumanLinks_UseThePublicBase()
	{
		Assert.Equal("https://jobs.example/a/tok", Links.Manage("tok"));
		Assert.Equal("https://jobs.example/a/c/tok", Links.Confirm("tok"));
		Assert.Equal("https://jobs.example/a/u/tok", Links.Unsubscribe("tok"));
	}

	[Fact]
	public void OneClickUnsubscribe_UsesTheApiBase()
	{
		// Mail clients POST here with no JavaScript, so it must reach the API directly.
		Assert.Equal("https://api.jobs.example/a/u/tok", Links.OneClickUnsubscribe("tok"));
	}
}
