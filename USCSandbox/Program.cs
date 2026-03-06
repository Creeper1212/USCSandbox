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
    }
}

