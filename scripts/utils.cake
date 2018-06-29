#reference "System"
#reference "System.IO"
#reference "System.IO.FileSystem"

public static bool IsTfs(ICakeContext context)
{
    return (context.EnvironmentVariable("TF_BUILD") == "True");
}

public static void ReplaceTextInFile(ICakeContext context, string fileName, string findText, string replaceText)
{
    context.Verbose($"ReplaceTextInFile: {fileName} {findText} {replaceText}");
    var contents = System.IO.File.ReadAllText(fileName, System.Text.Encoding.UTF8);
    contents = contents.Replace(findText, replaceText);
    context.Verbose($"Writing all Text: {contents}");
    System.IO.File.WriteAllText(fileName, contents, System.Text.Encoding.UTF8);
}

public static void SetBuildNumber(ICakeContext context, string version) {
     context.Verbose($"Set Build Number: {version}");

     if(context.BuildSystem().AppVeyor.IsRunningOnAppVeyor) {
         context.Verbose("Running on AppVeyor");

        // Update the AppVeyor version number.
        context.BuildSystem().AppVeyor.UpdateBuildVersion(version);
    }

    //BuildSystem.TFBuild.IsRunningOnVSTS
    if (IsTfs(context)){
        context.Verbose("Running on VSTS");

        // Update the TFS Build version number.
        context.BuildSystem().TFBuild.Commands.UpdateBuildNumber(version);
    }

}

public static string GetPlatformName(ICakeContext context)
{
    switch(context.Environment.Platform.Family)
    {
        case PlatformFamily.Windows:
            return "windows";
        case PlatformFamily.Linux:
            return "linux";
        case PlatformFamily.OSX:
            return "osx";
    }
    throw new InvalidOperationException("Could not get platform name.");
}


public static void EnsureEnvironmentVariable(ICakeContext context, string key, string expected = null)
{
    if(!context.HasEnvironmentVariable(key))
    {
        throw new InvalidOperationException($"Environment variable '{key}' has not been set.");
    }
    if(expected != null)
    {
        var value = context.EnvironmentVariable(key);
        if(!string.Equals(value, expected))
        {
            throw new InvalidOperationException($"Expected environment variable '{key}' to be '{expected}', but as '{value}'.");
        }
    }
}