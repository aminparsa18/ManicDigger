using System.Text;

public class ModDrawTestModel : ModBase
{
    private readonly IGame game;
    private readonly IOpenGlService platformOpenGl;

    public ModDrawTestModel(IGame game, IOpenGlService platformOpenGl)
    {
        this.game = game;
        this.platformOpenGl = platformOpenGl;
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        if (game.GuiState == GuiState.MapLoading)
        {
            return;
        }

        DrawTestModel(deltaTime);
    }

    private void DrawTestModel(float deltaTime)
    {
        if (!game.EnableDrawTestCharacter)
        {
            return;
        }
        if (testmodel == null)
        {
            testmodel = new AnimatedModelRenderer();
            byte[] data = game.GetAssetFile("player.txt");
            int dataLength = game.GetAssetFileLength("player.txt");
            string dataString = Encoding.UTF8.GetString(data, 0, dataLength);
            AnimatedModel model = AnimatedModelSerializer.Deserialize(dataString);
            testmodel.Start(game, model);
        }
        game.GLPushMatrix();
        game.GLTranslate(game.VoxelMap.MapSizeX / 2, game.Blockheight(game.VoxelMap.MapSizeX / 2, game.VoxelMap.MapSizeY / 2 - 2, 128), game.VoxelMap.MapSizeY / 2 - 2);
        platformOpenGl.BindTexture2d(game.GetTexture("mineplayer.png"));
        testmodel.Render(deltaTime, 0, true, true, 1);
        game.GLPopMatrix();
    }
    private AnimatedModelRenderer testmodel;

    public override bool OnClientCommand(ClientCommandArgs args)
    {
        if (args.command == "testmodel")
        {
            game.EnableDrawTestCharacter = game.BoolCommandArgument(args.arguments);
            return true;
        }
        return false;
    }
}
