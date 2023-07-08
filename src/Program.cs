using System.CommandLine;

namespace VPMPublish
{
    public static class Program
    {
        private static int exitCode = 0;

        private static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand(
                "Utility to publish VRChat Package Manager (VPM) packages to GitHub.");

            var packageRootOption = new Option<string>(
                "--package-root",
                Directory.GetCurrentDirectory,
                "The path to the root of the package which resides in the Packages folder in Unity. "
                    + "When omitted uses the current working directory."
            );

            var publishCommand = new Command("publish", "Publish a package to GitHub.");
            rootCommand.AddCommand(publishCommand);
            publishCommand.Add(packageRootOption);

            var mainBranchNameOption = new Option<string>(
                "--main-branch",
                () => "main",
                "The name of the main branch. One must only create packages from the main branch."
            );
            publishCommand.Add(mainBranchNameOption);

            publishCommand.SetHandler(Publish, packageRootOption, mainBranchNameOption);

            int libExitCode = await rootCommand.InvokeAsync(args);
            return libExitCode != 0 ? libExitCode : exitCode;
        }

        private static void Publish(string packageRoot, string mainBranch)
        {
            var context = new ExecutionState(packageRoot, mainBranch);
            exitCode = context.Publish();
        }
    }
}
