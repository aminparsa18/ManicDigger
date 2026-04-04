using Acornima.Ast;
using Jint;
using ManicDigger.ClientNative;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace ManicDigger
{
    public class ServerSystemModLoader : ServerSystem
    {
        bool started;
        public override void Update(Server server, float dt)
        {
            if (!started)
            {
                started = true;
                LoadMods(server, false);
            }
        }

        public override bool OnCommand(Server server, int sourceClientId, string command, string argument)
        {
            if (command == "mods")
            {
                RestartMods(server, sourceClientId);
                return true;
            }
            return false;
        }

        public bool RestartMods(Server server, int sourceClientId)
        {
            if (!server.PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.restart))
            {
                server.SendMessage(sourceClientId, string.Format(server.language.Get("Server_CommandInsufficientPrivileges"), server.colorError));
                return false;
            }
            server.SendMessageToAll(string.Format(server.language.Get("Server_CommandRestartModsSuccess"), server.colorImportant, server.GetClient(sourceClientId).ColoredPlayername(server.colorImportant)));
            server.ServerEventLog(string.Format("{0} restarts mods.", server.GetClient(sourceClientId).playername));

            server.modEventHandlers = new ModEventHandlers();
            for (int i = 0; i < server.systemsCount; i++)
            {
                if (server.systems[i] == null) { continue; }
                server.systems[i].OnRestart(server);
            }

            LoadMods(server, true);
            return true;
        }

        void LoadMods(Server server, bool restart)
        {
            server.modManager = new ModManager1();
            var m = server.modManager;
            m.Start(server);
            var scripts = GetScriptSources(server);
            Console.WriteLine($"[ModLoader] GetScriptSources returned {scripts.Count} scripts:");
            foreach (var k in scripts)
                Console.WriteLine($"  '{k.Key}' ({k.Value.Length} chars) - is .js: {k.Key.EndsWith(".js")}");
            CompileScripts(scripts, restart);
            Console.WriteLine($"[ModLoader] After CompileScripts, mods.Count = {mods.Count}");
            Start(m, m.required);
        }

        Dictionary<string, string> GetScriptSources(Server server)
        {
            string assemblyDir = Path.GetDirectoryName(GetType().Assembly.Location)!;

            string[] modpaths = new[] { Path.Combine(assemblyDir, "..", "..", "..", "..", "ManicDiggerLib", "Server", "Mods"),
    Path.Combine(assemblyDir, "Mods") };

            var cd = Directory.GetCurrentDirectory();
            var loc = GetType().Assembly.Location;
            Console.WriteLine($"[ModLoader] Working directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"[ModLoader] Assembly location: {GetType().Assembly.Location}");

            foreach (string modpath in modpaths)
            {
                Console.WriteLine($"[ModLoader] Checking modpath: {Path.GetFullPath(modpath)} - exists: {Directory.Exists(modpath)}");
            }

            for (int i = 0; i < modpaths.Length; i++)
            {
                if (File.Exists(Path.Combine(modpaths[i], "current.txt")))
                {
                    server.gameMode = File.ReadAllText(Path.Combine(modpaths[i], "current.txt")).Trim();
                }
                else if (Directory.Exists(modpaths[i]))
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(modpaths[i], "current.txt"), server.gameMode);
                    }
                    catch
                    {
                    }
                }
                modpaths[i] = Path.Combine(modpaths[i], server.gameMode);
            }
            Dictionary<string, string> scripts = new Dictionary<string, string>();
            foreach (string modpath in modpaths)
            {
                if (!Directory.Exists(modpath))
                {
                    continue;
                }
                server.ModPaths.Add(modpath);
                string[] files = Directory.GetFiles(modpath);
                foreach (string s in files)
                {
                    if (!GameStorePath.IsValidName(Path.GetFileNameWithoutExtension(s)))
                    {
                        continue;
                    }
                    if (!(Path.GetExtension(s).Equals(".cs", StringComparison.InvariantCultureIgnoreCase)
                        || Path.GetExtension(s).Equals(".js", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue;
                    }
                    string scripttext = File.ReadAllText(s);
                    string filename = new FileInfo(s).Name;
                    scripts[filename] = scripttext;
                }
            }
            return scripts;
        }

        Engine jintEngine = new(options =>
        {
            options.AllowClr();
        });
        Dictionary<string, string> javascriptScripts = new Dictionary<string, string>();
        public void CompileScripts(Dictionary<string, string> scripts, bool restart)
        {
            Dictionary<string, string> csharpScripts = new();

            foreach (var k in scripts)
            {
                if (k.Key.EndsWith(".js"))
                    javascriptScripts[k.Key] = k.Value;
                else
                    csharpScripts[k.Key] = k.Value;
            }

            if (restart)
                return; // javascript only

            // Build default metadata references from currently loaded assemblies
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            // Also add explicit assemblies your mods depend on
            foreach (var asmName in new[] { "ScriptingApi.dll", "LibNoise.dll", "protobuf-net.dll" })
            {
                string assemblyDir = Path.GetDirectoryName(GetType().Assembly.Location)!;
                // First try next to the executing assembly
                string localPath = Path.Combine(assemblyDir, asmName);
                if (File.Exists(localPath))
                {
                    references.Add(MetadataReference.CreateFromFile(localPath));
                    Console.WriteLine($"[ModLoader] Added reference: {localPath}");
                    continue;
                }

                // Then try already-loaded assemblies in AppDomain
                string nameWithoutExt = Path.GetFileNameWithoutExtension(asmName);
                var loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == nameWithoutExt);
                if (loaded != null && !string.IsNullOrEmpty(loaded.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(loaded.Location));
                    Console.WriteLine($"[ModLoader] Added reference from AppDomain: {loaded.Location}");
                    continue;
                }

                Console.WriteLine($"[ModLoader] WARNING: Could not find reference: {asmName}");
            }

            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

            // --- Try compiling all scripts together first ---
            bool allSucceeded = TryCompileScripts(
                csharpScripts,
                references,
                parseOptions,
                allowUnsafe: true,
                assemblyName: "ManicDiggerMods",
                out Assembly? allAssembly,
                out IEnumerable<Diagnostic> allDiagnostics);

            if (allSucceeded && allAssembly != null)
            {
                Use(allAssembly);
                return;
            }

            Console.WriteLine("[ModLoader] Combined compilation failed, falling back to per-script compilation.");

            // --- Fall back: compile each script individually ---
            foreach (var k in csharpScripts)
            {
                bool ok = TryCompileScripts(
                    new Dictionary<string, string> { { k.Key, k.Value } },
                    references,
                    parseOptions,
                    allowUnsafe: true,
                    assemblyName: "Mod_" + Path.GetFileNameWithoutExtension(k.Key),
                    out Assembly? modAssembly,
                    out IEnumerable<Diagnostic> diagnostics);

                if (!ok)
                {
                    var errors = string.Join("\n", diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => $"{d.Id} Line:{d.Location.GetLineSpan().StartLinePosition.Line + 1} {d.GetMessage()}"));

                    string errormsg = $"Can't load mod: {k.Key}\n{errors}";
                    Console.WriteLine(errormsg);

                    try { System.Windows.Forms.MessageBox.Show(errormsg); } catch { }
                    continue;
                }

                if (modAssembly != null)
                    Use(modAssembly);
            }
        }


        bool TryCompileScripts(
    Dictionary<string, string> scripts,
    List<MetadataReference> references,
    CSharpParseOptions parseOptions,
    bool allowUnsafe,
    string assemblyName,
    out Assembly? assembly,
    out IEnumerable<Diagnostic> diagnostics)
        {
            Console.WriteLine($"[Roslyn] Script count: {scripts.Count}");
            foreach (var k in scripts)
            {
                Console.WriteLine($"[Roslyn] Script '{k.Key}': {k.Value.Length} chars");
                Console.WriteLine($"[Roslyn] First 200 chars: {k.Value.Substring(0, Math.Min(200, k.Value.Length))}");
            }

            var syntaxTrees = scripts
                .Select(k => CSharpSyntaxTree.ParseText(k.Value, parseOptions, path: k.Key, Encoding.UTF8))
                .ToList();
            Console.WriteLine($"[Roslyn] Syntax trees: {syntaxTrees.Count}");
            foreach (var tree in syntaxTrees)
            {
                var root = tree.GetRoot();
                Console.WriteLine($"[Roslyn] Tree '{tree.FilePath}': {root.DescendantNodes().Count()} nodes");
            }

            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: allowUnsafe,
                optimizationLevel: OptimizationLevel.Release);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                references,
                compilationOptions);

            var ms = new MemoryStream();

