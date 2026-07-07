using Microsoft.EntityFrameworkCore;

namespace JobsGeParser.Data;

public class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
	public DbSet<JobEntity> Jobs => Set<JobEntity>();

	public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

	public DbSet<JobCategoryEntity> JobCategories => Set<JobCategoryEntity>();

	public DbSet<ScrapeRunEntity> ScrapeRuns => Set<ScrapeRunEntity>();

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
			entity.HasIndex(e => e.LastSeenAt);
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
	}
}
