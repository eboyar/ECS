/// <summary>
/// Fills a block's inventory up to the specified percentage, or the closest possible percentage. Agnostic of the world inventory multiplier.
/// </summary>
void FillToPercent(IMyTerminalBlock b, MyItemType itemType, double fillPercentage)
{
    if (!b.HasInventory)
    {
        return;
    }

    var inventory = b.GetInventory();
    var maxVolume = (double)inventory.MaxVolume;
    var targetVolume = maxVolume * fillPercentage / 100;
    var volumePerItem = itemType.GetItemInfo().Volume;
    var currentVolume = (double)inventory.GetItemAmount(itemType) * volumePerItem;

    if (currentVolume >= targetVolume)
    {
        return;
    }

    var requiredVolume = targetVolume - currentVolume;
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
    var transferAmount = Math.Floor(transferVolume / volumePerItem);
    var item = container.CargoContainer.GetInventory().FindItem(itemType).Value;

    inventory.TransferItemFrom(container.CargoContainer.GetInventory(), item, (MyFixedPoint)transferAmount);
    container.RemoveItem(itemType, transferAmount);
}