#if DEBUG
            var pdbStream = new MemoryStream();
            var emitOptions = new Microsoft.CodeAnalysis.Emit.EmitOptions(
                debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb);
            var result = compilation.Emit(ms, pdbStream, options: emitOptions);
#else
    var result = compilation.Emit(ms);
#endif

            // ADD THIS:
            foreach (var diag in result.Diagnostics)
            {
                Console.WriteLine($"[Roslyn] {diag.Severity} {diag.Id}: {diag.GetMessage()} (line {diag.Location.GetLineSpan().StartLinePosition.Line + 1})");
            }
            Console.WriteLine($"[Roslyn] Emit success: {result.Success}, stream length: {ms.Length}");

            diagnostics = result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning);

            if (!result.Success)
            {
                assembly = null;
                return false;
            }

            var loadContext = new AssemblyLoadContext(name: "ModLoader", isCollectible: true);
            ms.Seek(0, SeekOrigin.Begin);

#if DEBUG
            pdbStream.Seek(0, SeekOrigin.Begin);
            assembly = loadContext.LoadFromStream(ms, pdbStream);
            pdbStream.Dispose();
#else
    assembly = loadContext.LoadFromStream(ms);
#endif
            ms.Dispose();
            // ADD THIS:
            var tt = assembly.GetTypes();
            Console.WriteLine($"[Roslyn] Assembly name: {assembly.FullName}");
            Console.WriteLine($"[Roslyn] Types: {assembly.GetTypes().Length}");

            return true;
        }

        void Use(Assembly assembly)
        {
            var types = assembly.GetTypes().ToList();
            foreach (Type t in assembly.GetTypes())
            {
                if (typeof(IMod).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                {
                    mods[t.Name] = (IMod)Activator.CreateInstance(t)!;
                    Console.WriteLine("Loaded mod: {0}", t.Name);
                }
            }
        }

        Dictionary<string, IMod> mods = new Dictionary<string, IMod>();
        Dictionary<string, string[]> modRequirements = new Dictionary<string, string[]>();
        Dictionary<string, bool> loaded = new Dictionary<string, bool>();

        public void Start(IModManager m, List<string> currentRequires)
        {
            /*
            foreach (var mod in mods)
            {
                mod.Start(m);
            }
            */

            modRequirements.Clear();
            loaded.Clear();

            foreach (var k in mods)
            {
                k.Value.PreStart(m);
                modRequirements[k.Key] = currentRequires.ToArray();
                currentRequires.Clear();
            }
            foreach (var k in mods)
            {
                StartMod(k.Key, k.Value, m);
            }

            StartJsMods(m);
        }

        void StartJsMods(IModManager m)
        {
            jintEngine.SetValue("m", m);
            // todo: javascript mod requirements
            foreach (var k in javascriptScripts)
            {
                try
                {
                    jintEngine.Execute(k.Value);
                    Console.WriteLine("Loaded mod: {0}", k.Key);
                }
                catch
                {
                    Console.WriteLine("Error in mod: {0}", k.Key);
                }
            }
        }

        void StartMod(string name, IMod mod, IModManager m)
        {
            if (loaded.ContainsKey(name))
            {
                return;
            }
            if (modRequirements.TryGetValue(name, out string[]? value))
            {
                foreach (string required_name in value)
                {
                    if (!mods.ContainsKey(required_name))
                    {
                        try
                        {
                            System.Windows.Forms.MessageBox.Show(string.Format("Can't load mod {0} because its dependency {1} couldn't be loaded.", name, required_name));
                        }
                        catch
                        {
                            //This will be the case if the server is running on a headless linux server without X11 installed (previously crashed)
                            Console.WriteLine(string.Format("[Mod error] Can't load mod {0} because its dependency {1} couldn't be loaded.", name, required_name));
                        }
                    }
                    StartMod(required_name, mods[required_name], m);
                }
            }
            else
            {
                var ss = name;
            }
            mod.Start(m);
            loaded[name] = true;
        }
    }
}
