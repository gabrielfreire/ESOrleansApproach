using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    string PackageVersion;

    [Parameter]
    readonly string Increase;

    [Parameter("The library name")]
    readonly string Project_Name;

    [Parameter("The service name")]
    readonly string ServiceName;
    AbsolutePath SrcDirectory => RootDirectory / "src";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    // Libraries
    AbsolutePath ChosenProjectDir => SrcDirectory / Project_Name;
    AbsolutePath ChosenProjectFile => ChosenProjectDir / $"{Project_Name}.csproj";
    AbsolutePath ChosenLibraryNugetPackageDirectory => OutputDirectory / $"SMARTPlatform.{ServiceName}.{Project_Name}.{PackageVersion}.nupkg";
    Target GetNextVersion => _ => _
        .Executes(() =>
        {
            var _files = Directory.GetFiles(OutputDirectory);
            if (_files.Any())
            {
                var _filesInfo = _files.Select(f => new FileInfo(f));
                var _last = _filesInfo.OrderByDescending(f => f.LastWriteTime).First();
                var _version = new Regex("\\d+\\.\\d+\\.\\d+").Match(_last.Name);
                if (!string.IsNullOrWhiteSpace(_version.Value))
                {
                    var _parts = _version.Value.Split(".");
                    var _major = int.Parse(_parts[0]);
                    var _minor = int.Parse(_parts[1]);
                    var _patch = int.Parse(_parts[2]);

                    Console.WriteLine($"Previous version: {_major}.{_minor}.{_patch}");
                    if (Increase == "major")
                    {
                        _major++;
                        _minor = 0;
                        _patch = 0;
                    }
                    else if (Increase == "minor")
                    {
                        _minor++;
                        _patch = 0;
                    }
                    else if (Increase == "patch")
                    {
                        _patch++;
                    }
                    PackageVersion = $"{_major}.{_minor}.{_patch}";
                    Console.WriteLine($"Next version: {PackageVersion}");
                    Console.WriteLine($"Package DIR: {ChosenLibraryNugetPackageDirectory}");
                }
            }
            else
            {
                // first version
                PackageVersion = "0.0.1";
            }
        });

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            ChosenProjectDir.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .DependsOn(GetNextVersion)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(ChosenProjectFile));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(ChosenProjectFile)
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Console.WriteLine($"Output to ==> {OutputDirectory}");
            DotNetPack(s => s
                .SetProject(ChosenProjectFile)
                .SetVersion(PackageVersion)
                .SetOutputDirectory(OutputDirectory)
                .EnableNoRestore()
                .EnableNoBuild());
        });

    string NugetSource = "IMSMaximsFeed";

    // maybe necessary or not
    Target AddSource => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            var sourceUrl = "https://pkgs.dev.azure.com/smartplatform/Libraries/_packaging/IMSMaximsFeed/nuget/v3/index.json";
            var sources = NuGetTasks.NuGetSourcesList();
            if (sources.Any(s => s.Text.Contains(NugetSource)))
            {
                NuGetTasks.NuGetSourcesRemove(s => s.SetName(NugetSource));
            }

            NuGetTasks.NuGetSourcesAdd(s => s
            .SetName(NugetSource)
            .SetSource(sourceUrl)
            .SetUserName("microsoft.email@imsmaxims.com")
            .SetPassword("<ms-password>").EnableNonInteractive());

        });

    Target Push => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            NuGetTasks.NuGetPush(new NuGetPushSettings()
                .SetApiKey("az")
                .SetNonInteractive(false)
                .SetSource(NugetSource)
                .SetTargetPath(ChosenLibraryNugetPackageDirectory));
        });

}
