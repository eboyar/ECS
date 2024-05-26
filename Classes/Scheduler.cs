/// <summary>
/// A scheduler that manages a queue of routines.
/// </summary>
class Scheduler
{
    private Queue<IEnumerator<bool>> routines = new Queue<IEnumerator<bool>>();
    private IEnumerator<bool> routine = null;

    /// <summary>
    /// Adds a routine to the scheduler's queue.
    /// </summary>
    /// <param name="routine">The routine to add.</param>
    public void AddRoutine(IEnumerator<bool> routine)
    {
        routines.Enqueue(routine);
    }

    /// <summary>
    /// Executes the next routine in the queue.
    /// </summary>
    public void ExecuteRoutine()
    {
        if (routine != null)
        {
            bool hasMoreSteps = routine.MoveNext();
            if (!hasMoreSteps)
            {
                routine.Dispose();
                routine = null;
            }
        }
        if (routine == null && routines.Count > 0)
        {
            routine = routines.Dequeue();
            ExecuteRoutine();
        }
    }
    public bool IsEmpty()
    {
        return routines.Count == 0;
    }
    public bool IsActive()
    {
        return routine != null;
    }
}