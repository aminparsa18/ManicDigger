namespace ManicDigger;

/// <summary>
/// The last <see cref="ServerSystem"/> to run on startup. Use <see cref="Initialize"/>
/// to register anything that must execute after all other systems have initialized.
/// </summary>
public class ServerSystemLoadLast : ServerSystem
{
    /// <inheritdoc/>
    protected override void Initialize(Server server)
    {
        CallModOnLoad(server);
    }

    /// <summary>
    /// Fires all mod load callbacks in registration order.
    /// <para>
    /// <see cref="Server.OnLoad"/> handlers are called directly — errors there are
    /// considered fatal. <c>onloadworld</c> handlers are wrapped individually so a
    /// single bad mod cannot abort the rest of the load sequence.
    /// </para>
    /// </summary>
    private static void CallModOnLoad(Server server)
    {
        // Ensure mod data storage exists even if nothing was loaded from a savegame
        server.ModData ??= [];

        foreach (var handler in server.OnLoad)
            handler();

        foreach (var handler in server.ModEventHandlers.onloadworld)
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Mod exception: OnLoadWorld");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}