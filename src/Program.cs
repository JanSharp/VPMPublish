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

            var arg = new Argument<string>(
                "package-root",
                Directory.GetCurrentDirectory,
                "The path to the root of the package which resides in the Packages folder in Unity. "
                    + "When omitted uses the current working directory."
            );
            rootCommand.Add(arg);

            rootCommand.SetHandler(Publish, arg);

            int libExitCode = await rootCommand.InvokeAsync(args);
            return libExitCode != 0 ? libExitCode : exitCode;
        }

        private static async void Publish(string packageRoot)
        {
            var context = new ExecutionState(packageRoot);
            exitCode = await context.Publish();
        }
    }
}
