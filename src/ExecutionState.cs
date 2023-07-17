using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Semver;

namespace VPMPublish
{
    public class ExecutionState
    {
        private string packageRoot;
        private string mainBranch;
        private string? listingUrl;
        private bool validateOnly;
        private bool allowDirtyWorkingTree;
        ///<summary>
        ///Since time is passing during the execution of this program,
        ///make sure the same date is used throughout all of it.
        ///</summary>
        private string currentDateStr;

        private PackageJson? packageJson;
        private SemVersion? version;
        private string? wholeChangelog;
        ///<summary>
        ///Excludes the `## [x.x.x] - YYYY-MM-DD` line.
        ///</summary>
        private string? changelogEntry;
        private string? tempDirName;
        private string? packageFileName;
        private string? releaseNotesFileName;
        private ZipArchive? packageArchive;
        private string? sha256Checksum;

        public ExecutionState(
            string packageRoot,
            string mainBranch,
            string? listingUrl = null,
            bool validateOnly = false,
            bool allowDirtyWorkingTree = false)
        {
            this.packageRoot = packageRoot;
            this.mainBranch = mainBranch;
            this.listingUrl = listingUrl;
            this.validateOnly = validateOnly;
            this.allowDirtyWorkingTree = allowDirtyWorkingTree;
            currentDateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        public int Publish()
        {
            Util.SetChildProcessWorkingDirectory(packageRoot);
            try
            {
                Validation.EnsureCommandAvailability();
                Validation.EnsureGitHubCLIIsAuthenticated();
                Validation.EnsureIsMainBranch(mainBranch);
                Validation.EnsureCleanWorkingTree();
                Validation.EnsureRemoteIsReachable();
                LoadPackageJson();
                Validation.ValidatePackageJson(packageRoot, packageJson!, out version);
                Validation.EnsureTagDoesNotExist(packageJson!);
                LoadChangelog();
                Validation.ValidateChangelog(wholeChangelog!, packageJson!, currentDateStr, out changelogEntry);
                if (validateOnly)
                    return 0;
                PrepareForPackage();
                AddAllFilesToTheZipPackage();
                CalculateSha256Checksum();
                GenerateReleaseNotes();
                CreateGitTag();
                CreateGitHubRelease();
                IncrementVersionNumber();
            }
            catch (Exception e)
            {
                if (!Util.DidAbort)
                    throw;
                Console.Error.WriteLine(e.Message);
                Console.Error.Flush();
                return 1;
            }
            finally
            {
                CleanupPackage();
            }
            return 0;
        }

        public int ChangelogDraft()
        {
            Util.SetChildProcessWorkingDirectory(packageRoot);
            try
            {
                Validation.EnsureCommandAvailability();
                Validation.EnsureGitHubCLIIsAuthenticated();
                Validation.EnsureIsMainBranch(mainBranch);
                if (!allowDirtyWorkingTree)
                    Validation.EnsureCleanWorkingTree();
                LoadPackageJson();
                Validation.ValidatePackageJson(packageRoot, packageJson!, out version);
                Validation.EnsureTagDoesNotExist(packageJson!);
                LoadChangelog(acceptMissing: true);
                GenerateChangelogDraft();
            }
            catch (Exception e)
            {
                if (!Util.DidAbort)
                    throw;
                Console.Error.WriteLine(e.Message);
                Console.Error.Flush();
                return 1;
            }
            return 0;
        }

        public int NormalizePackageJson()
        {
            Util.SetChildProcessWorkingDirectory(packageRoot);
            try
            {
                LoadPackageJson();
                Validation.ValidatePackageJson(packageRoot, packageJson!, out version);
                SerializePackageJson(silent: false);
            }
            catch (Exception e)
            {
                if (!Util.DidAbort)
                    throw;
                Console.Error.WriteLine(e.Message);
                Console.Error.Flush();
                return 1;
            }
            return 0;
        }

        private void LoadPackageJson()
        {
            Util.Info("Ensuring 'package.json' exists and reading it.");

            string packageJsonPath = Path.Combine(packageRoot, "package.json");
            if (!File.Exists(packageJsonPath))
                throw Util.Abort(new FileNotFoundException(
                    "The package.json file should be directly inside the 'package-root'.",
                    packageJsonPath
                ));

            using var fileStream = File.OpenRead(packageJsonPath);
            packageJson = Util.MayAbort(() => JsonSerializer.Deserialize<PackageJson>(fileStream));

            if (packageJson == null)
                throw Util.Abort("Invalid package.json... I don't have an error message to pass along.");
        }

        private static Regex packageUrlRegex = new Regex(
            @"^https://github\.com/(?<user>[^/]+)/(?<repo>[^/]+)/"
                + @"releases/download/v(?<version>[^/]+)/(?<name>[^/]+)\.zip$", RegexOptions.Compiled);

        private static Regex changelogUrlRegex = new Regex(
            @"^https://github\.com/(?<user>[^/]+)/(?<repo>[^/]+)/"
                + @"blob/v(?<version>[^/]+)/CHANGELOG\.md$", RegexOptions.Compiled);

        private void LoadChangelog(bool acceptMissing = false)
        {
            Util.Info($"{(acceptMissing ? "Checking if" : "Ensuring")} CHANGELOG.md exists and reading it.");

            string changelogPath = Path.Combine(packageRoot, "CHANGELOG.md");
            if (!File.Exists(changelogPath))
            {
                if (acceptMissing)
                    return;
                throw Util.Abort(new FileNotFoundException(
                    "The CHANGELOG.md file should be directly inside the 'package-root'. "
                        + "Changelogs are usually optional, however this publish script requires one. "
                        + "It also must follow the https://common-changelog.org format, "
                        + "the only slight exception being how 'unreleased' changes are kept in there.",
                    changelogPath
                ));
            }

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

        private void PrepareForPackage()
        {
            Util.Info("Creating folder in the system's temp directory and creating the zip file inside.");

            tempDirName = Directory.CreateTempSubdirectory("VPMPublish").FullName;

            packageFileName = Path.Combine(tempDirName, packageJson!.Name + ".zip");
            FileStream fileStream = File.Create(packageFileName);
            packageArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, false, Encoding.UTF8);

            // This file will be created when it's actually written to.
            releaseNotesFileName = Path.Combine(tempDirName, "release-notes.md");
        }

        private void AddAllFilesToTheZipPackage()
        {
            Util.Info("Adding all files from the package to the zip archive (file).");

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
            Util.Info("Calculating the sha256 checksum of the complete zip file.");

            using FileStream fileStream = File.OpenRead(packageFileName!);
            sha256Checksum = Convert.ToHexString(SHA256.Create().ComputeHash(fileStream)).ToLower();
            fileStream.Close();
        }

        private void GenerateReleaseNotes()
        {
            Util.Info("Generating release notes for the GitHub release.");

            var file = File.CreateText(releaseNotesFileName!);
            file.WriteLine("# Installing");
            file.WriteLine();
            file.WriteLine($"Go to the [VCC Listing]({listingUrl}) page and follow the instructions there.");
            file.WriteLine();
            file.WriteLine("# Changelog");
            file.WriteLine();
            file.WriteLine($"## {packageJson!.Version} - {currentDateStr!}");
            file.WriteLine();
            file.WriteLine(changelogEntry!);
            file.WriteLine();
            file.WriteLine("# Zip sha256 checksum");
            file.WriteLine();
            file.WriteLine($"`{sha256Checksum!}`");
            file.Close();
        }

        private void CreateGitTag()
        {
            Util.Info($"Creating the git tag 'v{packageJson!.Version}' and adding the sha256 checksum "
                + $"to its message in a machine readable way. Used when generating the VCC listing."
            );

            Util.RunProcess(
                "git",
                "tag",
                "--annotate",
                $"--message={Util.FormatChecksumForTagMessage(sha256Checksum!)}",
                $"v{packageJson!.Version}"
            );
        }

        private void CreateGitHubRelease()
        {
            // Technically this doesn't have to push the main branch, because pushing the tag
            // does ultimately push all commits leading up to the tag, however it would not make sense
            // to have a tag that's ahead of th main branch, which it's supposed to be _on_ the main branch
            Util.Info("Pushing the current branch and pushing tags.");
            Util.RunProcess("git", "push");
            Util.RunProcess("git", "push", "--tags");

            Util.Info("Creating the GitHub release with the zip file and release notes attached.");
            Util.RunProcess(
                "gh", "release", "create", $"v{packageJson!.Version}",
                packageFileName!,
                "--verify-tag",
                "--title", $"v{packageJson!.Version}",
                "--notes-file", releaseNotesFileName!
            );
        }

        private void SerializePackageJson(bool silent)
        {
            if (!silent)
                Util.Info("Serializing package.json data and writing back to the file.");

            using FileStream fileStream = File.OpenWrite(Path.Combine(packageRoot, "package.json"));
            JsonSerializer.Serialize(fileStream, packageJson!, new JsonSerializerOptions()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                // Since the package.json file in the project itself is known to be a json file,
                // and is human readable, this should not be of concern. It only really matters for
                // < and > in regards to semantic version ranges.
                // When generating the listing, it will escape those characters in strings,
                // because that part is not going through this code path here.
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            fileStream.SetLength(fileStream.Position);
            fileStream.Close();
        }

        private void IncrementVersionNumber()
        {
            Util.Info("Incrementing version (including url and changelogUrl) in package.json and creating a commit locally.");

            SemVersion incrementedVersion = version!.WithPatch(version.Patch + 1);
            string incVersionStr = incrementedVersion.ToString();
            packageJson!.Url = packageJson.Url.Replace(packageJson.Version, incVersionStr);
            packageJson.ChangelogUrl = packageJson.ChangelogUrl!.Replace(packageJson.Version, incVersionStr);
            packageJson.Version = incVersionStr;

            SerializePackageJson(silent: true);

            Util.RunProcess("git", "commit", "-a", "-m", $"Move to version `v{incVersionStr}`");
        }

        private void DeleteTempDir()
        {
            if (tempDirName != null && Directory.Exists(tempDirName))
            {
                Util.Info("Deleting the temp directory.");
                Directory.Delete(tempDirName, true);
            }
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

        private static Regex findFirstInsertLocationRegex = new Regex(
            @"^(?:\r\n|\r|\n)# Changelog(?:\r\n|\r|\n){2}(?<pos>)",
            RegexOptions.Compiled
        );
        private static Regex findSecondInsertLocationRegex = new Regex(
            @"(?<pos>)(?:(?:\r\n|\r|\n)\[[^\r\n]+)+(?:\r\n|\r|\n)$",
            RegexOptions.Compiled | RegexOptions.RightToLeft
        );

        private static Regex newlineRegex = new Regex(
            @"(?:\r\n|\r|\n)",
            RegexOptions.Compiled
        );

        private void GenerateChangelogDraft()
        {
            Util.Info($"Generating changelog entry for `v{packageJson!.Version}`.");

            string part1 = "";
            string part2 = "";
            string? lastVersion = null;

            string lf = "\n";

            if (wholeChangelog != null)
            {
                Match changelogMatch = changelogEntryRegex.Match(wholeChangelog);
                Match firstMatch = findFirstInsertLocationRegex.Match(wholeChangelog);
                Match secondMatch = findSecondInsertLocationRegex.Match(wholeChangelog);
                if (!changelogMatch.Success || !firstMatch.Success || !secondMatch.Success)
                    throw Util.Abort($"The changelog is malformed, please refer to "
                        + $"https://common-changelog.org and verify your changelog. "
                        + $"Note that this script requires exactly 1 blank line at "
                        + $"both the top and bottom of the changelog file."
                    );

                lastVersion = changelogMatch.Groups["version"].Value;
                if (lastVersion == packageJson!.Version)
                    throw Util.Abort($"The changelog already contains an entry for the current version "
                        + $"{packageJson!.Version}. Cannot generate the same version entry twice."
                    );

                int firstPosition = firstMatch.Groups["pos"].Index;
                int secondPosition = secondMatch.Groups["pos"].Index + 1;

                part1 = wholeChangelog.Substring(firstPosition, secondPosition - firstPosition);
                part2 = wholeChangelog.Substring(secondPosition);

                // Find most commonly used new line.
                lf = newlineRegex.Matches(wholeChangelog)
                    .Select(m => m.Value)
                    .GroupBy(c => c)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault("\n");
            }

            Match urlMatch = packageUrlRegex.Match(packageJson!.Url);

            string user = urlMatch.Groups["user"].Value;
            string repo = urlMatch.Groups["repo"].Value;

            string logFormat = $"--pretty=- %s ([`%h`](https://github.com/{user}/{repo}/commit/%H))";

            // The "--" at the end tells git that the part after log is the revision, not a file path
            // This only matters when the tag it's trying to use here doesn't exist, that way it gives
            // a proper and useful error message

            List<string> log = wholeChangelog != null
                ? Util.RunProcess("git", "log", $"v{lastVersion}..HEAD", logFormat, "--")
                : Util.RunProcess("git", "log", logFormat);

            wholeChangelog = lf
                + $"# Changelog" + lf
                + lf
                + $"## [{packageJson.Version}] - {currentDateStr}" + lf
                + lf
                + $"_//" + $" TODO: Arrange the changes their appropriate categories, combine them, "
                + $"or remove them. Use https://common-changelog.org for reference._" + lf
                + lf
                + $"### Temp Draft" + lf
                + lf
                + string.Join(lf, log) + lf
                + lf
                + $"### Changed" + lf
                + lf
                + $"### Added" + lf
                + lf
                + $"### Removed" + lf
                + lf
                + $"### Fixed" + lf
                + lf
                + part1
                + $"[{packageJson.Version}]: https://github.com/{user}/{repo}/releases/tag/v{packageJson.Version}" + lf
                + part2;

            File.WriteAllText(Path.Combine(packageRoot, "CHANGELOG.md"), wholeChangelog);

            Util.Info($"Use the commit message: Update changelog for v`{packageJson.Version}`");
        }
    }
}
