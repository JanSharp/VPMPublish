using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Semver;

namespace VPMPublish
{
    public class ExecutionState
    {
        private bool didAbort;
        private string packageRoot;

        private PackageJson? packageJson;
        private SemVersion? version;
        private string? wholeChangelog;
        ///<summary>
        ///Excludes the `## [x.x.x] - YYYY-MM-DD` line.
        ///</summary>
        private string? changelogEntry;
        private string? tempDirName;
        private string? packageFileName;
        private ZipArchive? packageArchive;
        private string? sha256Checksum;

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

        public async Task<int> Publish()
        {
            try
            {
                LoadPackageJson();
                ValidatePackageJson();
                LoadChangelog();
                ValidateChangelog();
                PrepareForPackage();
                AddAllFilesToTheZipPackage();
                CalculateSha256Checksum();
                // Done.
                CleanupPackage();
                await Task.Delay(0); // HACK: To make it stop complaining about async.v
            }
            catch (Exception e)
            {
                CleanupPackage();
                if (!didAbort)
                    throw;
                Console.Error.WriteLine(e.Message);
                Console.Error.Flush();
                return 1;
            }
            return 0;
        }

        private void LoadPackageJson()
        {
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
            packageJson = JsonSerializer.Deserialize<PackageJson>(fileStream);
            didAbort = false;

            if (packageJson == null)
                throw Abort("Invalid package.json... I don't have an error message to pass along.");
        }

        private static Regex packageUrlRegex = new Regex(
            @"^https://github\.com/(?<user>[^/]+)/(?<repo>[^/]+)/"
                + @"releases/download/v(?<version>[^/]+)/(?<name>[^/]+)\.zip$", RegexOptions.Compiled);

        private static Regex changelogUrlRegex = new Regex(
            @"^https://github\.com/(?<user>[^/]+)/(?<repo>[^/]+)/"
                + @"blob/v(?<version>[^/]+)/CHANGELOG\.md$", RegexOptions.Compiled);

        public void ValidatePackageJson()
        {
            string packageName = packageJson!.Name;
            string packageVersion = packageJson.Version;

            if (Path.GetFileName(packageRoot) != packageName)
                throw Abort($"The package.json \"name\" ({packageName}) and the "
                    + $"folder name ({Path.GetFileName(packageRoot)}) must match."
                );

            version = MayAbort(
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

        private void LoadChangelog()
        {
            string changelogPath = Path.Combine(packageRoot, "CHANGELOG.md");
            if (!File.Exists(changelogPath))
                throw Abort(new FileNotFoundException(
                    "The CHANGELOG.md file should be directly inside the 'package-root'. "
                        + "Changelogs are usually optional, however this publish script requires one. "
                        + "It also must follow the https://common-changelog.org format, "
                        + "the only slight exception being how 'unreleased' changes are kept in there.",
                    changelogPath
                ));

            wholeChangelog = File.ReadAllText(changelogPath);
        }

        private static Regex changelogEntryRegex = new Regex(@"
            ^\#\ Changelog
            (?:\r\n|\r|\n){2} # Disallow extra blank lines. Only 1 is accepted.
            \#\#\ \[(?<version>[^\]]+)\]\ -\ (?<date>[^\r\n]+) # The date gets validated later.
            (?:\r\n|\r|\n){2} # Again, disallow extra blanks.
            (?=[^\n\r]) # Except the next part starts with an optional newline match, which should be disallowed for the first match.
            (?<entry>
                (?:
                    (?:\r\n|\r|\n)? # Only optional because the first line won't have a leading newline.
                    (?!(?:\r\n|\r|\n)(?:\#\#\ |\[)) # Also not accepting a line starting with `[` as that is a link back ref.
                    [^\r\n]* # * not + because empty lines are valid.
                )+
            )
        ", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

        private static Regex dateRegex = new Regex(
            @"[0-9]{4}-[0-9]{2}-[0-9]{2}",
            RegexOptions.Compiled
        );

        private void ValidateChangelog()
        {
            Match entryMatch = changelogEntryRegex.Match(wholeChangelog!);

            if (!entryMatch.Success)
                throw Abort($"The changelog is malformed, please refer to "
                    + $"https://common-changelog.org and verify your changelog."
                );

            if (packageJson!.Version != entryMatch.Groups["version"].Value)
                throw Abort($"The version of the top entry in the changelog is '{entryMatch.Groups["version"].Value}' "
                    + $"while the version in the package.json is '{packageJson.Version}', which is a mismatch. \n"
                    + $"There's a good chance you forgot to update the changelog for this version, please refer to "
                    + $"// TODO: insert link to documentation for generating the changelog entry here here."
                );

            string dateStr = entryMatch.Groups["date"].Value;
            if (!dateRegex.IsMatch(dateStr))
                throw Abort($"The date for the top entry in the changelog is '{dateStr}' "
                    + $"which does not match the ISO-8601 format YYYY-MM-DD."
                );

            string expectedDateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (dateStr != expectedDateStr)
                throw Abort($"The date for the top entry in the changelog is '{dateStr}' "
                    + $"which does not match the expected date '{expectedDateStr}'. "
                    + $"Chances are that you just generated the changelog entry a few minutes ago "
                    + $"in which case this is a bit confusing, but the reason for that is the fact "
                    + $"the changelog dates are in UTC, so the change from one day to the next "
                    + $"most likely isn't at your midnight. \n"
                    + $"If this is the case, simply update the date in the changelog, then run "
                    + $"the commands 'git add CHANGELOG.md' and 'git commit --amend --no-edit'."
                );

            changelogEntry = entryMatch.Groups["entry"].Value;
        }

        private void PrepareForPackage()
        {
            tempDirName = Directory.CreateTempSubdirectory("VPMPublish").FullName;
            packageFileName = Path.Combine(tempDirName, packageJson!.Name + ".zip");
            FileStream fileStream = File.Create(packageFileName);
            packageArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, false, Encoding.UTF8);
        }

        private void AddAllFilesToTheZipPackage()
        {
            string Combine(string left, string right) => left == "" ? right : left + "/" + right;

            void Walk(DirectoryInfo currentDirectory, string currentRelativeName)
            {
                foreach (FileInfo fileInfo in currentDirectory.EnumerateFiles())
                {
                    string entryName = Combine(currentRelativeName, fileInfo.Name);
                    packageArchive!.CreateEntryFromFile(fileInfo.FullName, entryName);
                }
                foreach (DirectoryInfo dirInfo in currentDirectory.EnumerateDirectories())
                {
                    if (dirInfo.Name == ".git")
                        continue;
                    Walk(dirInfo, Combine(currentRelativeName, dirInfo.Name));
                }
            }

            Walk(new DirectoryInfo(packageRoot), "");
            packageArchive!.Dispose(); // Dispose to close the file stream.
        }

        private void CalculateSha256Checksum()
        {
            using FileStream fileStream = File.OpenRead(packageFileName!);
            sha256Checksum = Convert.ToHexString(SHA256.Create().ComputeHash(fileStream)).ToLower();
            fileStream.Close();
        }

        private void DeleteTempDir()
        {
            if (tempDirName != null && Directory.Exists(tempDirName))
                Directory.Delete(tempDirName, true);
        }

        ///<summary>
        ///Call this in a try catch block to clean up any disposable resources.
        ///Of course also call it at the end in order to clean up after everything is done.
        ///</summary>
        private void CleanupPackage()
        {
            packageArchive?.Dispose();
            try
            {
                DeleteTempDir();
            }
            catch {} // Don't care about failure, the cleanup function is running in a 
            // catch block already, so if this fails it's a secondary, non important error.
        }
    }
}
