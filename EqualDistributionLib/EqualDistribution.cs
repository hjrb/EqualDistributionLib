using System.Numerics;

namespace EqualDistributionLib;


public class EqualDistribution
{

	public class BinItem<TPropertyValue>
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{
		public required TPropertyValue PropertyValue { get; init; }
		public int Count { get; set; }
		public override string ToString() => $"{PropertyValue}=>{Count}";
		public override int GetHashCode() => HashCode.Combine(PropertyValue, Count);
		public override bool Equals(object? obj) => obj is BinItem<TPropertyValue> other && other.PropertyValue.Equals(this.PropertyValue) && other.Count == this.Count;

	}


	/// <summary>
	/// Take a list of items and distribute them equally into bins 
	/// The correlation item to bin is determined the Func keySelector.
	/// If there is no bin for an item, it is added to the bin with the least items.
	/// Then the items are redistributed to achieve equal distribution across bins.
	/// Notes: this approach will 
	/// 1.) add the items in the source items collection to the bins if they are missing. 2.)
	/// 2.) actually move the items
	/// This is well suited if the items are already in memory
	/// </summary>
	/// <typeparam name="TItem">the item type</typeparam>
	/// <typeparam name="TPropertyValue">the type of the Property</typeparam>
	/// <param name="items">source of items</param>
	/// <param name="PropertySelector">function selecting the Property for each item to sort the item into the bin</param>
	/// <param name="bins">the bins to distribute the items into. These can be empty at the start or preloaded</param>
	/// <returns>the enumeration of the items that have been assigned to bin with deviating key. Those items must be updated to assign the key of the bin to the Property to persist and equal distribution</returns>
	public static async Task<IEnumerable<(TItem Item, TPropertyValue Property)>>
		DistributeEquallyAsync<TItem, TPropertyValue>(
			IEnumerable<TItem> items,
			Func<TItem, TPropertyValue> PropertySelector,
			Dictionary<TPropertyValue, ICollection<TItem>> bins,
			IEqualityComparer<TItem>? equalityComparer = null
		)
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{

		equalityComparer ??= EqualityComparer<TItem>.Default;

		// Phase 1 add the items not assigned to any bin
		foreach (var item in items)
		{
			var key = PropertySelector(item);
			// get the bin for the items key
			if (bins.ContainsKey(key))
			{
				// add the item to the bin if it not alreay in the bin
				if (!bins[key].Contains(item, equalityComparer))
				{
					bins[key].Add(item);
				}
			}
			else
			{
				// get bin with least amount of items
				// TODO we could have a parameter to specify how to handle this case
				var targetBin = bins.OrderBy(b => b.Value.Count).First();
				targetBin.Value.Add(item);
			}
		}
		var movedItems=new List<(TItem Item, TPropertyValue Property)>();
		await DistributeEquallyAsync(bins.Select(a=>new BinItem<TPropertyValue>() { PropertyValue=a.Key, Count=a.Value.Count }), async (count, from, to) =>
		{
			// move 'count' items from 'from' bin to 'to' bin
			var sourceBin = bins[from];
			var targetBin = bins[to];
			var moved=0;
			for (var i = 0; i < count; i++)
			{
				if (sourceBin.Count == 0) break; // should not happen
				var itemToMove = sourceBin.First();
				sourceBin.Remove(itemToMove);
				targetBin.Add(itemToMove);
				++moved;
			}
			return await Task.FromResult(moved);
		});
		return await Task.FromResult(movedItems);
	}

