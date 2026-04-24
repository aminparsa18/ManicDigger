using System.Text;

public class ModDrawTestModel : ModBase
{
    private readonly IGameClient _game;

    public ModDrawTestModel(IGameClient game)
    {
        _game = game;
    }

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        if (game.GuiState == GuiState.MapLoading)
        {
            return;
        }

        DrawTestModel(game, deltaTime);
    }

    private void DrawTestModel(Game game, float deltaTime)
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
        game.Platform.BindTexture2d(game.GetTexture("mineplayer.png"));
        testmodel.Render(deltaTime, 0, true, true, 1);
        game.GLPopMatrix();
    }
    private AnimatedModelRenderer testmodel;

    public override bool OnClientCommand(ClientCommandArgs args)
    {
        if (args.command == "testmodel")
        {
            _game.EnableDrawTestCharacter = _game.BoolCommandArgument(args.arguments);
            return true;
        }
        return false;
    }
}
