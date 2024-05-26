/// <summary>
/// Fills a block's inventory up to the specified amount, or the closest possible amount.
/// </summary>
void FillToAmount(IMyTerminalBlock b, MyItemType itemType, double fillAmount)
{
    if (!b.HasInventory)
    {
        return;
    }

    var inventory = b.GetInventory();
    var currentAmount = (double)inventory.GetItemAmount(itemType);

    if (currentAmount >= fillAmount)
    {
        return;
    }

    var requiredAmount = fillAmount - currentAmount;
    Container container = null;
    foreach (var c in containers)
    {
        if (c.Items[itemType] >= requiredAmount)
        {
            container = c;
            break;
        }
    }

    if (container == null || container.CargoContainer == null)
    {
        return;
    }

    var transferAmount = Math.Min(container.Items[itemType], requiredAmount);
    var item = container.CargoContainer.GetInventory().FindItem(itemType).Value;

    inventory.TransferItemFrom(container.CargoContainer.GetInventory(), item, (MyFixedPoint)transferAmount);
    container.RemoveItem(itemType, transferAmount);
}