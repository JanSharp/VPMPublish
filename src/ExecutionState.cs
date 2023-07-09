using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Semver;

namespace VPMPublish
{
    public class ExecutionState
    {
        private bool didAbort;
        private string packageRoot;
        private string mainBranch;
        private bool validateOnly;
        private ProcessStartInfo startInfo;

        private PackageJson? packageJson;
        private SemVersion? version;
        private string? wholeChangelog;
        ///<summary>
        ///Since time is passing during the execution of this program,
        ///make sure the same date is used throughout all of it.
        ///</summary>
        private string? currentDateStr;
        ///<summary>
        ///Excludes the `## [x.x.x] - YYYY-MM-DD` line.
        ///</summary>
        private string? changelogEntry;
        private string? tempDirName;
        private string? packageFileName;
        private string? releaseNotesFileName;
        private ZipArchive? packageArchive;
        private string? sha256Checksum;

        public ExecutionState(string packageRoot, string mainBranch, bool validateOnly)
        {
            this.packageRoot = packageRoot;
            this.mainBranch = mainBranch;
            this.validateOnly = validateOnly;
            startInfo = new ProcessStartInfo()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                WorkingDirectory = packageRoot,
            };
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

        private void Info(string msg) => Console.WriteLine(msg);

        public int Publish()
        {
            string currentDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(packageRoot);
            try
            {
                EnsureCommandAvailability();
                EnsureGitHubCLIIsAuthenticated();
                EnsureIsMainBranch();
                EnsureCleanWorkingTree();
                LoadPackageJson();
                ValidatePackageJson();
                EnsureTagDoesNotExist();
                LoadChangelog();
                ValidateChangelog();
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
                if (!didAbort)
                    throw;
                Console.Error.WriteLine(e.Message);
                Console.Error.Flush();
                return 1;
            }
            finally
            {
                CleanupPackage();
                Directory.SetCurrentDirectory(currentDir);
            }
            return 0;
        }

        private void EnsureCommandAvailability()
        {
            Info("Ensuring that 'git' and 'gh' (GitHub CLI) programs are available.");

            // Both use the same arg.
            startInfo.ArgumentList.Clear();
            startInfo.ArgumentList.Add("--version");
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            startInfo.FileName = "git";
            using Process? gitProcess = Process.Start(startInfo);
            startInfo.FileName = "gh";
            using Process? ghProcess = Process.Start(startInfo);

            if (gitProcess == null || ghProcess == null)
                throw Abort($"This program require both git and the github cli to be installed "
                    + $"({(gitProcess == null ? "failed to start 'git'" : "'git' may be fine")}) "
                    + $"({(ghProcess == null ? "failed to start 'gh'" : "'gh' may be fine")})."
                );

            gitProcess.BeginErrorReadLine();
            gitProcess.BeginOutputReadLine();
            ghProcess.BeginErrorReadLine();
            ghProcess.BeginOutputReadLine();

            gitProcess.WaitForExit();
            ghProcess.WaitForExit();

            if (gitProcess.ExitCode != 0 || ghProcess.ExitCode != 0)
                throw Abort($"This program require both git and the github cli to be installed "
                    + $"({(gitProcess.ExitCode != 0 ? $"'git' exited with exit code {gitProcess.ExitCode}" : "'git' is fine")}) "
                    + $"({(ghProcess.ExitCode != 0 ? $"'gh' exited with exit code {ghProcess.ExitCode}" : "'gh' is fine")})."
                );

            gitProcess.Close();
            ghProcess.Close();
        }

        private List<string> CheckRunProcess(string? errorMsgPrefix, string fileName, params string[] args)
        {
            startInfo.FileName = fileName;
            startInfo.ArgumentList.Clear();
            foreach (string arg in args)
                startInfo.ArgumentList.Add(arg);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            using Process? process = Process.Start(startInfo);
            if (process == null)
                throw new Exception($"Unable to start a '{fileName}' process even "
                    + $"though their availability has been validated already."
                );

            List<string> lines = new List<string>();
            process.OutputDataReceived += (object o, DataReceivedEventArgs e) => {
                if (e.Data != null)
                    lines.Add(e.Data);
            };
            process.BeginOutputReadLine();

            List<string> errorLines = new List<string>();
            process.ErrorDataReceived += (object o, DataReceivedEventArgs e) => {
                if (e.Data != null)
                    errorLines.Add(e.Data);
            };
            process.BeginErrorReadLine();

            process.WaitForExit();

            if (process.ExitCode != 0)
                throw Abort((errorMsgPrefix == null ? "" : errorMsgPrefix + "\n\n")
                    + $"The process '{fileName}' exited with the exit code {process.ExitCode}.\n"
                    + $"The arguments were:\n{string.Join('\n', args.Select(a => $"'{a}'"))}\n\n"
                    + $"The process had the following error output:\n{string.Join('\n', errorLines)}"
                );

            process.Close();

            return lines;
        }

        private List<string> RunProcess(string fileName, params string[] args)
        {
            return CheckRunProcess(null, fileName, args);
        }

        private void EnsureGitHubCLIIsAuthenticated()
        {
            Info("Ensuring 'gh' is authenticated with github.com.");

            // Just use the generic error handling of RunProcess, as that will include the
            // error message produced by 'gh', which includes (minor, but good enough) instructions.
            RunProcess("gh", "auth", "status", "--hostname", "github.com");
        }

