using EqualDistributionLib;

using Microsoft.EntityFrameworkCore;

using static EqualDistributionLib.EqualDistribution;

namespace EqualDistributionTest;

[TestClass]
public sealed class TestEqualDistribution
{
	public TestContext TestContext { get; set; }

	class TestItem(int key, string value) { public int key=key; public string value=value; }
	[AssemblyInitialize]
	public static void AssemblyInit(TestContext context)
	{
		// This method is called once for the test assembly, before any tests are run.
	}

	[AssemblyCleanup]
	public static void AssemblyCleanup()
	{
		// This method is called once for the test assembly, after all tests are run.
	}

	[ClassInitialize]
	public static void ClassInit(TestContext context)
	{
		// This method is called once for the test class, before any tests of the class are run.
	}

	[ClassCleanup]
	public static void ClassCleanup()
	{
		// This method is called once for the test class, after all tests of the class are run.
	}


	[TestInitialize]
	public void TestInit()
	{
	}

	[TestCleanup]
	public void TestCleanup()
	{
		// This method is called after each test method.
	}

	[TestMethod]
	public async Task TestMethodDistributeAsync1()
	{
		List<TestItem> items=[];
		Dictionary<int, ICollection<TestItem>> bins=new();
		int notInBin=0;
		items = Enumerable.Range(1, 1023).Select(i => new TestItem(Random.Shared.Next(0, 16), $"Item {i}")).ToList();
		// add some more randomness
		for (var i = 0; i < Random.Shared.Next(200, 500); ++i)
		{
			items.ElementAt(Random.Shared.Next(0, items.Count)).key = Random.Shared.Next(0, 16);
		}
		bins = items
			.Where(a => a.key < 10)
			.GroupBy(a => a.key)
			.ToDictionary(g => g.Key, g => (ICollection<TestItem>)g.ToList());
		notInBin = items.Count(a => !bins.ContainsKey(a.key));
		DumpBins(bins, Console.WriteLine);
		var result=await DistributeEquallyAsync(items, a => a.key, bins, null);
		DumpBins(bins, Console.WriteLine);
		AssertDistribution(bins.GroupBy(a => a.Key).Select(g => new BinItem<int> { PropertyValue = g.Key, Count = g.Count() }).ToList());

		var mustUpdateKey=result.ToList();
		Assert.IsLessThanOrEqualTo(notInBin, mustUpdateKey.Count());
		Console.WriteLine($"Items not in bin: {notInBin}, items to update key: {mustUpdateKey.Count()}");

	}
	[TestMethod]
	public async Task TestMethodDistributeAsync2()
	{
		List<TestItem> items=[];
		Dictionary<int, ICollection<TestItem>> bins=new();
		var notInBin=0;
		items = Enumerable.Range(1, 1023).Select(i => new TestItem(Random.Shared.Next(0, 10), $"Item {i}")).ToList();
		// add some more randomness
		for (var i = 0; i < Random.Shared.Next(200, 500); ++i)
		{
			items.ElementAt(Random.Shared.Next(0, items.Count)).key = Random.Shared.Next(0, 10);
		}
		bins = items
				.GroupBy(a => a.key)
				.ToDictionary(g => g.Key, g => (ICollection<TestItem>)g.ToList());
		notInBin = items.Count(a => !bins.ContainsKey(a.key));
		DumpBins(bins, Console.WriteLine);
		var result=await DistributeEquallyAsync(items, a => a.key, bins, null);
		DumpBins(bins, Console.WriteLine);
		AssertDistribution(bins.GroupBy(a => a.Key).Select(g => new BinItem<int> { PropertyValue = g.Key, Count = g.Count() }).ToList());
		var mustUpdateKey=result.ToList();
		Console.WriteLine($"Items not in bin: {notInBin}, items to update key: {mustUpdateKey.Count()}");
	}

	private static void AssertDistribution<T>(List<BinItem<T>> bins) where T : notnull, IEquatable<T>
	{
		var totalItems= TotalItemsCount(bins);
		var lowPerBin= MinItemsPerBin(bins);
		var highPerBin=MaxItemsPerBin(bins);
		Console.WriteLine($"Total items: {totalItems}, Bins: {bins.Count}, Low per bin: {lowPerBin}, High per bin: {highPerBin}, {string.Join(", ", bins)}");
		foreach (var bin in bins)
		{
			Assert.IsTrue(bin.Count == lowPerBin || bin.Count == highPerBin);
		}
	}


	[TestMethod]
	public async Task TestMethodDistributeAsync3()
	{
		var binItems=new List<BinItem<int>>()
	{
		new(){ PropertyValue=0, Count=114 },
		new(){ PropertyValue=1, Count=100 },
		new(){ PropertyValue=2, Count=97 },
		new(){ PropertyValue=3, Count=98 },
		new(){ PropertyValue=4, Count=120 },
		new(){ PropertyValue=5, Count=94 },
		new(){ PropertyValue=6, Count=99 },
		new(){ PropertyValue=7, Count=101 },
		new(){ PropertyValue=8, Count=100 },
		new(){ PropertyValue=9, Count=100 },
	};
		Console.WriteLine(string.Join(", ", binItems));
		_ = await DistributeEquallyAsync(binItems, async (count, from, to) =>
		{
			return await Task.FromResult(count);
		});
		AssertDistribution(binItems);
		Console.WriteLine(string.Join(", ", binItems));

	}

