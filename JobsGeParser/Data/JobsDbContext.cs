using Microsoft.EntityFrameworkCore;

namespace JobsGeParser.Data;

public class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
	public DbSet<JobEntity> Jobs => Set<JobEntity>();

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

		modelBuilder.Entity<ScrapeRunEntity>(entity =>
		{
			entity.ToTable("scrape_runs");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
			entity.HasIndex(e => e.StartedAt);
		});
	}
}
