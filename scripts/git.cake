#load "./utils.cake"

public static class MyGit
{
    
    public static string GetBranch(ICakeContext context)
    {
        context.Verbose("MyGit: Getting branch...");
        
        var ci = context.BuildSystem();

        var tfs = IsTfs(context);
        if (tfs) {
            var branchName = ci.TFBuild.Environment.Repository.Branch;
            context.Verbose("Returning VSTS branch: {0}", branchName);
            return branchName;
        }

        if(ci.TravisCI.IsRunningOnTravisCI) {
            var branchName = ci.TravisCI.Environment.Build.Branch;
            context.Verbose("Returning AppVeyor branch: {0}", branchName);
            return branchName;
        }

        if(ci.AppVeyor.IsRunningOnAppVeyor) {
            var branchName = ci.AppVeyor.Environment.Repository.Branch;
            context.Verbose("Returning AppVeyor branch: {0}", branchName);
            return branchName;
        }

        using(var process = context.StartAndReturnProcess("git", new ProcessSettings 
        {
            RedirectStandardOutput = true,
            Arguments = new ProcessArgumentBuilder()
                .Append("rev-parse")
                .Append("--abbrev-ref HEAD"),
        }))
        {
            process.WaitForExit();
            var branchName = string.Join("", process.GetStandardOutput());
            context.Verbose("Returning Git branch: {0}", branchName);
            return branchName;
        }
    }
}