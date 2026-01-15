# Purpose
a library in C# that helps distributing items equally to bins based on a property value.

# Example
 You have a DB table processingTable with a column "processingDate". 
 You have a huge number of items or processig an item requires many resources and want to distribute the processing across n-dates equally.
 You are use EF core to access the DB.
 ```
		var grouped=dbContext.ProcessingTable
			.GroupBy(a=>a.ProcessingDate)
			.Select(g=>new BinItem() {Key=g.Key, Count=g.Count()})
			.ToArray()
		var alreadyMoved=new HashSet<int>();
		await EqualDistribution(grouped, (count, from,to)=> {
	  	processingTable
        .Where(a=>a.ProcessingDate==from && !alreadyMoved.Contains(a.Id))
        .Take(count)
        .ToList()
        .ForEach(itemToMove=> {
  		    itemToMove.processingDate=to; // moves the processingDate
    		  alreadyMoved.Add(itemToMove.ID);
        });
     });
		await dbContext.SaveChangesAsync();
	```
