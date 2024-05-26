/// <summary>
/// Represents a status logger that displays status in the PB.
/// </summary>
class StatusLogger
{
    private bool split;
    private string routineReport;
    private string splitRoutineReport;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusLogger"/> class.
    /// </summary>
    /// <param name="split">A boolean value indicating whether to split the routine report.</param>
    public StatusLogger(bool split)
    {
        this.split = split;
    }

    /// <summary>
    /// Sets the routine report.
    /// </summary>
    /// <param name="report">The routine report to set.</param>
    public void RReport(string report)
    {
        routineReport = report;
    }

    /// <summary>
    /// Sets the split routine report.
    /// </summary>
    /// <param name="report">The split routine report to set.</param>
    public void SRReport(string report)
    {
        splitRoutineReport = report;
    }

    /// <summary>
    /// Gets the routine report.
    /// </summary>
    /// <returns>The routine report.</returns>
    public string getRReport()
    {
        return routineReport;
    }

    /// <summary>
    /// Gets the split routine report.
    /// </summary>
    /// <returns>The split routine report.</returns>
    public string getSRReport()
    {
        return splitRoutineReport;
    }

    /// <summary>
    /// Builds the status message based on the specified running state.
    /// </summary>
    /// <param name="isRunning">A boolean value indicating whether the script is running.</param>
    /// <returns>The status message.</returns>
    public string BuildStatus(bool isRunning)
    {
        string status;
        if (!isRunning)
        {
            RReport("");
            SRReport("");
            status = "ECS Script v" + version + "\n|- WAITING -|\n\n";
        }
        else
        {
            status = "ECS Script v" + version + "\n|- RUNNING -|\n" + routineReport + "\n" + splitRoutineReport;
        }

        return status;
    }
}