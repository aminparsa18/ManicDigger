using System.Text;

/// <summary>
/// Resolves and uploads the skin texture for every visible player entity.
/// In multiplayer the skin is first attempted as a download from the skin server;
/// if that fails or is unavailable the entity falls back to the bundled
/// <c>mineplayer.png</c>, or to a custom texture path stored on the entity itself.
/// </summary>
public class ModLoadPlayerTextures : ModBase
{
    /// <summary><see langword="true"/> after the first non-loading frame has run.</summary>
    private bool _started;

    /// <summary>
    /// Base URL of the skin server, populated once <see cref="_skinServerResponse"/>
    /// completes. <see langword="null"/> when not yet resolved or on error.
    /// </summary>
    internal string skinserver;

    /// <summary>Async HTTP response for the skin-server URL list.</summary>
    internal HttpResponse _skinServerResponse;

    public ModLoadPlayerTextures(IGame game) : base(game)
    {
    }

    /// <inheritdoc/>
    public override void OnNewFrame(float args)
    {
        if (Game.GuiState == GuiState.MapLoading) { return; }

        if (!_started)
        {
            _started = true;
        }

        LoadPlayerTextures();
    }

    /// <summary>
    /// Iterates all entities and ensures each has a resolved <c>CurrentTexture</c>.
    /// In multiplayer, waits for the skin-server URL before proceeding.
    /// For each entity the resolution order is:
    /// <list type="number">
    ///   <item><description>Downloaded skin from the skin server.</description></item>
    ///   <item><description>Custom texture file path stored on the entity.</description></item>
    ///   <item><description>Fallback to <c>mineplayer.png</c>.</description></item>
    /// </list>
    /// </summary>
    internal void LoadPlayerTextures()
    {
        if (!Game.IsSinglePlayer)
        {
            if (_skinServerResponse.Done)
            {
                skinserver = Encoding.UTF8.GetString(
                    _skinServerResponse.Value, 0, _skinServerResponse.Value.Length);
            }
            else if (_skinServerResponse.Error)
            {
                skinserver = null;
            }
            else
            {
                // Still waiting for the skin-server response.
                return;
            }
        }

        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity e = Game.Entities[i];
            if (e?.drawModel == null) { continue; }
            if (e.drawModel.CurrentTexture != -1) { continue; }

            if (TryLoadDownloadedSkin(e)) { continue; }
            if (TryLoadFileSkin(e)) { continue; }
        }
    }

    /// <summary>
    /// Attempts to resolve the entity's skin via the skin server.
    /// Initiates a download on first call, then polls on subsequent calls.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the caller should move on to the next entity
    /// (either because a download is in progress, succeeded, or failed in a way
    /// that means the server path is exhausted).
    /// <see langword="false"/> when this path is not applicable and the caller
    /// should try the file-skin fallback.
    /// </returns>
    private bool TryLoadDownloadedSkin(Entity e)
    {
        if (Game.IsSinglePlayer
         || !e.drawModel.DownloadSkin
         || skinserver == null
         || e.drawModel.Texture_ != null)
        {
            return false;
        }

        // Initiate the download on first visit.
        if (e.drawModel.SkinDownloadResponse == null)
        {
            e.drawModel.SkinDownloadResponse = new HttpResponse();
            string url = string.Concat(skinserver, e.drawName.Name[2..], ".png");
            return true; // still downloading
        }

        if (e.drawModel.SkinDownloadResponse.Error) { return false; }
        if (!e.drawModel.SkinDownloadResponse.Done) { return true; }

        // Download finished — decode and upload.
        Bitmap bmp = PixelBuffer.BitmapFromPng(
            e.drawModel.SkinDownloadResponse.Value,
            e.drawModel.SkinDownloadResponse.Value.Length);

        if (bmp != null)
        {
            e.drawModel.CurrentTexture = Game.GetTextureOrLoad(e.drawName.Name, bmp);
            bmp.Dispose();
        }

        return true;
    }

    /// <summary>
    /// Resolves the entity's texture from a file path stored on the entity, or
    /// falls back to <c>mineplayer.png</c> when no path is set.
    /// Always sets <c>CurrentTexture</c> and returns <see langword="true"/>.
    /// </summary>
    private bool TryLoadFileSkin(Entity e)
    {
        if (e.drawModel.Texture_ == null)
        {
            e.drawModel.CurrentTexture = Game.GetTexture("mineplayer.png");
            return true;
        }

        byte[] file = Game.GetAssetFile(e.drawModel.Texture_);
        if (file == null)
        {
            e.drawModel.CurrentTexture = 0;
            return true;
        }

        Bitmap bmp = PixelBuffer.BitmapFromPng(file, file.Length);
        if (bmp == null)
        {
            e.drawModel.CurrentTexture = 0;
            return true;
        }

        e.drawModel.CurrentTexture = Game.GetTextureOrLoad(e.drawModel.Texture_, bmp);
        bmp.Dispose();
        return true;
    }
}