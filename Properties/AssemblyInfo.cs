using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Windows;

[assembly: InternalsVisibleTo("Launcher.Tests")]
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: AssemblyMetadata("ReleaseChannel", "release")]
// VersionCode 与 StartRide/BuildInfo.cs 的 Version 对齐：major*10000 + minor*100 + patch
[assembly: AssemblyMetadata("VersionCode", "20901")]
[assembly: AssemblyCompany("肖又祺")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCopyright("Copyright © 2026 肖又祺 · StartRide")]
[assembly: AssemblyFileVersion("2.9.1.0")]
[assembly: AssemblyInformationalVersion("2.9.1")]
[assembly: AssemblyProduct("StartRide 启动器")]
[assembly: AssemblyTitle("StartRide 启动器")]
[assembly: TargetPlatform("Windows7.0")]
[assembly: SupportedOSPlatform("Windows7.0")]
[assembly: AssemblyVersion("2.9.1.0")]