	/// <summary>
	/// Redistributes items among bins so that each bin has as close to an equal number of items as possible, moving items
	/// asynchronously between bins as needed.
	/// The method attempts to minimize the difference in item counts between bins by repeatedly invoking
	/// the provided moveItem delegate. The actual number of items moved depends on the initial distribution. The method
	/// does not guarantee perfect balance if the total number of items cannot be evenly divided among bins.</remarks>
	/// This approach does not actuall move the items but call the delegate moveItem so the caller can implement the actual move logic.
	/// This is well suited if the items have not been loaded to memory yet, e.g. in database scenario
	/// Example:
	/// You have DB table processingTable with a column "processingDate". You have a huge number of items and want to distribute the processing across n-dates equally.
	/// <![CDATA[
	///		var grouped=processingTable.GroupBy(a=>a.processingDate).Select(g=>new {g.Key, Count=g.Count()}).ToArray().Select(a=>new BinItem() {Key=a.Key, Count=a.Count}).ToArray()
	///		var alreadyMoved=new HashSet<int>();
	///			await EqualDistribution(grouped, (from,to)=> {
	///				var itemToMove=processingTable.Where(a=>a.processingDate==from && !alreadyMoved.Contains(item.ID)).First();
	///				itemToMove.processingDate=to; // moves the processingDate
	///				alreadyMoved.Add(itemToMove.ID);
	///			}
	///		);
	///		await dbContext.SaveChangesAsync();
	///	]]>
	/// <typeparam name="TPropertyValue">The type that identifies each bin. Must be non-null and support equality comparison.</typeparam>
	/// <param name="bins">A collection of tuples, each containing a bin identifier and the current item count for that bin. The method
	/// attempts to balance the item counts across these bins.</param>
	/// <param name="moveItem">An asynchronous delegate that moves a single item from one bin to another. The first parameter is the source bin
	/// identifier, and the second is the target bin identifier.</param>
	/// <returns>A task that represents the asynchronous redistribution operation.</returns>
	public static async Task
		DistributeEquallyAsync<TPropertyValue>(
			IEnumerable<BinItem<TPropertyValue>> bins,
			Func<int, TPropertyValue, TPropertyValue, Task<int>> moveItem
		)
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{
		var totalItems = bins.Sum(b => b.Count);
		var hiPerBin=(int)Math.Round(totalItems / (double)bins.Count(), MidpointRounding.ToPositiveInfinity);
		var lowPerBin = (int)Math.Round(totalItems / (double)bins.Count(), MidpointRounding.ToNegativeInfinity);
		var totalMoved=0;
		for (var i = 0; i < bins.Count(); i++)
		{
			var bin = bins.ElementAt(i);
			var movedItems = 0;
			while (bin.Count > hiPerBin)
			{
				// this should do all required moves all at once.
				// but as this is in an outer data source things might have changed since the input bin distribution has been created
				// find target bin as the bin that currently has the least number of items
				var targetBin = bins.OrderBy(b => b.Count).First();
				if (object.Equals(targetBin, bin)) break; // no more items to move
				var toMove=Math.Min(hiPerBin-targetBin.Count, bin.Count-lowPerBin);
				if (toMove==0) {
					throw new Exception("Nothing can be moved");
				}
				var moved= await DoMove(moveItem, bin, targetBin, toMove);
				movedItems +=moved;
				if (moved<=0) {
					break; // nothing moved - can't have optimum here
				}
			}
			while (bin.Count < lowPerBin)
			{
				// this should do all required moves all at once
				// but as this is in an outer data source things might have changed since the input bin distribution has been created
				// find target bin as the bin that currently has the maximum number of items
				var sourceBin = bins.OrderByDescending(b => b.Count).First();
				if (object.Equals(sourceBin, bin)) break; // no more items to move
				var toMove = Math.Min(lowPerBin - bin.Count, sourceBin.Count - lowPerBin);
				if (toMove==0) {
					throw new Exception("Nothing can be moved");
				}
				var moved= await DoMove(moveItem, sourceBin, bin, toMove);
				movedItems +=moved;
				if (moved<=0) {
					break; // nothing moved
				}
			}
			totalMoved += movedItems;
		}
	}

	private static async Task<int> DoMove<TPropertyValue>(
		Func<int, TPropertyValue, TPropertyValue, Task<int>> moveItem,
		BinItem<TPropertyValue> source,
		BinItem<TPropertyValue> targetBin,
		int toMove
	) where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{
		var moved=await moveItem(toMove,source.PropertyValue, targetBin.PropertyValue);
		source.Count -= moved;
		targetBin.Count += moved;
		return moved;
	}

	public static void DumpBins<TItem, TPropertyValue>(Dictionary<TPropertyValue, ICollection<TItem>> bins, Action<string> dumper)
	where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{
		dumper($"Bins: {bins.Count}, Keys: {string.Join(", ", bins.Keys.Order())}, Total Items in bins: {bins.Values.Sum(a => a.Count)}, Average: {bins.Values.Sum(a => a.Count) / (double)bins.Count}");
		foreach (var bin in bins.OrderBy(a=>a.Key))
		{
			dumper($"Bin {bin.Key}: {bin.Value.Count} items");
		}

	}
}