using System.Security.Cryptography;

/// <summary>
/// Scans one or more directories and loads all files as <see cref="Asset"/> instances.
/// Each asset is fingerprinted with an MD5 hash for server-side deduplication and caching.
/// </summary>
public class ServerAssetManager : IAssetManager
{
    private readonly MD5 _md5 = MD5.Create();
    private readonly string _assetsRoot;

    public List<Asset> Assets { get; set; } = [];
    public float AssetsLoadProgress { get; set; }

    public ServerAssetManager(IWebHostEnvironment env)
    {
        _assetsRoot = Path.Combine(env.ContentRootPath, "wwwroot");
    }

    public void LoadAssets()
    {
        if (Assets.Count > 0) return;

        foreach (string filename in ReadManifest())
        {
            string fullPath = Path.Combine(_assetsRoot, filename);
            byte[] data = File.ReadAllBytes(fullPath);

            Assets.Add(new Asset
            {
                data = data,
                dataLength = data.Length,
                name = Path.GetFileName(filename).ToLowerInvariant(),
                md5 = ComputeMd5(data),
            });
        }

        AssetsLoadProgress = 1;
    }

    private IEnumerable<string> ReadManifest()
    {
        string manifestPath = Path.Combine(_assetsRoot, "assets_manifest.txt");
        return File.ReadAllText(manifestPath)
                   .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                   .Select(line => line.Replace('\\', '/'));
    }

    private string ComputeMd5(byte[] data)
        => Convert.ToHexString(_md5.ComputeHash(data)).ToLowerInvariant();
}