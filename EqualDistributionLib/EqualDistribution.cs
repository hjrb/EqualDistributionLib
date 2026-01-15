using System.Numerics;

namespace EqualDistributionLib;


/// <summary>
/// this class offer two methods to make the distribution of items to bins as equal as possible
/// So if you have three bins: A:11, B:3, C:5 initially after apply the method DistributeEquallyAsync the distribution will be
/// A: 7, B: 6, C: 6
/// </summary>
public class EqualDistribution
{

	/// <summary>
	/// Represents an item within a bin, associating a property value with its occurrence count.
	/// </summary>
	/// <typeparam name="TPropertyValue">The type of the property value associated with the bin item. Must be non-null and implement <see
	/// cref="IEquatable{T}"/>.</typeparam>
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
	/// adds items from the itmes collection to the bins trying to make the distribution equal
	/// Note: for items which property value does NOT match any bin the item is added to the bin with the least nummber of items
	/// else it will be to the matching bin
	/// </summary>
	/// <typeparam name="TItem"></typeparam>
	/// <typeparam name="TPropertyValue"></typeparam>
	/// <param name="items"></param>
	/// <param name="PropertySelector"></param>
	/// <param name="bins"></param>
	/// <param name="equalityComparer"></param>
	public static void
		AddMissingItems<TItem, TPropertyValue>(
			IEnumerable<TItem> items,
			Func<TItem, TPropertyValue> PropertySelector,
			Dictionary<TPropertyValue, ICollection<TItem>> bins,
			IEqualityComparer<TItem> equalityComparer)
	where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{
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
	}

	/// <summary>
	/// Redistributes items among bins so that each bin has as close to an equal number of items as possible, by moving items
	/// asynchronously between bins as needed.
	/// The method attempts to minimize the difference in item counts between bins by repeatedly invoking
	/// the provided moveItem delegate. The actual number of items moved depends on the initial distribution. The method
	/// does not guarantee perfect balance if the total number of items cannot be evenly divided among bins.
	/// This approach does not actuall move the items but call the delegate moveItem so the caller can implement the actual move logic.
	/// This is well suited if the items have not been loaded to memory yet, e.g. in database scenario
	/// Example:
	/// You have DB table processingTable with a column "processingDate". 
	/// You have a huge number of items and want to distribute the processing across n-dates equally.
	/// <![CDATA[
	///		var grouped=processingTable
	///			.GroupBy(a=>a.processingDate)
	///			.Select(a=>new BinItem() {Key=a.Key, Count=a.Count})
	///			.ToArray()
	///		var alreadyMoved=new HashSet<int>();
	///			await EqualDistribution(grouped, (from,to)=> {
	///				// optional
	///				var itemToMove=processingTable.Where(a=>a.processingDate==from && !alreadyMoved.Contains(item.ID)).First();
	///				itemToMove.processingDate=to; // moves the processingDate
	///				alreadyMoved.Add(itemToMove.ID);
	///			}
	///		);
	///		await dbContext.SaveChangesAsync();
	///	]]>
	/// <typeparam name="TPropertyValue">The type that identifies each bin. Must be non-null and support equality comparison.</typeparam>
	/// <param name="bins">A collection of tuples, each containing a bin identifier and the current item count for that bin.</param>
	/// <param name="moveItem">An asynchronous delegate that moves a single item from one bin to another. The first parameter is the source bin
	/// property value, and the second is the target bin property.</param>
	/// <returns>The total number of moved items.</returns>
	public static async Task<int>
		DistributeEquallyAsync<TPropertyValue>(
			IEnumerable<BinItem<TPropertyValue>> bins,
			Func<int, TPropertyValue, TPropertyValue, Task<int>> moveItem
		)
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{
		var totalItems = TotalItemsCount(bins);
		var hiPerBin = MaxItemsPerBin(bins);
		var lowPerBin = MinItemsPerBin(bins);
		var totalMovedItems=0;
		// process each bin only once
		for (var i = 0; i < bins.Count(); i++)
		{
			var bin = bins.ElementAt(i);
			var binMovedItems = 0;
			// too many?
			while (bin.Count > hiPerBin)
			{
				// find target bin as the bin that currently has the least number of items
				var targetBin = bins.OrderBy(b => b.Count).First();
				if (object.Equals(targetBin, bin)) break; // no more items to move
				var toMove=Math.Min(hiPerBin-targetBin.Count, bin.Count-lowPerBin);
				if (toMove == 0)
				{
					// tread this a en error
					throw new Exception("Nothing can be moved");
				}
				var moved= await DoMove(moveItem, bin, targetBin, toMove);
				binMovedItems += moved;
				if (moved <= 0)
				{
					break; // nothing moved - can't have optimum here
				}
			}
			// to few?
			while (bin.Count < lowPerBin)
			{
				// find target bin as the bin that currently has the maximum number of items
				var sourceBin = bins.OrderByDescending(b => b.Count).First();
				if (object.Equals(sourceBin, bin)) break; // no more items to move
				var toMove = Math.Min(lowPerBin - bin.Count, sourceBin.Count - lowPerBin);
				if (toMove == 0)
				{
					// tread this a en error
					throw new Exception("Nothing can be moved");
				}
				var moved= await DoMove(moveItem, sourceBin, bin, toMove);
				binMovedItems += moved;
				if (moved <= 0)
				{
					break; // nothing moved
				}
			}
			totalMovedItems += binMovedItems;
		}
		return totalMovedItems;
	}