	[TestMethod]
	public async Task TestMethodDistributeAsync5()
	{
		var binItems=new List<BinItem<int>>()
	{
new () { PropertyValue=01, Count=4 },
new () { PropertyValue=02, Count=5 },
new () { PropertyValue=05, Count=7},
new () { PropertyValue=06, Count=4},
new () { PropertyValue=08, Count=3},
new () { PropertyValue=09, Count=5},
new () { PropertyValue=10, Count=6},
new () { PropertyValue=11, Count=5},
new () { PropertyValue=12, Count=5},
new () { PropertyValue=13, Count=4},
new () { PropertyValue=14, Count=2},
new () { PropertyValue=15, Count=8},
new () { PropertyValue=16, Count=3},
new () { PropertyValue=17, Count=6},
new () { PropertyValue=18, Count=6},
new () { PropertyValue=19, Count=9},
new () { PropertyValue=20, Count=4},
};
		Console.WriteLine(string.Join(", ", binItems));
		_ = await DistributeEquallyAsync(binItems, async (count, from, to) =>
		{
			return await Task.FromResult(count);
		});
		AssertDistribution(binItems);
		Console.WriteLine(string.Join(", ", binItems));

	}

	[TestMethod]
	public async Task TestMethodDistributeAsync4()
	{
		var binItems=new List<BinItem<string>>()
	{
		new(){ PropertyValue="A", Count=11 },
		new(){ PropertyValue="B", Count=3 },
		new(){ PropertyValue="C", Count=5 },
	};
		Console.WriteLine(string.Join(", ", binItems));
		_ = await DistributeEquallyAsync(binItems, async (count, from, to) =>
		{
			return await Task.FromResult(count);
		});
		AssertDistribution(binItems);
		Console.WriteLine(string.Join(", ", binItems));

	}

	[TestMethod]
	public async Task TestMethodDistributeAsyncDB1()
	{
		using var context = ProcessingDbContext.CreateInMemoryContext();
		var bins = await context.ProcessingItems
			.GroupBy(a=>a.ProcessingDate)
			.Select(g=> new BinItem<DateOnly>() { PropertyValue=g.Key, Count=g.Count() })
			.ToListAsync(TestContext.CancellationToken);
		Console.WriteLine(string.Join(", ", bins));
		_ = await DistributeEquallyAsync(bins, async (count, from, to) =>
		{
			var moved = 0;
			(await context.ProcessingItems
					.Where(a => a.ProcessingDate == from)
					.Take(count)
					.ToListAsync(TestContext.CancellationToken))
					.ForEach(itemToMove =>
					{
						itemToMove.ProcessingDate = to;
						moved++;
					});
			// must save here or the next select will get out of synch results
			await context.SaveChangesAsync();
			return await Task.FromResult(moved);
		});
		// re select
		bins = await context.ProcessingItems.GroupBy(a => a.ProcessingDate)
			.Select(g => new BinItem<DateOnly>() { PropertyValue = g.Key, Count = g.Count() })
			.ToListAsync(TestContext.CancellationToken);
		Console.WriteLine(string.Join(", ", bins));
		AssertDistribution(bins);
	}

	[TestMethod]
	public async Task TestMethodDistributeAsyncDB2()
	{
		using var context = ProcessingDbContext.CreateInMemoryContext();
		var bins = await context.ProcessingItems
			.GroupBy(a=>a.ProcessingDate)
			.Select(g=> new BinItem<DateOnly>() { PropertyValue=g.Key, Count=g.Count() })
			.ToListAsync(TestContext.CancellationToken);
		Console.WriteLine(string.Join(", ", bins));
		var alreadyMoved=new HashSet<int>();
		_ = await DistributeEquallyAsync(bins, async (count, from, to) =>
		{
			var moved = 0;
			(await context.ProcessingItems
					.Where(a => a.ProcessingDate == from && !alreadyMoved.Contains(a.Id))
					.Take(count)
					.ToListAsync(TestContext.CancellationToken))
					.ForEach(itemToMove =>
					{
						itemToMove.ProcessingDate = to;
						moved++;
						alreadyMoved.Add(itemToMove.Id);
					});
			return await Task.FromResult(moved);
		});
		// must save here or the next select will get out of synch results
		await context.SaveChangesAsync();
		// re select
		bins = await context.ProcessingItems.GroupBy(a => a.ProcessingDate)
			.Select(g => new BinItem<DateOnly>() { PropertyValue = g.Key, Count = g.Count() })
			.ToListAsync(TestContext.CancellationToken);
		Console.WriteLine(string.Join(", ", bins));
		AssertDistribution(bins);
	}

}
