// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Runtime.InteropServices;
using Microsoft.DotNet.PlatformAbstractions;
using FluentAssertions;
using NuGet.Versioning;
using NuGet.Packaging.Core;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToCacheAProjectWithDependencies : SdkTest
    {
        private static string _runtimeOs;
        private static string _runtimeLibOs;
        private static string _runtimeRid;
        private static string _testArch;
        private static string _tfm = "netcoreapp1.0";

        static GivenThatWeWantToCacheAProjectWithDependencies()
        {
            var rid = RuntimeEnvironment.GetRuntimeIdentifier();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _runtimeOs = "win7";
                _runtimeLibOs = "win";
                _testArch = rid.Substring(rid.LastIndexOf("-") + 1);
                _runtimeRid = "win7-" + _testArch;
            }
            else
            {
                _runtimeOs = "unix";
                _runtimeLibOs = "unix";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // microsoft.netcore.dotnetapphost  has assets  only for osx.10.10
                    _runtimeRid = "osx.10.10-x64";
                }
                else
                {
                    _runtimeRid = rid;                    
                }
            }
        }

        [Fact]
        public void compose_dependencies()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleCache")
                .WithSource();

            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot, "SimpleCache.xml");

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");

            cacheCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:ComposeWorkingDir={WorkingDir}", "/p:DoNotDecorateComposeDir=true", "/p:PreserveComposeWorkingDir=true")
                .Should()
                .Pass();
            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);

            List<string> files_on_disk = new List < string > {
               "artifact.xml",
               $"runtime.{_runtimeRid}.microsoft.netcore.dotnetapphost/1.2.0-beta-001304-00/runtimes/{_runtimeRid}/native/apphost{Constants.ExeSuffix}",
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _testArch != "x86")
            {
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native{Constants.DynamicLibSuffix}");
            }
            cacheDirectory.Should().OnlyHaveFiles(files_on_disk);

            //valid artifact.xml
           var knownpackage = new HashSet<PackageIdentity>();

            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.Targets", NuGetVersion.Parse("1.2.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("System.Private.Uri", NuGetVersion.Parse("4.4.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.DotNetAppHost", NuGetVersion.Parse("1.2.0-beta-001304-00")));
            knownpackage.Add(new PackageIdentity($"runtime.{_runtimeOs}.System.Private.Uri", NuGetVersion.Parse("4.4.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.Platforms", NuGetVersion.Parse("1.2.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity($"runtime.{_runtimeRid}.Microsoft.NETCore.DotNetAppHost", NuGetVersion.Parse("1.2.0-beta-001304-00")));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _testArch != "x86")
            {
                knownpackage.Add(new PackageIdentity("runtime.native.System", NuGetVersion.Parse("4.4.0-beta-24821-02")));
                knownpackage.Add(new PackageIdentity($"runtime.{_runtimeRid}.runtime.native.System", NuGetVersion.Parse("4.4.0-beta-24821-02")));
            }

            var artifact = Path.Combine(OutputFolder, "artifact.xml");
            HashSet<PackageIdentity> packagescomposed = ParseCacheArtifacts(artifact);

            packagescomposed.Count.Should().Be(knownpackage.Count);

            foreach(var pkg in packagescomposed)
            {
                knownpackage.Should().Contain(elem => elem.Equals(pkg),"package {0}, version {1} was not expected to be cached", pkg.Id, pkg.Version);
            }
            
        }

        [Fact]
        public void compose_with_fxfiles()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleCache")
                .WithSource();


            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot, "SimpleCache.xml");

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");

            cacheCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:ComposeWorkingDir={WorkingDir}", "/p:DoNotDecorateComposeDir=true", "/p:SkipRemovingSystemFiles=true")
                .Should()
                .Pass();

            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);
            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               $"runtime.{_runtimeRid}.microsoft.netcore.dotnetapphost/1.2.0-beta-001304-00/runtimes/{_runtimeRid}/native/apphost{Constants.ExeSuffix}",
               $"runtime.{_runtimeOs}.system.private.uri/4.4.0-beta-24821-02/runtimes/{_runtimeLibOs}/lib/netstandard1.0/System.Private.Uri.dll"
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _testArch != "x86")
            {
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native{Constants.DynamicLibSuffix}");
            }
            cacheDirectory.Should().OnlyHaveFiles(files_on_disk);
        }

        [Fact]
        public void compose_dependencies_noopt()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleCache")
                .WithSource();


            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot, "SimpleCache.xml");

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");

            cacheCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeDir={OutputFolder}", $"/p:DoNotDecorateComposeDir=true", "/p:SkipOptimization=true", $"/p:ComposeWorkingDir={WorkingDir}", "/p:PreserveComposeWorkingDir=true")
                .Should()
                .Pass();

            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);

            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               $"runtime.{_runtimeRid}.microsoft.netcore.dotnetapphost/1.2.0-beta-001304-00/runtimes/{_runtimeRid}/native/apphost{Constants.ExeSuffix}",
               $"runtime.{_runtimeOs}.system.private.uri/4.4.0-beta-24821-02/runtimes/{_runtimeLibOs}/lib/netstandard1.0/System.Private.Uri.dll"
               };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _testArch != "x86")
            {
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native.a");
                files_on_disk.Add($"runtime.{_runtimeRid}.runtime.native.system/4.4.0-beta-24821-02/runtimes/{_runtimeRid}/native/System.Native{Constants.DynamicLibSuffix}");
            }

            cacheDirectory.Should().OnlyHaveFiles(files_on_disk);
        }

        [Fact]
        public void cache_nativeonlyassets()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("UnmanagedCache")
                .WithSource();

            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot);

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "outdir");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");
            cacheCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:ComposeWorkingDir={WorkingDir}", $"/p:ComposeDir={OutputFolder}", $"/p:DoNotDecorateComposeDir=true")
                .Should()
                .Pass();

            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);

            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               $"runtime.{_runtimeRid}.microsoft.netcore.dotnetapphost/1.2.0-beta-001304-00/runtimes/{_runtimeRid}/native/apphost{Constants.ExeSuffix}"
               };

            cacheDirectory.Should().OnlyHaveFiles(files_on_disk);
        }

        [Fact]
        public void compose_multifile()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("ProfileLists")
                .WithSource();

            ComposeCache cacheCommand = new ComposeCache(Stage0MSBuild, simpleDependenciesAsset.TestRoot, "NewtonsoftFilterProfile.xml");

            var OutputFolder = Path.Combine(simpleDependenciesAsset.TestRoot, "o");
            var WorkingDir = Path.Combine(simpleDependenciesAsset.TestRoot, "w");
            var additonalproj1 = Path.Combine(simpleDependenciesAsset.TestRoot, "NewtonsoftMultipleVersions.xml");
            var additonalproj2 = Path.Combine(simpleDependenciesAsset.TestRoot, "FluentAssertions.xml");

            cacheCommand
                .Execute($"/p:RuntimeIdentifier={_runtimeRid}", $"/p:TargetFramework={_tfm}", $"/p:Additionalprojects={additonalproj1}%3b{additonalproj2}", $"/p:ComposeDir={OutputFolder}", $"/p:ComposeWorkingDir={WorkingDir}", "/p:DoNotDecorateComposeDir=true")
                .Should()
                .Pass();
            DirectoryInfo cacheDirectory = new DirectoryInfo(OutputFolder);

            List<string> files_on_disk = new List<string> {
               "artifact.xml",
               @"newtonsoft.json/9.0.2-beta2/lib/netstandard1.1/Newtonsoft.Json.dll",
               @"newtonsoft.json/9.0.1/lib/netstandard1.0/Newtonsoft.Json.dll",
               @"fluentassertions.json/4.12.0/lib/netstandard1.3/FluentAssertions.Json.dll"
               };

            cacheDirectory.Should().HaveFiles(files_on_disk);

            var knownpackage = new HashSet<PackageIdentity>();

            knownpackage.Add(new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("9.0.1")));
            knownpackage.Add(new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("9.0.2-beta2")));
            knownpackage.Add(new PackageIdentity("FluentAssertions.Json", NuGetVersion.Parse("4.12.0")));

            var artifact = Path.Combine(OutputFolder, "artifact.xml");
            var packagescomposed = ParseCacheArtifacts(artifact);

            packagescomposed.Count.Should().BeGreaterThan(0);

            foreach (var pkg in knownpackage)
            {
                packagescomposed.Should().Contain(elem => elem.Equals(pkg), "package {0}, version {1} was not expected to be cached", pkg.Id, pkg.Version);
            }
        }

        private static HashSet<PackageIdentity> ParseCacheArtifacts(string path)
        {
            return new HashSet<PackageIdentity>(
                from element in XDocument.Load(path).Root.Elements("Package")
                select new PackageIdentity(
                    element.Attribute("Id").Value,
                    NuGetVersion.Parse(element.Attribute("Version").Value)));
        }
    }
}
