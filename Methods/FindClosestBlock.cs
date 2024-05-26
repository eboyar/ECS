/// <summary>
/// Finds the closest block, to a given block, from a list of blocks.
/// </summary>
T FindClosestBlock<T>(List<T> blocks, Vector3D position) where T : IMyTerminalBlock
{
    T closestBlock = default(T);
    double closestDistance = double.MaxValue;

    foreach (var block in blocks)
    {
        double distance = Vector3D.Distance(position, block.GetPosition());
        if (distance < closestDistance)
        {
            closestDistance = distance;
            closestBlock = block;
        }
    }

    return closestBlock;
}