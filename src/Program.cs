using System.CommandLine;

namespace VPMPublish
{
    public class Program
    {
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

            return await rootCommand.InvokeAsync(args);
        }

        private static void Publish(string packageRoot)
        {
            var context = new ExecutionState();
            context.Publish(packageRoot);
        }
    }
}
