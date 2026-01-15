using static EqualDistributionLib.EqualDistribution;
// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
var bins=new List<BinItem<string>>()
{
    new(){ PropertyValue="A", Count=10},
    new(){ PropertyValue="B", Count=50},
    new(){ PropertyValue="C", Count=90},
    new(){ PropertyValue="D", Count=30},
};
await DistributeEquallyAsync(bins, async (count, from, to) =>
{
    Console.WriteLine($"Moving {count} items from {from} to {to}");
    return await Task.FromResult(count);
});
 Console.WriteLine(string.Join(", ", bins));