/// <summary>
/// Describes the cached state of a container. More performant then getting the inventory.
/// </summary>
class Container
{
    public IMyCargoContainer CargoContainer { get; set; }
    public Dictionary<MyItemType, double> Items { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Container"/> class.
    /// </summary>
    /// <param name="cargoContainer">The cargo container associated with this container.</param>
    /// <param name="itemTypes">The dictionary of item types and their corresponding names.</param>
    public Container(IMyCargoContainer cargoContainer, Dictionary<string, MyItemType> itemTypes)
    {
        CargoContainer = cargoContainer;
        Items = new Dictionary<MyItemType, double>(10);
        foreach (var itemType in itemTypes.Values)
        {
            Items[itemType] = 0;
            UpdateInventory();
        }
    }
    /// <summary>
    /// Updates the state based on the container's inventory.
    /// </summary>
    public void UpdateInventory()
    {
        if (CargoContainer == null || CargoContainer.Closed || !CargoContainer.IsFunctional)
        {
            return;
        }

        var inventory = CargoContainer.GetInventory();
        foreach (var item in Items.Keys.ToList())
        {
            Items[item] = (double)inventory.GetItemAmount(item);
        }
    }

    /// <summary>
    /// Removes items from the cached state of the container.
    /// </summary>
    /// <param name="itemType">The type of item to remove.</param>
    /// <param name="amount">The amount of the item to remove.</param>
    public void RemoveItem(MyItemType itemType, double amount)
    {
        if (Items.ContainsKey(itemType))
        {
            Items[itemType] -= amount;
            if (Items[itemType] < 0)
            {
                Items[itemType] = 0;
            }
        }
    }
}