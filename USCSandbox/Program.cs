using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Diagnostics;
using USCSandbox.Processor;
using UnityVersion = AssetRipper.Primitives.UnityVersion;

namespace USCSandbox
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ConsoleUi.Initialize();
            ConsoleUi.Banner();

            string exeDir = AppContext.BaseDirectory;
            string logsRoot = Path.Combine(exeDir, "logs");
            string shaderLogsRoot = Path.Combine(logsRoot, "shaders");
            string classDataPath = Path.Combine(exeDir, "classdata.tpk");

            Stopwatch stopwatch = Stopwatch.StartNew();
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
                    ConsoleUi.Error(error);
                    return;
                }

                if (args.Length < 1)
                {
                    ConsoleUi.Usage();
                    return;
                }

                var manager = new AssetsManager();
                AssetsFileInstance afileInst;

                GPUPlatform platform = GPUPlatform.d3d11;
                UnityVersion? ver = null;
                bool allSet = false;
                string? scanPath = null;

                List<string> argList = new();
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
                            case "--scan":
                                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                                {
                                    ConsoleUi.Warning("--scan requires a folder path.");
                                    return;
                                }
                                scanPath = args[++i];
                                Logger.Info($"Optional argument --scan={scanPath}");
                                break;
                            default:
                                ConsoleUi.Warning($"Optional argument {arg} is invalid.");
                                Logger.Warning($"Invalid optional argument: {arg}");
                                return;
                        }
                    }
                    else
                    {
                        argList.Add(arg);
                    }
                }

                if (scanPath is not null)
                {
                    RunAssetFileScan(scanPath);
                    return;
                }

                if (argList.Count == 1 && Directory.Exists(argList[0]))
                {
                    RunAssetFileScan(argList[0]);
                    return;
                }

                if (argList.Count == 0)
                {
                    ConsoleUi.Usage();
                    return;
                }

                var bundlePath = argList[0];
                Logger.Info($"Bundle path: {bundlePath}");
                ConsoleUi.Info($"Bundle: {bundlePath}");
                ConsoleUi.Info($"Platform: {platform}");
                if (ver is not null)
                {
                    ConsoleUi.Info($"Unity Version Override: {ver}");
                }

                if (argList.Count == 1)
                {
                    var bundleFile = manager.LoadBundleFile(bundlePath, true);
                    var dirInfs = bundleFile.file.BlockAndDirInfo.DirectoryInfos;
                    Logger.Info($"Bundle file loaded. File entries: {dirInfs.Count}");

                    ConsoleUi.Section("Bundle Files");
                    foreach (var dirInf in dirInfs)
                    {
                        if ((dirInf.Flags & 4) == 0)
                        {
                            continue;
                        }
                        ConsoleUi.ListItem(dirInf.Name);
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

                        ConsoleUi.Section($"Shaders In Bundle Asset: {assetsFileName}");
                    }
                    else
                    {
                        afileInst = manager.LoadAssetsFile(assetsFileName);
                        Logger.Info($"Assets file loaded: {assetsFileName}");

                        manager.LoadClassPackage(classDataPath);
                        manager.LoadClassDatabaseFromPackage(afileInst.file.Metadata.UnityVersion);
                        Logger.Info($"Loaded class database from assets version: {afileInst.file.Metadata.UnityVersion}");

                        ConsoleUi.Section($"Shaders In Assets File: {assetsFileName}");
                    }

                    foreach (var shaderInf in afileInst.file.GetAssetsOfType(AssetClassID.Shader))
                    {
                        var tmpShaderBf = manager.GetBaseField(afileInst, shaderInf);
                        var tmpShaderName = tmpShaderBf["m_ParsedForm"]["m_Name"].AsString;
                        ConsoleUi.ListItem($"{tmpShaderName} (path id {shaderInf.PathId})");
                    }
                    return;
                }

                long shaderPathId = 0;
                if (argList.Count > 2)
                {
                    shaderPathId = long.Parse(argList[2]);
                }
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
                            ConsoleUi.Info($"Detected Unity Version: {ver}");
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
                            ConsoleUi.Info($"Detected Unity Version: {ver}");
                        }
                    }
                }

                if (ver is null)
                {
                    ConsoleUi.Warning("File version was stripped. Please pass --version.");
                    return;
                }

                manager.LoadClassPackage(classDataPath);
                manager.LoadClassDatabaseFromPackage(ver.ToString());
                Logger.Info($"Class database ready for version {ver}");

                var shadersToLoad = new List<AssetFileInfo>();
                if (shaderPathId != 0)
                {
                    shadersToLoad.Add(afileInst.file.GetAssetInfo(shaderPathId));
                }
                else
                {
                    shadersToLoad.AddRange(afileInst.file.GetAssetsOfType(AssetClassID.Shader));
                }
                Logger.Info($"Shaders queued: {shadersToLoad.Count}");
                ConsoleUi.Section("Decompile");
                ConsoleUi.Info($"Assets: {assetsFileName}");
                ConsoleUi.Info($"Shaders Queued: {shadersToLoad.Count}");

                int successCount = 0;
                int failureCount = 0;

                for (int i = 0; i < shadersToLoad.Count; i++)
                {
                    AssetFileInfo shaderInf = shadersToLoad[i];
                    var shaderBf = manager.GetBaseField(afileInst, shaderInf);
                    if (shaderBf == null)
                    {
                        failureCount++;
                        ConsoleUi.Error($"Shader asset (path id {shaderInf.PathId}) could not be read.");
                        Logger.Warning($"Shader with path id {shaderInf.PathId} could not be read.");
                        continue;
                    }

                    var shaderName = shaderBf["m_ParsedForm"]["m_Name"].AsString;
                    ConsoleUi.Progress(i + 1, shadersToLoad.Count, shaderName);
                    Logger.StartShaderLog(shaderLogsRoot, shaderName, shaderInf.PathId);
                    try
                    {
                        Logger.Info($"Starting shader decompile: {shaderName} (path id {shaderInf.PathId})");
                        var shaderProcessor = new ShaderProcessor(shaderBf, ver.Value, platform);
                        string shaderText = shaderProcessor.Process();

                        string shaderDirectory = Path.Combine(Environment.CurrentDirectory, "out", Path.GetDirectoryName(shaderName)!);
                        Directory.CreateDirectory(shaderDirectory);
                        string outputPath = $"{Path.Combine(Environment.CurrentDirectory, "out", shaderName)}.shader";
                        File.WriteAllText(outputPath, shaderText);
                        Logger.Info($"Shader written to {outputPath}");
                        ConsoleUi.Success($"{shaderName} decompiled");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        ConsoleUi.Error($"{shaderName} failed");
                        Logger.Exception($"Failed to decompile shader: {shaderName} (path id {shaderInf.PathId})", ex);
                    }
                    finally
                    {
                        Logger.EndShaderLog();
                    }
                }

                ConsoleUi.Summary(
                    shadersToLoad.Count,
                    successCount,
                    failureCount,
                    Path.Combine(Environment.CurrentDirectory, "out"),
                    stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                ConsoleUi.Error($"Fatal error: {ex.Message}");
                Logger.Exception("Fatal error in Program.Main", ex);
                throw;
            }
            finally
            {
                Logger.Info("USCSandbox shutdown.");
                Logger.Shutdown();
            }
        }

        private static void RunAssetFileScan(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                string message = $"Scan path does not exist: {rootPath}";
                ConsoleUi.Error(message);
                Logger.Warning(message);
                return;
            }

            ConsoleUi.Section("Asset Scan");
            ConsoleUi.Info($"Root: {rootPath}");

            List<string> bundles = new();
            List<string> assets = new();
            List<string> noExtAssets = new();
            List<(string BundlePath, string AssetName)> bundleAssets = new();

            foreach (string file in EnumerateFilesSafe(rootPath))
            {
                string ext = Path.GetExtension(file);
                string fileName = Path.GetFileName(file);

                if (IsBundleFile(ext))
                {
                    bundles.Add(file);
                }
                else if (string.Equals(ext, ".assets", StringComparison.OrdinalIgnoreCase))
                {
                    assets.Add(file);
                }
                else if (string.IsNullOrEmpty(ext) && IsKnownNoExtensionAsset(fileName))
                {
                    noExtAssets.Add(file);
                }
            }

            bundles.Sort(StringComparer.OrdinalIgnoreCase);
            assets.Sort(StringComparer.OrdinalIgnoreCase);
            noExtAssets.Sort(StringComparer.OrdinalIgnoreCase);

            if (bundles.Count > 0)
            {
                AssetsManager scanManager = new();
                for (int i = 0; i < bundles.Count; i++)
                {
                    AddBundleAssetEntries(scanManager, bundles[i], bundleAssets);
                }
            }

            ConsoleUi.Section($"Bundles ({bundles.Count})");
            foreach (string path in bundles)
            {
                ConsoleUi.ListItem(Path.GetRelativePath(rootPath, path));
            }

            ConsoleUi.Section($"Assets ({assets.Count})");
            foreach (string path in assets)
            {
                ConsoleUi.ListItem(Path.GetRelativePath(rootPath, path));
            }

            ConsoleUi.Section($"No-Extension Asset Files ({noExtAssets.Count})");
            foreach (string path in noExtAssets)
            {
                ConsoleUi.ListItem(Path.GetRelativePath(rootPath, path));
            }

            ConsoleUi.Section($"Assets Inside Bundles ({bundleAssets.Count})");
            foreach ((string bundlePath, string assetName) in bundleAssets)
            {
                string relBundle = Path.GetRelativePath(rootPath, bundlePath);
                ConsoleUi.ListItem($"{relBundle} -> {assetName}");
            }

            if (bundles.Count == 0 && assets.Count == 0 && noExtAssets.Count == 0 && bundleAssets.Count == 0)
            {
                ConsoleUi.Warning("No Unity bundle/asset files were found.");
                return;
            }

            ConsoleUi.Section("Quick Commands");
            if (bundles.Count > 0)
            {
                ConsoleUi.ListItem($"USCSandbox \"{bundles[0]}\"");
            }
            if (assets.Count > 0)
            {
                ConsoleUi.ListItem($"USCSandbox null \"{assets[0]}\" 0 --all");
            }
            if (bundleAssets.Count > 0)
            {
                (string firstBundle, string firstAsset) = bundleAssets[0];
                ConsoleUi.ListItem($"USCSandbox \"{firstBundle}\" \"{firstAsset}\"");
                ConsoleUi.ListItem($"USCSandbox \"{firstBundle}\" \"{firstAsset}\" 0 --all");
            }
            else if (bundles.Count > 0)
            {
                ConsoleUi.ListItem($"USCSandbox \"{bundles[0]}\" \"sharedassets0.assets\" 0 --all");
            }
        }

        private static void AddBundleAssetEntries(
            AssetsManager manager,
            string bundlePath,
            List<(string BundlePath, string AssetName)> results)
        {
            try
            {
                var bundleFile = manager.LoadBundleFile(bundlePath, true);
                var dirInfos = bundleFile.file.BlockAndDirInfo.DirectoryInfos;
                for (int i = 0; i < dirInfos.Count; i++)
                {
                    var dirInfo = dirInfos[i];
                    if ((dirInfo.Flags & 4) == 0)
                    {
                        continue;
                    }

                    if (!dirInfo.Name.EndsWith(".assets", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    results.Add((bundlePath, dirInfo.Name));
                }
            }
            catch (Exception ex)
            {
                string message = $"Unable to inspect bundle contents: {bundlePath}";
                ConsoleUi.Warning(message);
                Logger.Exception(message, ex);
            }
        }

        private static IEnumerable<string> EnumerateFilesSafe(string rootPath)
        {
            Stack<string> directories = new();
            directories.Push(rootPath);

            while (directories.Count > 0)
            {
                string current = directories.Pop();

                string[] files;
                try
                {
                    files = Directory.GetFiles(current);
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < files.Length; i++)
                {
                    yield return files[i];
                }

                string[] subDirectories;
                try
                {
                    subDirectories = Directory.GetDirectories(current);
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < subDirectories.Length; i++)
                {
                    directories.Push(subDirectories[i]);
                }
            }
        }

        private static bool IsBundleFile(string extension)
        {
            return string.Equals(extension, ".unity3d", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".bundle", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".assetbundle", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".ab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKnownNoExtensionAsset(string fileName)
        {
            if (string.Equals(fileName, "globalgamemanagers", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(fileName, "maindata", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (fileName.StartsWith("level", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
    }
}
