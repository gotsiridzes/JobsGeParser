using Microsoft.Extensions.Options;

namespace JobsGeParser.Configuration;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
	public ValidateOptionsResult Validate(string? name, DatabaseOptions options) =>
		ValidateOptionsResult.Success;
}
