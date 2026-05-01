using OpenTK.Mathematics;
using System.Text;

/// <summary>
/// Renders animated player/entity models in the 3D world each frame.
/// </summary>
public class ModDrawPlayers : ModBase
{
    private readonly IGameService platform;
    private readonly IVoxelMap voxelMap;
    private readonly IFrustumCulling frustumCulling;
    private readonly IMeshDrawer meshDrawer;
    private readonly IOpenGlService openGlService;

    public ModDrawPlayers(IGameService platform, IVoxelMap voxelMap, IFrustumCulling frustumCulling,
        IMeshDrawer meshDrawer, IOpenGlService openGlService, IGame game) : base(game)
    {
        this.platform = platform;
        this.voxelMap = voxelMap;
        this.frustumCulling = frustumCulling;
        this.meshDrawer = meshDrawer;
        this.openGlService = openGlService;
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        Game.TotalTimeMilliseconds = platform.TimeMillisecondsFromStart;

        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity p = Game.Entities[i];
            if (p?.drawModel == null) continue;
            if (i == Game.LocalPlayerId && !Game.EnableTppView) continue;
            if (p.networkPosition != null && !p.networkPosition.PositionLoaded) continue;
            if (!frustumCulling.SphereInFrustum(p.position.x, p.position.y, p.position.z, 3)) continue;
            if (p.drawModel.CurrentTexture == -1) continue;

            int cx = (int)p.position.x / GameConstants.CHUNK_SIZE;
            int cy = (int)p.position.z / GameConstants.CHUNK_SIZE;
            int cz = (int)p.position.y / GameConstants.CHUNK_SIZE;
            if (voxelMap.IsValidChunkPos(cx, cy, cz) && !voxelMap.IsChunkRendered(cx, cy, cz)) continue;

            p.playerDrawInfo ??= new PlayerDrawInfo();

            float shadow = (float)Game.GetLight((int)p.position.x, (int)p.position.z, (int)p.position.y) / GameConstants.maxlight;
            float speed = i == Game.LocalPlayerId ? GetLocalPlayerSpeed(Game) : GetNetworkPlayerSpeed(p, deltaTime);

            EnsureRenderer(p);
            DrawEntity(p, deltaTime, shadow, speed);
        }
    }

    /// <summary>Calculates movement speed for the local player based on physics velocity.</summary>
    private float GetLocalPlayerSpeed(IGame game)
    {
        game.Player.playerDrawInfo ??= new PlayerDrawInfo();

        float speed = new Vector3(
            game.playervelocity.X / 60f,
            game.playervelocity.Y / 60f,
            game.playervelocity.Z / 60f).Length * 1.5f;

        game.Player.playerDrawInfo.moves = speed != 0;
        return speed;
    }

    /// <summary>Calculates movement speed for a network entity based on interpolated velocity.</summary>
    private static float GetNetworkPlayerSpeed(Entity p, float dt)
    {
        return p.playerDrawInfo.Velocity.Length / dt * 0.04f;
    }

    /// <summary>Loads and initializes the animated model renderer for an entity if not already done.</summary>
    private void EnsureRenderer(Entity p)
    {
        if (p.drawModel.renderer != null) return;

        p.drawModel.renderer = new AnimatedModelRenderer(meshDrawer, openGlService);
        byte[] data = Game.GetAssetFile(p.drawModel.Model_);
        int dataLength = Game.GetAssetFileLength(p.drawModel.Model_);
        if (data == null) return;

        string dataString = Encoding.UTF8.GetString(data, 0, dataLength);
        AnimatedModel model = AnimatedModelSerializer.Deserialize(dataString);
        p.drawModel.renderer.Start(Game, model);
    }

    /// <summary>Renders the entity's animated model at its current world position and orientation.</summary>
    private void DrawEntity(Entity p, float dt, float shadow, float speed)
    {
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(p.position.x, p.position.y, p.position.z);
        meshDrawer.GLRotate(float.RadiansToDegrees(-p.position.roty + MathF.PI), 0, 1, 0);
        openGlService.BindTexture2d(p.drawModel.CurrentTexture);
        p.drawModel.renderer.Render(dt, float.RadiansToDegrees(p.position.rotx + MathF.PI), true, p.playerDrawInfo.moves, shadow);
        meshDrawer.GLPopMatrix();
    }
}