public partial class Game
{
    // -------------------------------------------------------------------------
    // Asset lookup
    // -------------------------------------------------------------------------

    private bool HasAsset(string md5, string name)
    {
        for (int i = 0; i < assets.Count; i++)
        {
            Asset a = assets[i];
            // Check both MD5 and name as there might be files with same content.
            if (a.md5 == md5 && a.name == name)
                return true;
        }
        return false;
    }

    internal byte[] GetAssetFile(string p)
    {
        string pLower = p.ToLowerInvariant();
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i].name == pLower)
                return assets[i].data;
        }
        return null;
    }

    internal int GetAssetFileLength(string p)
    {
        string pLower = p.ToLowerInvariant();
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i].name == pLower)
                return assets[i].dataLength;
        }
        return 0;
    }

    // -------------------------------------------------------------------------
    // Asset cache
    // -------------------------------------------------------------------------

    private void CacheAsset(Asset asset)
    {
        // Prevent crash on old servers that don't send a checksum.
        if (asset.md5 == null)
            return;

        if (!StringUtils.IsChecksum(asset.md5))
            return;

        if (!Platform.IsCached(asset.md5))
            Platform.SaveAssetToCache(asset);
    }

    public void SetFile(string name, string md5, byte[] downloaded, int downloadedLength)
    {
        string nameLower = name.ToLowerInvariant();

        // Update mouse cursor if the cursor asset changed.
        if (nameLower == "mousecursor.png")
            Platform.SetWindowCursor(0, 0, 32, 32, downloaded, downloadedLength);

        Asset newAsset = new()
        {
            data = downloaded,
            dataLength = downloadedLength,
            name = nameLower,
            md5 = md5
        };

        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i] == null)
                continue;

            if (assets[i].name == nameLower)
            {
                if (options.UseServerTextures)
                    assets[i] = newAsset;

                CacheAsset(newAsset);
                return;
            }
        }
        assets.Add(newAsset);
        CacheAsset(newAsset);
    }
}