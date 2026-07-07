# Agent guide

1. Read [Readme.MD](Readme.MD) for architecture and API
2. Follow [.cursor/rules/](.cursor/rules/) for conventions
3. Use [.cursor/skills/](.cursor/skills/) for endpoint and scraping workflows
4. Configure PostgreSQL connection string in `appsettings.Development.json`
5. Run: `dotnet run --project JobsGeParser/JobsGeParser.csproj`
6. Test: `JobsGeParser/JobsGeParser.http` (read endpoints + scrape status)
