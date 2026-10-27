using Microsoft.EntityFrameworkCore;

namespace JobsGeParser.Data;

public class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
	public DbSet<JobEntity> Jobs => Set<JobEntity>();

	public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

	public DbSet<JobCategoryEntity> JobCategories => Set<JobCategoryEntity>();

	public DbSet<ScrapeRunEntity> ScrapeRuns => Set<ScrapeRunEntity>();

	public DbSet<JobEventEntity> JobEvents => Set<JobEventEntity>();

	public DbSet<AlertSubscriberEntity> AlertSubscribers => Set<AlertSubscriberEntity>();

	public DbSet<AlertSubscriptionEntity> AlertSubscriptions => Set<AlertSubscriptionEntity>();

	public DbSet<AlertMessageEntity> AlertMessages => Set<AlertMessageEntity>();

	public DbSet<AlertDeliveryEntity> AlertDeliveries => Set<AlertDeliveryEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<JobEntity>(entity =>
		{
			entity.ToTable("jobs");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).ValueGeneratedNever();
			entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
			entity.Property(e => e.Link).IsRequired().HasMaxLength(1000);
			entity.Property(e => e.Company).IsRequired().HasMaxLength(500);
			entity.Property(e => e.CompanyLink).HasMaxLength(1000);
			entity.Property(e => e.SalaryCurrency).HasMaxLength(10);
			entity.Property(e => e.SalaryPeriod).HasMaxLength(20);
			entity.Property(e => e.City).HasMaxLength(100);
			entity.Property(e => e.WorkMode).HasMaxLength(20);
			entity.Property(e => e.EmploymentType).HasMaxLength(30);
			entity.Property(e => e.Seniority).HasMaxLength(20);
			entity.Property(e => e.LanguageRequirement).HasMaxLength(10);
			entity.Property(e => e.EnrichmentVersion).HasDefaultValue(0);
			entity.Property(e => e.SearchVector)
				.HasColumnType("tsvector")
				.HasComputedColumnSql("""
					setweight(to_tsvector('simple', coalesce("Name", '')), 'A') ||
					setweight(to_tsvector('simple', coalesce("Company", '')), 'B') ||
					setweight(to_tsvector('simple', coalesce("Description", '')), 'C')
					""", stored: true)
				.ValueGeneratedOnAddOrUpdate();
			entity.HasIndex(e => e.LastSeenAt);
			entity.HasIndex(e => e.SearchVector).HasMethod("GIN");
			entity.HasIndex(e => e.Name).HasMethod("GIN").HasOperators("gin_trgm_ops");
			entity.HasIndex(e => e.Company).HasMethod("GIN").HasOperators("gin_trgm_ops");
		});

		modelBuilder.Entity<CategoryEntity>(entity =>
		{
			entity.ToTable("categories");
			entity.HasKey(e => e.Slug);
			entity.Property(e => e.Slug).HasMaxLength(100);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.ListUrl).IsRequired().HasMaxLength(1000);
		});

		modelBuilder.Entity<JobCategoryEntity>(entity =>
		{
			entity.ToTable("job_categories");
			entity.HasKey(e => new { e.JobId, e.CategorySlug });
			entity.HasOne(e => e.Job)
				.WithMany(j => j.JobCategories)
				.HasForeignKey(e => e.JobId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(e => e.Category)
				.WithMany(c => c.JobCategories)
				.HasForeignKey(e => e.CategorySlug)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<ScrapeRunEntity>(entity =>
		{
			entity.ToTable("scrape_runs");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
			entity.Property(e => e.Phase).IsRequired().HasMaxLength(50);
			entity.Property(e => e.CategorySlug).HasMaxLength(100);
			entity.HasIndex(e => e.StartedAt);
			entity.HasIndex(e => e.Status);
			entity.HasIndex(e => e.BatchId);
			entity.HasIndex(e => new { e.CategorySlug, e.StartedAt });
			entity.HasOne(e => e.Category)
				.WithMany(c => c.ScrapeRuns)
				.HasForeignKey(e => e.CategorySlug)
				.OnDelete(DeleteBehavior.SetNull);
		});

		modelBuilder.Entity<JobEventEntity>(entity =>
		{
			entity.ToTable("job_events");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.EventType).IsRequired().HasMaxLength(30);
			entity.Property(e => e.CategorySlug).HasMaxLength(100);
			entity.HasOne(e => e.Job)
				.WithMany()
				.HasForeignKey(e => e.JobId)
				.OnDelete(DeleteBehavior.Cascade);
			// Partial: the table grows unbounded but the pending set stays near zero, so the
			// index stays a few pages regardless of history.
			entity.HasIndex(e => new { e.AvailableAt, e.Id })
				.HasFilter("\"ProcessedAt\" IS NULL");
			entity.HasIndex(e => e.JobId);
		});

		modelBuilder.Entity<AlertSubscriberEntity>(entity =>
		{
			entity.ToTable("alert_subscribers");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).ValueGeneratedNever();
			entity.Property(e => e.Channel).IsRequired().HasMaxLength(20).HasConversion<string>();
			entity.Property(e => e.Address).IsRequired().HasMaxLength(320);
			entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
			entity.Property(e => e.TimeZoneId).IsRequired().HasMaxLength(64);
			entity.Property(e => e.Locale).HasMaxLength(10);
			entity.Property(e => e.SuppressionReason).HasMaxLength(200);
			// Makes subscribing twice with the same address idempotent.
			entity.HasIndex(e => new { e.Channel, e.Address }).IsUnique();
			entity.HasIndex(e => e.ManageTokenHash).IsUnique();
			entity.HasIndex(e => e.ConfirmationTokenHash)
				.IsUnique()
				.HasFilter("\"ConfirmationTokenHash\" IS NOT NULL");
		});

		modelBuilder.Entity<AlertSubscriptionEntity>(entity =>
		{
			entity.ToTable("alert_subscriptions");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).ValueGeneratedNever();
			entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
			entity.Property(e => e.Cadence).IsRequired().HasMaxLength(20).HasConversion<string>();
			entity.Property(e => e.Query).HasMaxLength(200);
			entity.Property(e => e.TitleContains).HasMaxLength(200);
			entity.Property(e => e.DescriptionContains).HasMaxLength(200);
			entity.Property(e => e.CompanyContains).HasMaxLength(200);
			entity.Property(e => e.SalaryCurrency).HasMaxLength(10);
			entity.HasOne(e => e.Subscriber)
				.WithMany(s => s.Subscriptions)
				.HasForeignKey(e => e.SubscriberId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasIndex(e => new { e.SubscriberId, e.CriteriaHash })
				.IsUnique()
				.HasFilter("\"Status\" <> 'Deleted'");
			entity.HasIndex(e => e.SubscriberId);
			entity.HasIndex(e => e.DigestSendLocalTime)
				.HasFilter("\"Status\" = 'Active' AND \"Cadence\" = 'DailyDigest'");
			// Array GIN indexes serve the reverse match prefilter. Default array_ops covers the
			// containment and overlap operators, which is all the prefilter uses.
			entity.HasIndex(e => e.CategorySlugs).HasMethod("GIN");
			entity.HasIndex(e => e.Cities).HasMethod("GIN");
			entity.HasIndex(e => e.WorkModes).HasMethod("GIN");
			entity.HasIndex(e => e.EmploymentTypes).HasMethod("GIN");
			entity.HasIndex(e => e.Seniorities).HasMethod("GIN");
			entity.HasIndex(e => e.Languages).HasMethod("GIN");
		});

		modelBuilder.Entity<AlertMessageEntity>(entity =>
		{
			entity.ToTable("alert_messages");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).ValueGeneratedNever();
			entity.Property(e => e.Kind).IsRequired().HasMaxLength(20);
			entity.Property(e => e.Channel).IsRequired().HasMaxLength(20).HasConversion<string>();
			entity.Property(e => e.Address).IsRequired().HasMaxLength(320);
			entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(120);
			entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
			entity.Property(e => e.Subject).HasMaxLength(300);
			entity.Property(e => e.BodyText).IsRequired();
			entity.Property(e => e.ProviderMessageId).HasMaxLength(200);
			entity.HasOne(e => e.Subscriber)
				.WithMany()
				.HasForeignKey(e => e.SubscriberId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(e => e.Subscription)
				.WithMany()
				.HasForeignKey(e => e.SubscriptionId)
				.OnDelete(DeleteBehavior.SetNull);
			entity.HasIndex(e => e.IdempotencyKey).IsUnique();
			entity.HasIndex(e => new { e.AvailableAt, e.Id })
				.HasFilter("\"Status\" IN ('Pending', 'Claimed')");
			entity.HasIndex(e => new { e.SubscriberId, e.CreatedAt })
				.IsDescending(false, true);
		});

		modelBuilder.Entity<AlertDeliveryEntity>(entity =>
		{
			entity.ToTable("alert_deliveries");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
			entity.Property(e => e.SkipReason).HasMaxLength(50);
			entity.HasOne(e => e.Subscription)
				.WithMany()
				.HasForeignKey(e => e.SubscriptionId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(e => e.Job)
				.WithMany()
				.HasForeignKey(e => e.JobId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(e => e.Message)
				.WithMany(m => m.Deliveries)
				.HasForeignKey(e => e.MessageId)
				.OnDelete(DeleteBehavior.SetNull);
			// The entire no-duplicate-alerts guarantee, enforced by Postgres rather than by code.
			entity.HasIndex(e => new { e.SubscriptionId, e.JobId }).IsUnique();
			// Pending rows are exactly the digest backlog, so this doubles as the digest query index.
			entity.HasIndex(e => new { e.SubscriptionId, e.MatchedAt })
				.HasFilter("\"MessageId\" IS NULL AND \"Status\" = 'Pending'");
		});
	}
}
