using EqualDistributionLib;

using static EqualDistributionLib.EqualDistribution;

namespace EqualDistributionTest;

[TestClass]
public sealed class TestEqualDistribution
{

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
		EqualDistribution.DumpBins(bins, Console.WriteLine);
		var result=await EqualDistribution.DistributeEquallyAsync(items, a => a.key, bins, null);
		var totalItems=bins.Values.Sum(b=>b.Count);
		var targetPerBin=totalItems/bins.Count;
		EqualDistribution.DumpBins(bins, Console.WriteLine);
		foreach (var bin in bins)
		{
			Assert.IsTrue(bin.Value.Count == targetPerBin || bin.Value.Count == targetPerBin + 1);
		}
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
		EqualDistribution.DumpBins(bins, Console.WriteLine);
		var result=await EqualDistribution.DistributeEquallyAsync(items, a => a.key, bins, null);
		var totalItems=bins.Values.Sum(b=>b.Count);
		var lowPerBin=Math.Round(totalItems/(double)bins.Count, MidpointRounding.ToNegativeInfinity);
		var highPerBin=Math.Round(totalItems/(double)bins.Count, MidpointRounding.ToPositiveInfinity);
		EqualDistribution.DumpBins(bins, Console.WriteLine);
		foreach (var bin in bins)
		{
			Assert.IsTrue(bin.Value.Count == lowPerBin || bin.Value.Count == highPerBin);
		}
		var mustUpdateKey=result.ToList();
		Console.WriteLine($"Items not in bin: {notInBin}, items to update key: {mustUpdateKey.Count()}");

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
	await EqualDistribution.DistributeEquallyAsync(binItems, async (count, from, to)=>{
		return await Task.FromResult(count);
		});

}
}
