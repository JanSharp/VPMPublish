using System.Text.Json;
using System.Text.RegularExpressions;
using Semver;

namespace VPMPublish
{
    public class ExecutionState
    {
        private bool didAbort;
        private string packageRoot;

        public ExecutionState(string packageRoot)
        {
            this.packageRoot = packageRoot;
        }

        // I seriously dislike these Abort and MayAbort functions,
        // but it's at least somewhat better than not having them.

        private Exception Abort(string message)
        {
            didAbort = true;
            return new Exception(message);
        }

        private T Abort<T>(T exception) where T : Exception
        {
            didAbort = true;
            return exception;
        }

        private T MayAbort<T>(Func<T> func)
        {
            didAbort = true;
            T result = func();
            didAbort = false;
            return result;
        }

        private PackageJson? packageJsonCache;
        private PackageJson GetPackageJson()
        {
            if (packageJsonCache != null)
                return packageJsonCache;

            string packageJsonPath = Path.Combine(packageRoot, "package.json");
            if (!File.Exists(packageJsonPath))
                throw Abort(new FileNotFoundException(
                    "The package.json file should be directly inside the 'package-root'.",
                    packageJsonPath
                ));

            using var fileStream = File.OpenRead(packageJsonPath);
            // Can't use MayAbort with async functions. At least I don't know how to.
            didAbort = true;
            // For some reason DeserializeAsync is aborting the entire application
            // without any sort of error or exception...
            packageJsonCache = JsonSerializer.Deserialize<PackageJson>(fileStream);
            didAbort = false;

            if (packageJsonCache == null)
                throw Abort("Invalid package.json... I don't have an error message to pass along.");

            return packageJsonCache;
        }

        private string? changelogCache;
        private string GetChangelog()
        {
            if (changelogCache != null)
                return changelogCache;

            string changelogPath = Path.Combine(packageRoot, "CHANGELOG.md");
            if (!File.Exists(changelogPath))
                throw Abort(new FileNotFoundException(
                    "The CHANGELOG.md file should be directly inside the 'package-root'. "
                        + "Changelogs are usually optional, however this publish script requires one. "
                        + "It also must follow the https://common-changelog.org format, "
                        + "the only slight exception being how 'unreleased' changes are kept in there.",
                    changelogPath
                ));

            changelogCache = File.ReadAllText(changelogPath);
            return changelogCache;
        }

        public async Task<int> Publish()
        {
            try
            {
                await Validate();
            }
            catch (Exception e)
            {
                if (!didAbort)
                    throw;
                Console.Error.WriteLine(e.Message);
                Console.Error.Flush();
                return 1;
            }
            return 0;
        }

        private static Regex packageUrlRegex = new Regex(
            @"^https://github\.com/(?<user>[^/]+)/(?<repo>[^/]+)/"
                + @"releases/download/v(?<version>[^/]+)/(?<name>[^/]+)\.zip$", RegexOptions.Compiled);

        private static Regex changelogUrlRegex = new Regex(
            @"^https://github\.com/(?<user>[^/]+)/(?<repo>[^/]+)/"
                + @"blob/v(?<version>[^/]+)/CHANGELOG\.md$", RegexOptions.Compiled);

        public async Task Validate()
        {
            await Task.Delay(0); // HACK: To make it stop complaining about async.
            PackageJson packageJson = GetPackageJson();
            string changelog = GetChangelog();

            string packageName = packageJson.Name;
            string packageVersion = packageJson.Version;

            if (Path.GetFileName(packageRoot) != packageName)
                throw Abort($"The package.json \"name\" ({packageName}) and the "
                    + $"folder name ({Path.GetFileName(packageRoot)}) must match."
                );

            SemVersion version = MayAbort(
                () => SemVersion.Parse(packageVersion, SemVersionStyles.Strict)
            );

            Match urlMatch = packageUrlRegex.Match(packageJson.Url);
            if (!urlMatch.Success)
                throw Abort($"The package.json \"url\" must match the "
                    + $"regex \"{packageUrlRegex}\", got \"{packageJson.Url}\"."
                );

            string versionInUrl = urlMatch.Groups["version"].Value;
            if (versionInUrl != packageVersion)
                throw Abort($"The package.json \"url\" contains the version \"{versionInUrl}\" "
                    + $"while the \"version\" is \"{packageVersion}\", which is a mismatch."
                );

            string nameInUrl = urlMatch.Groups["name"].Value;
            if (nameInUrl != packageName)
                throw Abort($"The package.json \"url\" contains the name \"{nameInUrl}\" "
                    + $"while the \"name\" is \"{packageName}\", which is a mismatch."
                );

            if (packageJson.ChangelogUrl == null)
                throw Abort($"This publish program requires a \"changelogUrl\" in the package.json "
                    + $"(note, it'll have to match the regex {changelogUrlRegex})."
                );

            Match changelogUrlMatch = changelogUrlRegex.Match(packageJson.ChangelogUrl);
            if (!changelogUrlMatch.Success)
                throw Abort($"The package.json \"changelogUrl\" must match the "
                    + $"regex \"{changelogUrlMatch}\", got \"{packageJson.ChangelogUrl}\"."
                );

            string versionInChangelogUrl = changelogUrlMatch.Groups["version"].Value;
            if (versionInChangelogUrl != packageVersion)
                throw Abort($"The package.json \"changelogUrl\" contains the version \"{versionInChangelogUrl}\" "
                    + $"while the \"version\" is \"{packageVersion}\", which is a mismatch."
                );

            string userInUrl = urlMatch.Groups["user"].Value;
            string userInChangelogUrl = changelogUrlMatch.Groups["user"].Value;
            if (userInUrl != userInChangelogUrl)
                throw Abort($"The package.json \"url\" contains the github username \"{userInUrl}\" "
                    + $"while the \"changelogUrl\" contains \"{userInChangelogUrl}\", which is a mismatch."
                );

            string repoInUrl = urlMatch.Groups["repo"].Value;
            string repoInChangelogUrl = changelogUrlMatch.Groups["repo"].Value;
            if (repoInUrl != repoInChangelogUrl)
                throw Abort($"The package.json \"url\" contains the github repo name \"{repoInUrl}\" "
                    + $"while the \"changelogUrl\" contains \"{repoInChangelogUrl}\", which is a mismatch."
                );
        }
    }
}
