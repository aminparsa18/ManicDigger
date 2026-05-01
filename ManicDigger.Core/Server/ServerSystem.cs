public abstract class ServerSystem
{
    protected const int One = 1;
    private bool _initialized;

    public void Update(Server server, float dt)
    {
        if (!_initialized)
        {
            _initialized = true;
            Initialize(server);
        }

        OnUpdate(server, dt);
    }

    protected virtual void Initialize(Server server) { }
    protected virtual void OnUpdate(Server server, float dt) { }
    public virtual void OnRestart(Server server) { }
    public virtual bool OnCommand(Server server, int sourceClientId, string command, string argument) => false;
}