        private void EnsureIsMainBranch()
        {
            Info($"Ensuring the git branch '{mainBranch}' is checked out.");

            string currentBranch = RunProcess("git", "branch", "--show-current").First();
            if (currentBranch != mainBranch)
                throw Abort($"Must only publish from the '{mainBranch}' branch, "
                    + $"the currently checked out branch is '{currentBranch}'."
                );
        }

        private void EnsureCleanWorkingTree()
        {
            Info("Ensuring the git working is clean.");

            List<string> changes = RunProcess("git", "status", "--porcelain");
            if (changes.Any()) /// cSpell:ignore uncommited
                throw Abort($"The working tree must be clean - have no uncommited changes.\n"
                    + $"Current changes:\n{string.Join('\n', changes)}"
                );
        }

        private void EnsureRemoteIsReachable()
        {
            Info("Ensuring the git remote for the current branch is reachable.");

            CheckRunProcess(
                "Unable to reach the remote, make sure git authentication (https or ssh) "
                    + "is setup correctly. If you are using ssh, make sure to run 'ssh-add' "
                    + "if you haven't already this session.",
                "git", "fetch", "--dry-run"
            );
        }

        private void LoadPackageJson()
        {
            Info("Ensuring 'package.json' exists and reading it.");

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
            Info("Validating the name, version, url and changelogUrl in the package.json.");

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

        private void EnsureTagDoesNotExist()
        {
            string expectedTag = $"v{packageJson!.Version}";
            Info($"Ensuring that the git tag '{expectedTag}' doesn't already exist.");

            List<string> tags = RunProcess("git", "tag", "--list", expectedTag);
            if (tags.Any(t => t == expectedTag))
                throw Abort($"The git tag '{expectedTag}' already exists. If you are rerunning this program "
                    + $"after an error occurred and now you're getting this error, there's a very high chance "
                    + $"that all you have to do is run 'git tag --delete {expectedTag}' and if it's already "
                    + $"been pushed also 'git push origin :refs/tags/{expectedTag}'. For reference: "
                    + $"https://stackoverflow.com/questions/5480258/how-can-i-delete-a-remote-tag"
                );
        }

        private void LoadChangelog()
        {
            Info($"Ensuring CHANGELOG.md exists and reading it.");

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
            Info("Validating the top changelog entry in CHANGELOG.md, its version and date.");

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

            currentDateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (dateStr != currentDateStr)
                throw Abort($"The date for the top entry in the changelog is '{dateStr}' "
                    + $"which does not match the expected date '{currentDateStr}'. "
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
            Info("Creating folder in the system's temp directory and creating the zip file inside.");

            tempDirName = Directory.CreateTempSubdirectory("VPMPublish").FullName;

            packageFileName = Path.Combine(tempDirName, packageJson!.Name + ".zip");
            FileStream fileStream = File.Create(packageFileName);
            packageArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, false, Encoding.UTF8);

            // This file will be created when it's actually written to.
            releaseNotesFileName = Path.Combine(tempDirName, "release-notes.md");
        }

        private void AddAllFilesToTheZipPackage()
        {
            Info("Adding all files from the package to the zip archive (file).");

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
            Info("Calculating the sha256 checksum of the complete zip file.");

            using FileStream fileStream = File.OpenRead(packageFileName!);
            sha256Checksum = Convert.ToHexString(SHA256.Create().ComputeHash(fileStream)).ToLower();
            fileStream.Close();
        }

        private void GenerateReleaseNotes()
        {
            Info("Generating release notes for the GitHub release.");

            var file = File.CreateText(releaseNotesFileName!);
            file.WriteLine("// TODO: Link to human readable listing page here.");
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
            Info($"Creating the git tag 'v{packageJson!.Version}' and adding the sha256 checksum "
                + $"to its message in a machine readable way. Used when generating the VCC listing."
            );

            RunProcess(
                "git",
                "tag",
                "--annotate",
                $"--message=(zip package sha256 checksum: {sha256Checksum})",
                $"v{packageJson!.Version}"
            );
        }

        private void CreateGitHubRelease()
        {
            Info("Pushing the current branch, pushing tags and creating the GitHub release "
                + "with the zip file and release notes attached."
            );

            // Technically this doesn't have to push the main branch, because pushing the tag
            // does ultimately push all commits leading up to the tag, however it would not make sense
            // to have a tag that's ahead of th main branch, which it's supposed to be _on_ the main branch
            RunProcess("git", "push");
            RunProcess("git", "push", "--tags");
            RunProcess(
                "gh", "release", "create", $"v{packageJson!.Version}",
                packageFileName!,
                "--verify-tag",
                "--title", $"v{packageJson!.Version}",
                "--notes-file", releaseNotesFileName!
            );
        }

        private void IncrementVersionNumber()
        {
            Info("Incrementing version (including url and changelogUrl) in package.json and creating a commit locally.");

            SemVersion incrementedVersion = version!.WithPatch(version.Patch + 1);
            string incVersionStr = incrementedVersion.ToString();
            packageJson!.Url = packageJson.Url.Replace(packageJson.Version, incVersionStr);
            packageJson.ChangelogUrl = packageJson.ChangelogUrl!.Replace(packageJson.Version, incVersionStr);
            packageJson.Version = incVersionStr;

            using FileStream fileStream = File.OpenWrite(Path.Combine(packageRoot, "package.json"));
            JsonSerializer.Serialize(fileStream, packageJson, new JsonSerializerOptions()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
            fileStream.Close();

            RunProcess("git", "commit", "-a", "-m", $"Move to version `v{incVersionStr}`");
        }

        private void DeleteTempDir()
        {
            if (tempDirName != null && Directory.Exists(tempDirName))
            {
                Info("Deleting the temp directory.");
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
    }
}
