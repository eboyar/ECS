/// <summary>
/// Completely fills a block's inventory with the specified item type, agnostic of the world inventory multiplier.
/// </summary>
void FillToFull(IMyTerminalBlock b, MyItemType itemType)
{
    if (!b.HasInventory)
    {
        return;
    }

    var inventory = b.GetInventory();
    var maxVolume = (double)inventory.MaxVolume;
    var volumePerItem = itemType.GetItemInfo().Volume;
    var currentVolume = (double)inventory.GetItemAmount(itemType) * volumePerItem;

    if (currentVolume >= maxVolume)
    {
        return;
    }

    var requiredVolume = maxVolume - currentVolume;
    Container container = null;
    foreach (var c in containers)
    {
        var containerVolume = c.Items[itemType] * volumePerItem;
        if (containerVolume >= requiredVolume)
        {
            container = c;
            break;
        }
    }

    if (container == null || container.CargoContainer == null)
    {
        return;
    }

    var transferVolume = Math.Min(container.Items[itemType] * volumePerItem, requiredVolume);
    var transferAmount = transferVolume / volumePerItem;
    var item = container.CargoContainer.GetInventory().FindItem(itemType).Value;

    inventory.TransferItemFrom(container.CargoContainer.GetInventory(), item, (MyFixedPoint)transferAmount);
    container.RemoveItem(itemType, transferAmount);
}