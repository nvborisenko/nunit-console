#addin "nuget:?package=Cake.ExtendedNuGet&version=1.0.0.24"
#addin "nuget:?package=NuGet.Core&version=2.14.0"
#addin "Cake.FileHelpers"

//////////////////////////////////////////////////////////////////////
// NUGET PACKAGES
//////////////////////////////////////////////////////////////////////

var CONSOLE_PACKAGES = new []
{
  "NUnit.ConsoleRunner"
};

var EXTENSION_PACKAGES = new []
{
  "NUnit.Extension.VSProjectLoader",
  "NUnit.Extension.NUnitProjectLoader",
  "NUnit.Extension.NUnitV2Driver",
  "NUnit.Extension.NUnitV2ResultWriter",
  "NUnit.Extension.TeamCityEventListener"
};

//////////////////////////////////////////////////////////////////////
// FILE PATHS
//////////////////////////////////////////////////////////////////////

var ROOT_DIR = Context.Environment.WorkingDirectory.FullPath + "/packaging/";
var WIX_PROJ = ROOT_DIR + "nunit/nunit.wixproj";
var RESOURCES_DIR = ROOT_DIR + "resources/";
var RUNNER_PACKAGES_DIR = ROOT_DIR + "runner-packages/";
var EXTENSION_PACKAGES_DIR = ROOT_DIR + "extension-packages/";
var PACKAGE_IMAGE_DIR = ROOT_DIR + "image/";
var IMAGE_ADDINS_DIR = PACKAGE_IMAGE_DIR + "addins/";
var COMPONENTS_FILE_PATH = PACKAGE_IMAGE_DIR + "COMPONENTS.txt";

//////////////////////////////////////////////////////////////////////
// TASK
//////////////////////////////////////////////////////////////////////

Task("FetchPackages")
.IsDependentOn("Clean")
.Does(() =>
{
    foreach(var package in CONSOLE_PACKAGES)
    {
        NuGetInstall(package, new NuGetInstallSettings {
						OutputDirectory = RUNNER_PACKAGES_DIR
					});
    }

    foreach(var package in EXTENSION_PACKAGES)
    {
        NuGetInstall(package, new NuGetInstallSettings {
						OutputDirectory = EXTENSION_PACKAGES_DIR
					});
    }
});

Task("CreatePackagingImage")
.IsDependentOn("Clean")
.IsDependentOn("FetchPackages")
.Does(() =>
{
    CopyDirectory(RESOURCES_DIR, PACKAGE_IMAGE_DIR);

    foreach(var packageDir in GetAllDirectories(RUNNER_PACKAGES_DIR))
		CopyPackageContents(packageDir, PACKAGE_IMAGE_DIR);

    foreach(var packageDir in GetAllDirectories(EXTENSION_PACKAGES_DIR))
		CopyPackageContents(packageDir, IMAGE_ADDINS_DIR);
});

Task("WriteComponentsFile")
.IsDependentOn("Clean")
.IsDependentOn("FetchPackages")
.Does(context =>
{
    List<string> lines = new List<string> { "This package contains the following components:", "" };

	var packageDirs = new [] { RUNNER_PACKAGES_DIR, EXTENSION_PACKAGES_DIR };

	foreach (var packageDir in packageDirs)
	{
		foreach(var nupkgPath in GetFiles(packageDir + "*/*.nupkg"))
		{
			var nupkg = new ZipPackage(nupkgPath.MakeAbsolute(context.Environment).FullPath);
			lines.Add(string.Format("{0} - {1}{2}{3}{2}", nupkg.Id, nupkg.Version, Environment.NewLine, nupkg.Summary));
		}
	}

	FileWriteLines(COMPONENTS_FILE_PATH, lines.ToArray());
});

Task("PackageMsi")
.IsDependentOn("WriteComponentsFile")
.IsDependentOn("CreatePackagingImage")
.Does(() =>
{
    MSBuild(WIX_PROJ, new MSBuildSettings()
        .WithTarget("Rebuild")
        .SetConfiguration(configuration)
        .WithProperty("Version", packageVersion)
        .WithProperty("DisplayVersion", packageVersion)
        .WithProperty("OutDir", PACKAGE_DIR)
        .WithProperty("Image", PACKAGE_IMAGE_DIR)
        .SetMSBuildPlatform(MSBuildPlatform.x86)
        .SetNodeReuse(false)
        );
});

Task("PackageZip")
.IsDependentOn("WriteComponentsFile")
.IsDependentOn("CreatePackagingImage")
.Does(() =>
{
    var zipFileName = string.Format("{0}NUnit.Console-{1}.zip", PACKAGE_DIR, packageVersion);
    Zip(PACKAGE_IMAGE_DIR, zipFileName);
});

Task("PackageZipAndMsi")
.IsDependentOn("PackageMsi")
.IsDependentOn("PackageZip");

//////////////////////////////////////////////////////////////////////
// HELPER METHODS
//////////////////////////////////////////////////////////////////////

public string[] GetAllDirectories(string dirPath)
{
    return System.IO.Directory.GetDirectories(dirPath);
}

public void CopyPackageContents(DirectoryPath packageDir, DirectoryPath outDir)
{
    var files = GetFiles(packageDir + "/tools/*");
    CopyFiles(files.Where(f => f.GetExtension() != ".addins"), outDir);
}