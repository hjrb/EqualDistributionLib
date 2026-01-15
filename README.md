# Purpose
a library in C# that helps distributing items equally to bins based on a property value.

# Examples
## In memory example
```csharp
	class TestItem(int key, string value) { public int key=key; public string value=value; }
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
	EqualDistribution.DumpBins(bins, Console.WriteLine);
```

## DB Example
 You have a DB table processingTable with a column "processingDate". 
 You have a huge number of items or processig an item requires many resources and want to distribute the processing across n-dates equally.
 You are use EF core to access the DB.
 ```csharp
var grouped=dbContext.ProcessingTable
	.GroupBy(a=>a.ProcessingDate) // ProcesingDate must not be null
	.Select(g=>new BinItem() {Key=g.Key, Count=g.Count()})
	.ToArray()
var alreadyMoved=new HashSet<int>();
await EqualDistribution(grouped, (count, from,to)=> {
	var moved=0;
	(await processingTable
	.Where(a=>a.ProcessingDate==from && !alreadyMoved.Contains(a.Id))
	.Take(count)
	.ToListAsync())
	.ForEach(itemToMove=> {
		itemToMove.processingDate=to; // moves the processingDate
		// required or the next select will return out-of-synch results
		// altenative is to call SaveChangesAsynch() at the end of every move operation: slower but will keep partial improvements in case of some error saving all changes all at once
	  		  alreadyMoved.Add(itemToMove.ID); 
			moved++;
	    });
		return moved;
});
await dbContext.SaveChangesAsync();

```