	/// <summary>
	/// Take a list of items and distribute them equally into bins 
	/// The correlation item to bin is determined the Func PropertySelector.
	/// If there is no bin for an item, it is added to the bin with the least items.
	/// Then the items are redistributed to achieve equal distribution across bins.
	/// That means: afte the distribution there will be items that are in bin that does NOT match the property value
	/// Notes: this approach will 
	/// 1.) add the items in the source items collection to the bins if they are missing 
	/// 2.) actually move the items
	/// This is well suited if the items are already in memory but might cause performance issues if loading all items to memory is slow.
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
		AddMissingItems(items, PropertySelector, bins, equalityComparer);
		// Phase 2: equaly distribut items
		var movedItems=new List<(TItem Item, TPropertyValue Property)>();
		_ = await DistributeEquallyAsync(
				bins: bins.Select(a => new BinItem<TPropertyValue>() { PropertyValue = a.Key, Count = a.Value.Count }),
				moveItem: async (count, from, to) =>
				{
					// move 'count' items from 'from' bin to 'to' bin
					var sourceBin = bins[from];
					var targetBin = bins[to];
					var moved=0;
					// ICollection only supports moving one item at a time
					for (var i = 0; i < count; i++)
					{
						if (sourceBin.Count == 0) break; // should not happen
						var itemToMove = sourceBin.First();
						_ = sourceBin.Remove(itemToMove);
						targetBin.Add(itemToMove);
						++moved;
						movedItems.Add((itemToMove, to));
					}
					return await Task.FromResult(moved);
				}
		);
		return await Task.FromResult(movedItems);
	}

	/// <summary>
	/// Calculates the total number of items across all bins in the specified collection.
	/// </summary>
	/// <typeparam name="TPropertyValue">The type of the value associated with each bin. Must be non-null and support equality comparison.</typeparam>
	/// <param name="bins">A collection of bins whose item counts will be summed. Cannot be null.</param>
	/// <returns>The sum of the item counts for all bins in the collection. Returns 0 if the collection is empty.</returns>
	public static int
		TotalItemsCount<TPropertyValue>(
			IEnumerable<BinItem<TPropertyValue>> bins
		)
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
		=> bins.Sum(b => b.Count);

	/// <summary>
	/// return the maximum number of items per bins to be expected after distribution has been done
	/// </summary>
	/// <typeparam name="TPropertyValue"></typeparam>
	/// <param name="bins"></param>
	/// <returns></returns>
	public static int
		MinItemsPerBin<TPropertyValue>(
			IEnumerable<BinItem<TPropertyValue>> bins
		)
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
		=> (int)Math.Round(TotalItemsCount(bins) / (double)bins.Count(), MidpointRounding.ToNegativeInfinity);

	/// <summary>
	/// return the minimum number of items per bins to be expected after distribution has been done
	/// </summary>
	/// <typeparam name="TPropertyValue"></typeparam>
	/// <param name="bins"></param>
	/// <returns></returns>
	public static int
		MaxItemsPerBin<TPropertyValue>(
			IEnumerable<BinItem<TPropertyValue>> bins
		)
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
		=> (int)Math.Round(TotalItemsCount(bins) / (double)bins.Count(), MidpointRounding.ToPositiveInfinity);

	private static async Task<int>
		DoMove<TPropertyValue>(
			Func<int, TPropertyValue, TPropertyValue, Task<int>> moveItem,
			BinItem<TPropertyValue> source,
			BinItem<TPropertyValue> targetBin,
			int toMove
		)
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{
		var moved=await moveItem(toMove,source.PropertyValue, targetBin.PropertyValue);
		source.Count -= moved;
		targetBin.Count += moved;
		return moved;
	}

	/// <summary>
	/// Outputs summary information and item counts for each bin in the specified collection using the provided dumper
	/// action.
	/// </summary>
	/// <remarks>The method outputs the total number of bins, the list of bin keys, the total number of items across
	/// all bins, and the average number of items per bin, followed by the item count for each individual bin. The order of
	/// output is determined by the sorted order of the bin keys.</remarks>
	/// <typeparam name="TItem">The type of items contained in each bin.</typeparam>
	/// <typeparam name="TPropertyValue">The type of the key used to identify each bin. Must be non-null and support equality comparison.</typeparam>
	/// <param name="bins">A dictionary mapping bin keys to collections of items. Each key represents a bin, and the associated collection
	/// contains the items in that bin.</param>
	/// <param name="dumper">An action that receives formatted strings containing summary and per-bin information. Used to output or log the bin
	/// data.</param>
	public static void
		DumpBins<TItem, TPropertyValue>(
			Dictionary<TPropertyValue, ICollection<TItem>> bins, Action<string> dumper
		)
		where TPropertyValue : notnull, IEquatable<TPropertyValue>
	{
		dumper($"Bins: {bins.Count}, Keys: {string.Join(", ", bins.Keys.Order())}, Total Items in bins: {bins.Values.Sum(a => a.Count)}, Average: {bins.Values.Sum(a => a.Count) / (double)bins.Count}");
		foreach (var bin in bins.OrderBy(a => a.Key))
		{
			dumper($"Bin {bin.Key}: {bin.Value.Count} items");
		}

	}
}