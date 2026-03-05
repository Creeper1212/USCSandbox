using AssetsTools.NET;
using AssetsTools.NET.Extra;
using USCSandbox.Processor;
using UnityVersion = AssetRipper.Primitives.UnityVersion;

namespace USCSandbox
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string exeDir = AppContext.BaseDirectory;
            string logsRoot = Path.Combine(exeDir, "logs");
            string shaderLogsRoot = Path.Combine(logsRoot, "shaders");
            string classDataPath = Path.Combine(exeDir, "classdata.tpk");
            Logger.Initialize(logsRoot);
            Logger.Info($"USCSandbox starting in {Environment.CurrentDirectory}");
            Logger.Info($"Executable directory: {exeDir}");
            Logger.Info($"Class data path: {classDataPath}");
            Logger.Info($"Arguments: {string.Join(" ", args)}");

            try
            {
            if (!File.Exists(classDataPath))
            {
                string error = $"classdata.tpk was not found next to the executable. Expected: {classDataPath}";
                Logger.Error(error);
                Console.WriteLine(error);
                return;
            }

            if (args.Length < 1)
            {
                Console.WriteLine("USCS [bundle path] [assets path] [shader path id] <--platform> <--version> <--all>");
                Console.WriteLine("  [bundle path (or \"null\" for no bundle)]");
                Console.WriteLine("  [assets path (or file name in bundle)]");
                Console.WriteLine("  [shader path id (or --all to load all shaders)]");
                Console.WriteLine("  --platform <[d3d11, Switch] (or skip this arg for d3d11)>");
                Console.WriteLine("  --version <unity version override>");
                return;
            }

            var manager = new AssetsManager();
            AssetsFileInstance afileInst;

            GPUPlatform platform = GPUPlatform.d3d11;
            UnityVersion? ver = null;
            bool allSet = false;

            List<string> argList = [];
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("--"))
                {
                    switch (arg)
                    {
                        case "--platform":
                            platform = Enum.Parse<GPUPlatform>(args[++i]);
                            Logger.Info($"Optional argument --platform={platform}");
                            break;
                        case "--version":
                            ver = UnityVersion.Parse(args[++i]);
                            Logger.Info($"Optional argument --version={ver}");
                            break;
                        case "--all":
                            allSet = true;
                            Logger.Info("Optional argument --all=true");
                            break;
                        default:
                            Console.WriteLine($"Optional argmuent {arg} is invalid.");
                            Logger.Warning($"Invalid optional argument: {arg}");
                            return;
                    }
                }
                else
                {
                    argList.Add(arg);
                }
            }

            var bundlePath = argList[0];
            Logger.Info($"Bundle path: {bundlePath}");
            if (argList.Count == 1)
            {
                var bundleFile = manager.LoadBundleFile(bundlePath, true);
                var dirInfs = bundleFile.file.BlockAndDirInfo.DirectoryInfos;
                Logger.Info($"Bundle file loaded. File entries: {dirInfs.Count}");
                Console.WriteLine("Available files in bundle:");
                foreach (var dirInf in dirInfs)
                {
                    if ((dirInf.Flags & 4) == 0)
                        continue;

                    Console.WriteLine($"  {dirInf.Name}");
                }
                return;
            }

            var assetsFileName = argList[1];
            if (argList.Count == 2 && !allSet)
            {
                if (bundlePath != "null")
                {
                    var bundleFile = manager.LoadBundleFile(bundlePath, true);
                    afileInst = manager.LoadAssetsFileFromBundle(bundleFile, assetsFileName);
                    Logger.Info($"Assets file loaded from bundle: {assetsFileName}");

                    manager.LoadClassPackage(classDataPath);
                    manager.LoadClassDatabaseFromPackage(bundleFile.file.Header.EngineVersion);
                    Logger.Info($"Loaded class database from bundle version: {bundleFile.file.Header.EngineVersion}");

                    Console.WriteLine("Available shaders in bundle:");
                }
                else
                {
                    afileInst = manager.LoadAssetsFile(assetsFileName);
                    Logger.Info($"Assets file loaded: {assetsFileName}");

                    manager.LoadClassPackage(classDataPath);
                    manager.LoadClassDatabaseFromPackage(afileInst.file.Metadata.UnityVersion);
                    Logger.Info($"Loaded class database from assets version: {afileInst.file.Metadata.UnityVersion}");

                    Console.WriteLine("Available shaders in assets file:");
                }

                foreach (var shaderInf in afileInst.file.GetAssetsOfType(AssetClassID.Shader))
                {
                    var tmpShaderBf = manager.GetBaseField(afileInst, shaderInf);
                    var tmpShaderName = tmpShaderBf["m_ParsedForm"]["m_Name"].AsString;
                    Console.WriteLine($"  {tmpShaderName} (path id {shaderInf.PathId})");
                }
                return;
            }

            long shaderPathId = 0;
            if (argList.Count > 2)
                shaderPathId = long.Parse(argList[2]);
            Logger.Info($"Shader path id filter: {shaderPathId}");

            if (bundlePath != "null")
            {
                var bundleFile = manager.LoadBundleFile(bundlePath, true);
                afileInst = manager.LoadAssetsFileFromBundle(bundleFile, assetsFileName);
                Logger.Info($"Loaded assets from bundle for decompilation: {assetsFileName}");

                if (ver is null)
                {
                    var verStr = bundleFile.file.Header.EngineVersion;
                    if (verStr != "0.0.0")
                    {
                        var fixedVerStr = new AssetsTools.NET.Extra.UnityVersion(verStr).ToString();
                        ver = UnityVersion.Parse(fixedVerStr);
                        Logger.Info($"Auto-detected unity version from bundle: {ver}");
                    }
                }
            }
            else
            {
                afileInst = manager.LoadAssetsFile(assetsFileName);
                Logger.Info($"Loaded assets file for decompilation: {assetsFileName}");

                if (ver is null)
                {
                    var verStr = afileInst.file.Metadata.UnityVersion;
                    if (verStr != "0.0.0")
                    {
                        var fixedVerStr = new AssetsTools.NET.Extra.UnityVersion(verStr).ToString();
                        ver = UnityVersion.Parse(fixedVerStr);
                        Logger.Info($"Auto-detected unity version from assets: {ver}");
                    }
                }
            }

            if (ver is null)
            {
                Console.WriteLine("File version was stripped. Please set --version flag.");
                return;
            }

            manager.LoadClassPackage(classDataPath);
            manager.LoadClassDatabaseFromPackage(ver.ToString());
            Logger.Info($"Class database ready for version {ver}");

            var shadersToLoad = new List<AssetFileInfo>();
            if (shaderPathId != 0)
                shadersToLoad.Add(afileInst.file.GetAssetInfo(shaderPathId));
            else
                shadersToLoad.AddRange(afileInst.file.GetAssetsOfType(AssetClassID.Shader));
            Logger.Info($"Shaders queued: {shadersToLoad.Count}");

            foreach (var shaderInf in shadersToLoad)
            {
                var shaderBf = manager.GetBaseField(afileInst, shaderInf);
                if (shaderBf == null)
                {
                    Console.WriteLine("Shader asset not found or couldn't be read.");
                    Logger.Warning($"Shader with path id {shaderInf.PathId} could not be read.");
                    return;
                }

                var shaderName = shaderBf["m_ParsedForm"]["m_Name"].AsString;
                Logger.StartShaderLog(shaderLogsRoot, shaderName, shaderInf.PathId);
                try
                {
                    Logger.Info($"Starting shader decompile: {shaderName} (path id {shaderInf.PathId})");
                    var shaderProcessor = new ShaderProcessor(shaderBf, ver.Value, platform);
                    string shaderText = shaderProcessor.Process();

                    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "out", Path.GetDirectoryName(shaderName)!));
                    string outputPath = $"{Path.Combine(Environment.CurrentDirectory, "out", shaderName)}.shader";
                    File.WriteAllText(outputPath, shaderText);
                    Logger.Info($"Shader written to {outputPath}");
                    Console.WriteLine($"{shaderName} decompiled");
                }
                catch (Exception ex)
                {
                    Logger.Exception($"Failed to decompile shader: {shaderName} (path id {shaderInf.PathId})", ex);
                }
                finally
                {
                    Logger.EndShaderLog();
                }
            }
            }
            catch (Exception ex)
            {
                Logger.Exception("Fatal error in Program.Main", ex);
                throw;
            }
            finally
            {
                Logger.Info("USCSandbox shutdown.");
                Logger.Shutdown();
            }
        }
    }
}
