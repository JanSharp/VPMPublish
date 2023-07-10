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
            var mainBranchNameOption = new Option<string>(
                "--main-branch",
                () => "main",
                "The name of the main branch. One must only create packages from the main branch."
            );

            var publishCommand = new Command("publish", "Publish a package to GitHub.");
            rootCommand.AddCommand(publishCommand);
            publishCommand.Add(packageRootOption);
            publishCommand.Add(mainBranchNameOption);

            var validateOnlyOption = new Option<bool>(
                "--validate-only",
                "When set, only runs all validation steps, but won't actually perform any publish steps."
            );
            publishCommand.Add(validateOnlyOption);

            publishCommand.SetHandler(Publish, packageRootOption, mainBranchNameOption, validateOnlyOption);

            var changelogDraftCommand = new Command("changelog-draft", "Generate a changelog draft for the current version.");
            rootCommand.AddCommand(changelogDraftCommand);
            changelogDraftCommand.Add(packageRootOption);
            changelogDraftCommand.Add(mainBranchNameOption);

            changelogDraftCommand.SetHandler(ChangelogDraft, packageRootOption, mainBranchNameOption);

            var normalizePackageJsonCommand = new Command("normalize-package-json", "Normalize package.json, specifically the order of fields.");
            rootCommand.AddCommand(normalizePackageJsonCommand);
            normalizePackageJsonCommand.Add(packageRootOption);

            normalizePackageJsonCommand.SetHandler(NormalizePackageJson, packageRootOption);

            int libExitCode = await rootCommand.InvokeAsync(args);
            return libExitCode != 0 ? libExitCode : exitCode;
        }

        private static void Publish(string packageRoot, string mainBranch, bool validateOnly)
        {
            var context = new ExecutionState(packageRoot, mainBranch, validateOnly);
            exitCode = context.Publish();
        }

        private static void ChangelogDraft(string packageRoot, string mainBranch)
        {
            var context = new ExecutionState(packageRoot, mainBranch, false);
            exitCode = context.ChangelogDraft();
        }

        private static void NormalizePackageJson(string packageRoot)
        {
            var context = new ExecutionState(packageRoot, "main", false);
            exitCode = context.NormalizePackageJson();
        }
    }
}
