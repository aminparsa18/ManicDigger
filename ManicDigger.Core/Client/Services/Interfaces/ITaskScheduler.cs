//This class runs all the game's client mods every frame in the right order, with optional multithreading.
//Each mod has three hooks:

using System.Collections.Concurrent;

public interface ITaskScheduler
{
    ConcurrentQueue<Action> CommitActions { get; set; }

    bool Dequeue(out Action? action);
    void Enqueue(Action action);
    void Initialise();
    void Update(float dt);
}