using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace JobsGeParser.Configuration;

public static class AlertRateLimiting
{
	private const string UnknownClient = "unknown";

	/// <summary>
	/// Per-IP limits on the alert endpoints. IP limiting alone is trivially bypassed, so the
	/// subscribe flow also carries per-address and global caps in the database; this is the
	/// cheap first layer that keeps casual abuse away from the provider bill.
	/// </summary>
	public static IServiceCollection AddAlertRateLimiting(this IServiceCollection self)
	{
		self.AddRateLimiter(limiter =>
		{
			limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

			limiter.AddPolicy(AlertRateLimitPolicies.Preview, context =>
				FixedWindow(context, permitLimit: 30, window: TimeSpan.FromMinutes(1)));

			limiter.AddPolicy(AlertRateLimitPolicies.Subscribe, context =>
				FixedWindow(context, permitLimit: 5, window: TimeSpan.FromHours(1)));

			limiter.AddPolicy(AlertRateLimitPolicies.Manage, context =>
				FixedWindow(context, permitLimit: 60, window: TimeSpan.FromMinutes(1)));
		});

		return self;
	}

	private static RateLimitPartition<string> FixedWindow(HttpContext context, int permitLimit, TimeSpan window) =>
		RateLimitPartition.GetFixedWindowLimiter(
			context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = permitLimit,
				Window = window,
				// No queueing: a caller over the limit is rejected immediately rather than
				// holding a request open.
				QueueLimit = 0
			});
}
