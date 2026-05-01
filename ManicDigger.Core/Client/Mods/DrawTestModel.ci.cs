using System.Text;

public class ModDrawTestModel : ModBase
{
    private readonly IOpenGlService platformOpenGl;
    private readonly IVoxelMap voxelMap;
    private readonly IMeshDrawer meshDrawer;

    public ModDrawTestModel(IOpenGlService platformOpenGl, IVoxelMap voxelMap, IMeshDrawer meshDrawer)
    {
        this.platformOpenGl = platformOpenGl;
        this.voxelMap = voxelMap;
        this.meshDrawer = meshDrawer;
    }

    public override void OnNewFrameDraw3d(IGame game, float deltaTime)
    {
        if (game.GuiState == GuiState.MapLoading)
        {
            return;
        }

        DrawTestModel(game, deltaTime);
    }

    private void DrawTestModel(IGame game, float deltaTime)
    {
        if (!game.EnableDrawTestCharacter)
        {
            return;
        }
        if (testmodel == null)
        {
            testmodel = new AnimatedModelRenderer(meshDrawer, platformOpenGl);
            byte[] data = game.GetAssetFile("player.txt");
            int dataLength = game.GetAssetFileLength("player.txt");
            string dataString = Encoding.UTF8.GetString(data, 0, dataLength);
            AnimatedModel model = AnimatedModelSerializer.Deserialize(dataString);
            testmodel.Start(game, model);
        }
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(voxelMap.MapSizeX / 2, game.Blockheight(voxelMap.MapSizeX / 2, voxelMap.MapSizeY / 2 - 2, 128), voxelMap.MapSizeY / 2 - 2);
        platformOpenGl.BindTexture2d(game.GetTexture("mineplayer.png"));
        testmodel.Render(deltaTime, 0, true, true, 1);
        meshDrawer.GLPopMatrix();
    }
    private AnimatedModelRenderer testmodel;

    public override bool OnClientCommand(IGame game, ClientCommandArgs args)
    {
        if (args.Command == "testmodel")
        {
            game.EnableDrawTestCharacter = game.BoolCommandArgument(args.Arguments);
            return true;
        }
        return false;
    }
}
