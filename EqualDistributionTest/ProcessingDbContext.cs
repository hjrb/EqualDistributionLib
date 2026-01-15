using Microsoft.EntityFrameworkCore;

using System;
using System.Linq;

namespace EqualDistributionTest;

internal class ProcessingDbContext : DbContext
{
	public DbSet<ProcessingItem> ProcessingItems { get; set; }

	public ProcessingDbContext(DbContextOptions<ProcessingDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		_ = modelBuilder.Entity<ProcessingItem>(entity =>
		{
			_ = entity.HasKey(e => e.Id);
			_ = entity.Property(e => e.Id).IsRequired();
			_ = entity.Property(e => e.ProcessingDate).IsRequired();
		});

		// Seed sample data
		var baseDate = new DateTime(2024, 1, 1);
		var sampleData = Enumerable.Range(1, 100)
			.Select(i => new ProcessingItem
			{
				Id = i,
				ProcessingDate = DateOnly.FromDateTime(baseDate.AddDays(Random.Shared.Next(0, 20)))
			})
			.ToArray();

		_ = modelBuilder.Entity<ProcessingItem>().HasData(sampleData);
	}

	public static ProcessingDbContext CreateInMemoryContext()
	{
		var options = new DbContextOptionsBuilder<ProcessingDbContext>()
			.UseInMemoryDatabase(databaseName: $"ProcessingDb_{Guid.NewGuid()}")
			.Options;

		var context = new ProcessingDbContext(options);
		_ = context.Database.EnsureCreated();
		return context;
	}
}
