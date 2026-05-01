//This class runs all the game's client mods every frame in the right order, with optional multithreading.
//Each mod has three hooks:

//OnReadOnlyMainThread — safe to read game state, runs on main thread
//OnReadOnlyBackgroundThread — read-only work that can run on a worker thread in parallel
//OnReadWriteMainThread — can modify game state, must run on main thread after background work is done

//In multithreaded mode it runs like a pipeline: kick off background work, and while those worker threads are busy,
//next frame starts its read-only main-thread work. Once all workers finish,
//it flushes any queued state changes and dispatches the next batch of background tasks.
//In single-threaded mode it just runs everything sequentially in order.

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
    private readonly IModRegistry modRegistry;
    private readonly IGame game;
    private readonly IGameService platform;

    public TaskScheduler(IGame game, IGameService platform, IModRegistry modRegistry)
    {
        this.game = game;
        this.platform = platform;
        this.modRegistry = modRegistry;
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
        _actions = new BackgroundAction[modRegistry.Mods.Count];
        for (int i = 0; i < modRegistry.Mods.Count; i++)
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

        for (int i = 0; i < modRegistry.Mods.Count; i++)
            modRegistry.Mods[i].OnReadOnlyBackgroundThread(dt);

        RunReadWriteMainThread(dt);
        FlushCommitActions();
    }

    /// <summary>
    /// Returns <c>true</c> if no background task is still active and unfinished.
    /// </summary>
    private bool AllBackgroundTasksFinished()
    {
        for (int i = 0; i < modRegistry.Mods.Count; i++)
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
        for (int i = 0; i < modRegistry.Mods.Count; i++)
        {
            int captured = i;
            _actions[captured].Active = true;
            _actions[captured].Finished = false;
            platform.QueueUserWorkItem(
                CreateBackgroundAction(captured, dt, () => _actions[captured].Finished = true));
        }
    }

    /// <summary>Calls <c>OnReadOnlyMainThread</c> on every registered client mod.</summary>
    private void RunReadOnlyMainThread(float dt)
    {
        for (int i = 0; i < modRegistry.Mods.Count; i++)
            modRegistry.Mods[i].OnReadOnlyMainThread(dt);
    }

    /// <summary>Calls <c>OnReadWriteMainThread</c> on every registered client mod.</summary>
    private void RunReadWriteMainThread(float dt)
    {
        for (int i = 0; i < modRegistry.Mods.Count; i++)
            modRegistry.Mods[i].OnReadWriteMainThread(dt);
    }

    /// <summary>Executes all pending commit actions then clears the queue.</summary>
    private void FlushCommitActions()
    {
        foreach (var action in game.CommitActions)
            action();
        game.CommitActions.Clear();
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
            modRegistry.Mods[modIndex].OnReadOnlyBackgroundThread(dt);
            onFinished();
        };
    }
}

/// <summary>
/// Tracks the execution state of a single background task slot.
/// </summary>
public class BackgroundAction
{
    /// <summary>
    /// Whether this slot has been dispatched to the thread pool and not yet reset.
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Whether the background work has completed. Set to <c>true</c> by the worker thread.
    /// </summary>
    public volatile bool Finished;
}