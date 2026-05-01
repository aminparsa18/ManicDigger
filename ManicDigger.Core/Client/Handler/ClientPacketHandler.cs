/// <summary>
/// Base class for all server-packet handlers.
/// Implementations receive a fully-deserialized <see cref="Packet_Server"/> and
/// are responsible for applying its effect to the game state.
/// </summary>
public abstract class ClientPacketHandler
{
    protected readonly IGame game;
    protected readonly IGameService gameService;

    public ClientPacketHandler(IGameService gameService, IGame game)
    {
        this.gameService = gameService;
        this.game = game;
    }

    /// <summary>Applies the effect of <paramref name="packet"/> to <paramref name="game"/>.</summary>
    public abstract void Handle(Packet_Server packet);
}