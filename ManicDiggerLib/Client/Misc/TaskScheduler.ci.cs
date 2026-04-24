/// <summary>
/// Schedules and coordinates per-frame execution of client mod lifecycle hooks,
/// dispatching background work to worker threads when multithreading is available
/// and falling back to sequential execution otherwise.
/// </summary>
public class TaskScheduler
{
    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    /// <summary>
    /// Per-mod background task state. Initialised once via <see cref="Initialise"/>.
    /// </summary>
    private BackgroundAction[] _actions;
    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    public TaskScheduler(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initialises the background action slots for all currently registered client mods.
    /// Must be called once before the first <see cref="Update"/> call.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    public void Initialise()
    {
        _actions = new BackgroundAction[game.ClientMods.Count];
        for (int i = 0; i < game.ClientMods.Count; i++)
            _actions[i] = new BackgroundAction();
    }

    /// <summary>
    /// Drives the mod update lifecycle for one frame.
    /// When multithreading is available, background work runs on worker threads
    /// while the main thread proceeds; all read-write work is deferred until
    /// all background tasks from the previous frame have completed.
    /// When multithreading is unavailable, all hooks run sequentially.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="dt">Delta time in seconds since the last frame.</param>
    public void Update(float dt)
    {
        if (platform.MultithreadingAvailable())
            UpdateMultithreaded(dt);
        else
            UpdateSingleThreaded(dt);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs main-thread read-only hooks, then — if all background tasks from the
    /// previous frame are done — flushes commit actions, runs read-write hooks,
    /// and dispatches new background tasks.
    /// </summary>
    private void UpdateMultithreaded(float dt)
    {
        RunReadOnlyMainThread(dt);

        if (!AllBackgroundTasksFinished())
            return;

        RunReadWriteMainThread(dt);
        FlushCommitActions();
        DispatchBackgroundTasks(dt);
    }

    /// <summary>
    /// Runs all mod hooks sequentially on the main thread:
    /// read-only main, read-only background, then read-write main.
    /// </summary>
    private void UpdateSingleThreaded(float dt)
    {
        RunReadOnlyMainThread(dt);

        for (int i = 0; i < game.ClientMods.Count; i++)
            game.ClientMods[i].OnReadOnlyBackgroundThread(dt);

        RunReadWriteMainThread(dt);
        FlushCommitActions();
    }

    /// <summary>
    /// Returns <c>true</c> if no background task is still active and unfinished.
    /// </summary>
    private bool AllBackgroundTasksFinished()
    {
        for (int i = 0; i < game.ClientMods.Count; i++)
        {
            BackgroundAction action = _actions[i];
            if (action.Active && !action.Finished)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Marks each background slot as active and queues its work item on the thread pool.
    /// </summary>
    private void DispatchBackgroundTasks(float dt)
    {
        for (int i = 0; i < game.ClientMods.Count; i++)
        {
            int captured = i;
            _actions[captured].Active = true;
            _actions[captured].Finished = false;
            game.Platform.QueueUserWorkItem(
                CreateBackgroundAction(captured, dt, () => _actions[captured].Finished = true));
        }
    }

    /// <summary>Calls <c>OnReadOnlyMainThread</c> on every registered client mod.</summary>
    private void RunReadOnlyMainThread(float dt)
    {
        for (int i = 0; i < game.ClientMods.Count; i++)
            game.ClientMods[i].OnReadOnlyMainThread(dt);
    }

    /// <summary>Calls <c>OnReadWriteMainThread</c> on every registered client mod.</summary>
    private void RunReadWriteMainThread(float dt)
    {
        for (int i = 0; i < game.ClientMods.Count; i++)
            game.ClientMods[i].OnReadWriteMainThread(dt);
    }

    /// <summary>Executes all pending commit actions then clears the queue.</summary>
    private void FlushCommitActions()
    {
        foreach (Action action in game.commitActions)
            action();
        game.commitActions.Clear();
    }

    /// <summary>
    /// Builds an <see cref="Action"/> that invokes a mod's background hook
    /// then calls <paramref name="onFinished"/> when complete.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="modIndex">Index of the mod in <c>game.clientmods</c>.</param>
    /// <param name="dt">Delta time passed through to the background hook.</param>
    /// <param name="onFinished">Callback invoked on the worker thread when the hook returns.</param>
    /// <returns>A self-contained <see cref="Action"/> safe to queue on the thread pool.</returns>
    private Action CreateBackgroundAction(int modIndex, float dt, Action onFinished)
    {
        return () =>
        {
            game.ClientMods[modIndex].OnReadOnlyBackgroundThread(dt);
            onFinished();
        };
    }
}

/// <summary>
/// Tracks the execution state of a single background task slot.
/// </summary>
internal class BackgroundAction
{
    /// <summary>
    /// Whether this slot has been dispatched to the thread pool and not yet reset.
    /// </summary>
    internal bool Active;

    /// <summary>
    /// Whether the background work has completed. Set to <c>true</c> by the worker thread.
    /// </summary>
    internal bool Finished;
}