using System.Net;
using System.Text;

public class ServerSystemHttpServer : ServerSystem
{
    internal FragLabs.HTTP.HttpServer httpServer;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override void Initialize(Server server)
    {
        if (!server.config.EnableHTTPServer || server.IsSinglePlayer)
            return;

        int httpPort = server.Port + 1;
        try
        {
            httpServer = new FragLabs.HTTP.HttpServer(new IPEndPoint(IPAddress.Any, httpPort));
            httpServer.Install(new MainHttpModule { server = server, system = this });

            foreach (ActiveHttpModule m in server.httpModules)
            {
                httpServer.Install(m.module);
                m.installed = true;
            }

            httpServer.Start();
            Console.WriteLine(server.language.ServerHTTPServerStarted(), httpPort);
        }
        catch
        {
            Console.WriteLine(server.language.ServerHTTPServerError(), httpPort);
        }
    }

    protected override void OnUpdate(Server server, float dt)
    {
        if (httpServer == null) return;

        foreach (ActiveHttpModule m in server.httpModules.Where(m => !m.installed))
        {
            httpServer.Install(m.module);
            m.installed = true;
        }
    }

    public override void OnRestart(Server server)
    {
        foreach (ActiveHttpModule m in server.httpModules.Where(m => m.installed))
            httpServer.Uninstall(m.module);

        server.httpModules.Clear();
    }
}

// -----------------------------------------------------------------------------

public class ActiveHttpModule
{
    public string name;
    public Func<string> description;
    public FragLabs.HTTP.IHttpModule module;
    public bool installed;
}

// -----------------------------------------------------------------------------

internal class MainHttpModule : FragLabs.HTTP.IHttpModule
{
    public Server server;
    public ServerSystemHttpServer system;

    public void Installed(FragLabs.HTTP.HttpServer server) { }
    public void Uninstalled(FragLabs.HTTP.HttpServer server) { }

    public bool ResponsibleForRequest(FragLabs.HTTP.HttpRequest request) =>
        request.Uri.AbsolutePath.Equals("/", StringComparison.CurrentCultureIgnoreCase);

    public bool ProcessAsync(FragLabs.HTTP.ProcessRequestEventArgs args)
    {
        var sb = new StringBuilder("<html>");

        foreach (ActiveHttpModule m in server.httpModules.OrderBy(m => m.name))
            sb.Append($"<a href='{m.name}'>{m.name}</a> - {m.description()}");

        sb.Append("</html>");
        args.Response.Producer = new FragLabs.HTTP.BufferedProducer(sb.ToString());
        return false;
    }
}