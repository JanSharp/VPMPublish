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

            var humanReadableListingUrlOption = new Option<string>(
                "--listing-url",
                "The _human readable_ listing url, used in the generated release notes."
            );
            humanReadableListingUrlOption.IsRequired = true;
            publishCommand.Add(humanReadableListingUrlOption);

            var validateOnlyOption = new Option<bool>(
                "--validate-only",
                "When set, only runs all validation steps, but won't actually perform any publish steps."
            );
            publishCommand.Add(validateOnlyOption);

            publishCommand.SetHandler(Publish, packageRootOption, mainBranchNameOption, humanReadableListingUrlOption, validateOnlyOption);

            var changelogDraftCommand = new Command("changelog-draft", "Generate a changelog draft for the current version.");
            rootCommand.AddCommand(changelogDraftCommand);
            changelogDraftCommand.Add(packageRootOption);
            changelogDraftCommand.Add(mainBranchNameOption);

            changelogDraftCommand.SetHandler(ChangelogDraft, packageRootOption, mainBranchNameOption);

            var normalizePackageJsonCommand = new Command("normalize-package-json", "Normalize package.json, specifically the order of fields.");
            rootCommand.AddCommand(normalizePackageJsonCommand);
            normalizePackageJsonCommand.Add(packageRootOption);

            normalizePackageJsonCommand.SetHandler(NormalizePackageJson, packageRootOption);

            var generateVCCListing = new Command("generate-vcc-listing", "Generate the vcc listing and a human readable webpage. (Does not upload anything.)");
            rootCommand.AddCommand(generateVCCListing);

            ///cSpell:ignore jansharp
            var nameOption = new Option<string>("--name", "The human readable name of the VCC Listing.");
            var idOption = new Option<string>("--id", "The internal id of the VCC Listing. The format this is supposed to follow is unknown, "
                + "but my guess is this: https://docs.unity3d.com/2019.4/Documentation/Manual/cus-naming.html. For example 'com.jansharp.dummy'.");
            var urlOption = new Option<string>("--url", "The full url to the resulting vcc listing json file, for example https://jansharp.github.io/dummyvcclisting.json.");
            var authorOption = new Option<string>("--author", "The email address of the author for this listing.");
            var outputDirOption = new Option<string>("--out-dir", "The directory the json file and the webpage file will be written to.");
            var packagesArg = new Argument<string[]>("packages", "Paths to all the packages included in this listing.");
            nameOption.IsRequired = true;
            idOption.IsRequired = true;
            urlOption.IsRequired = true;
            authorOption.IsRequired = true;
            outputDirOption.IsRequired = true;

            generateVCCListing.Add(nameOption);
            generateVCCListing.Add(idOption);
            generateVCCListing.Add(urlOption);
            generateVCCListing.Add(authorOption);
            generateVCCListing.Add(outputDirOption);
            generateVCCListing.Add(packagesArg);

            generateVCCListing.SetHandler(
                GenerateVCCListing,
                nameOption,
                idOption,
                urlOption,
                authorOption,
                outputDirOption,
                packagesArg
            );

            int libExitCode = await rootCommand.InvokeAsync(args);
            return libExitCode != 0 ? libExitCode : exitCode;
        }

        private static void Publish(string packageRoot, string mainBranch, string listingUrl, bool validateOnly)
        {
            var context = new ExecutionState(packageRoot, mainBranch, listingUrl, validateOnly);
            exitCode = context.Publish();
        }

        private static void ChangelogDraft(string packageRoot, string mainBranch)
        {
            var context = new ExecutionState(packageRoot, mainBranch);
            exitCode = context.ChangelogDraft();
        }

        private static void NormalizePackageJson(string packageRoot)
        {
            var context = new ExecutionState(packageRoot, "main");
            exitCode = context.NormalizePackageJson();
        }

        private static void GenerateVCCListing(
            string name,
            string id,
            string url,
            string author,
            string outputDir,
            string[] packages)
        {
            var context = new ListingExecutionState(
                name,
                id,
                url,
                author,
                outputDir,
                packages
            );
            exitCode = context.GenerateVCCListing();
        }
    }
}
