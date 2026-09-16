using JobsGeParser.Alerts;
using JobsGeParser.Data;
using Microsoft.EntityFrameworkCore;

namespace JobsGeParser.IntegrationTests;

/// <summary>
/// Seed helpers. Jobs are inserted through <see cref="Repo"/> where the test cares about the
/// outbox, and directly where it only needs a row to match against.
/// </summary>
public static class AlertsTestData
{
	public static JobEntity NewJob(
		int id,
		string name = "Senior Backend Developer",
		string company = "Acme LLC",
		string? description = "We need a strong .NET engineer for a remote role in Tbilisi.",
		string? city = "Tbilisi",
		string? workMode = "remote",
		string? seniority = "senior",
		decimal? salaryMin = null,
		decimal? salaryMax = null,
		string? salaryCurrency = null,
		DateTimeOffset? firstSeenAt = null)
	{
		var now = firstSeenAt ?? DateTimeOffset.UtcNow;

		return new JobEntity
		{
			Id = id,
			Name = name,
			Link = $"https://jobs.ge/en/?view=jobs&id={id}",
			Company = company,
			Published = DateOnly.FromDateTime(now.UtcDateTime),
			EndDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(30)),
			Description = description,
			City = city,
			WorkMode = workMode,
			Seniority = seniority,
			SalaryMin = salaryMin,
			SalaryMax = salaryMax,
			SalaryCurrency = salaryCurrency,
			DetailsFetchedAt = now,
			FirstSeenAt = now,
			LastSeenAt = now
		};
	}

	public static async Task<JobEntity> AddJobAsync(
		JobsDbContext db,
		JobEntity job,
		params string[] categorySlugs)
	{
		db.Jobs.Add(job);

		foreach (var slug in categorySlugs.DefaultIfEmpty("it"))
		{
			db.JobCategories.Add(new JobCategoryEntity
			{
				JobId = job.Id,
				CategorySlug = slug,
				FirstSeenAt = job.FirstSeenAt,
				LastSeenAt = job.LastSeenAt
			});
		}

		await db.SaveChangesAsync();
		return job;
	}

	/// <summary>
	/// A confirmed subscriber with one active subscription, which is the state almost every test
	/// needs as its starting point.
	/// </summary>
	public static async Task<(AlertSubscriberEntity Subscriber, AlertSubscriptionEntity Subscription)>
		AddConfirmedSubscriptionAsync(
			JobsDbContext db,
			AlertCriteria criteria,
			AlertChannelKind channel = AlertChannelKind.Email,
			AlertCadence cadence = AlertCadence.Instant,
			string address = "subscriber@example.com",
			string timeZoneId = "Asia/Tbilisi",
			TimeOnly? digestSendLocalTime = null,
			DateTimeOffset? matchFromAt = null)
	{
		var now = matchFromAt ?? DateTimeOffset.UtcNow.AddDays(-1);
		var id = Guid.CreateVersion7();

		var subscriber = new AlertSubscriberEntity
		{
			Id = id,
			Channel = channel,
			Address = address,
			Status = AlertSubscriberStatus.Confirmed,
			TimeZoneId = timeZoneId,
			ManageTokenHash = AlertTokens.Hash(AlertTokens.DeriveManageToken(id, AlertsFixture.TokenSecret)),
			ConfirmedAt = now,
			CreatedAt = now,
			UpdatedAt = now
		};

		var canonical = criteria.Canonicalize();

		var subscription = new AlertSubscriptionEntity
		{
			Id = Guid.CreateVersion7(),
			SubscriberId = subscriber.Id,
			Name = "Test alert",
			Status = AlertSubscriptionStatus.Active,
			Cadence = cadence,
			DigestSendLocalTime = digestSendLocalTime ?? new TimeOnly(9, 0),
			MatchFromAt = now,
			CriteriaHash = canonical.ComputeHash(),
			CreatedAt = now,
			UpdatedAt = now,
			Query = canonical.Query,
			TitleContains = canonical.TitleContains,
			DescriptionContains = canonical.DescriptionContains,
			CompanyContains = canonical.CompanyContains,
			CategorySlugs = canonical.CategorySlugs?.ToArray(),
			Cities = canonical.Cities?.ToArray(),
			WorkModes = canonical.WorkModes?.ToArray(),
			EmploymentTypes = canonical.EmploymentTypes?.ToArray(),
			Seniorities = canonical.Seniorities?.ToArray(),
			Languages = canonical.Languages?.ToArray(),
			SalaryMin = canonical.SalaryMin,
			SalaryMax = canonical.SalaryMax,
			SalaryCurrency = canonical.SalaryCurrency,
			RequireSalary = canonical.RequireSalary
		};

		db.AlertSubscribers.Add(subscriber);
		db.AlertSubscriptions.Add(subscription);
		await db.SaveChangesAsync();

		return (subscriber, subscription);
	}

	public static Task<int> CountDeliveriesAsync(JobsDbContext db, Guid subscriptionId) =>
		db.AlertDeliveries.CountAsync(d => d.SubscriptionId == subscriptionId);

	public static void AddJobEvent(JobsDbContext db, int jobId, string eventType = JobEventType.JobReady)
	{
		var now = DateTimeOffset.UtcNow;

		db.JobEvents.Add(new JobEventEntity
		{
			JobId = jobId,
			EventType = eventType,
			OccurredAt = now,
			AvailableAt = now.AddMinutes(-1)
		});
	}
}
