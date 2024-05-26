/// <summary>
/// Checks if a terminal block can be interacted with by the PB. Refhack intentionally unaccounted for.
/// </summary>
bool IsValidBlock(IMyTerminalBlock b)
{
    return b != null && !b.Closed && b.IsFunctional;
}