using System.Text.RegularExpressions;
using System.Diagnostics;
using Semver;

namespace VPMPublish
{
    public static class Validation
    {
        public static void EnsureCommandAvailability()
        {
            Util.Info("Ensuring that 'git' and 'gh' (GitHub CLI) programs are available.");

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                // Both use the same arg.
                ArgumentList = { "--version" },
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            startInfo.FileName = "git";
            using Process? gitProcess = Process.Start(startInfo);
            startInfo.FileName = "gh";
            using Process? ghProcess = Process.Start(startInfo);

            if (gitProcess == null || ghProcess == null)
                throw Util.Abort($"This program require both git and the github cli to be installed "
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
                throw Util.Abort($"This program require both git and the github cli to be installed "
                    + $"({(gitProcess.ExitCode != 0 ? $"'git' exited with exit code {gitProcess.ExitCode}" : "'git' is fine")}) "
                    + $"({(ghProcess.ExitCode != 0 ? $"'gh' exited with exit code {ghProcess.ExitCode}" : "'gh' is fine")})."
                );

            gitProcess.Close();
            ghProcess.Close();
        }

        public static void EnsureGitHubCLIIsAuthenticated()
        {
            Util.Info("Ensuring 'gh' is authenticated with github.com.");

            // Just use the generic error handling of RunProcess, as that will include the
            // error message produced by 'gh', which includes (minor, but good enough) instructions.
            Util.RunProcess("gh", "auth", "status", "--hostname", "github.com");
        }

        public static void EnsureIsMainBranch(string mainBranch)
        {
            Util.Info($"Ensuring the git branch '{mainBranch}' is checked out.");

            string currentBranch = Util.RunProcess("git", "branch", "--show-current").First();
            if (currentBranch != mainBranch)
                throw Util.Abort($"Must only publish from the '{mainBranch}' branch, "
                    + $"the currently checked out branch is '{currentBranch}'."
                );
        }

        public static void EnsureCleanWorkingTree()
        {
            Util.Info("Ensuring the git working is clean.");

            List<string> changes = Util.RunProcess("git", "status", "--porcelain");
            if (changes.Any()) /// cSpell:ignore uncommited
                throw Util.Abort($"The working tree must be clean - have no uncommited changes.\n"
                    + $"Current changes:\n{string.Join('\n', changes)}"
                );
        }

        public static void EnsureRemoteIsReachable()
        {
            Util.Info("Ensuring the git remote for the current branch is reachable.");

            Util.CheckRunProcess(
                "Unable to reach the remote, make sure git authentication (https or ssh) "
                    + "is setup correctly. If you are using ssh, make sure to run 'ssh-add' "
                    + "if you haven't already this session.",
                "git", "fetch", "--dry-run"
            );
        }

        private static Regex packageUrlRegex = new Regex(
            @"^https://github\.com/(?<user>[^/]+)/(?<repo>[^/]+)/"
                + @"releases/download/v(?<version>[^/]+)/(?<name>[^/]+)\.zip$", RegexOptions.Compiled);

        private static Regex changelogUrlRegex = new Regex(
            @"^https://github\.com/(?<user>[^/]+)/(?<repo>[^/]+)/"
                + @"blob/v(?<version>[^/]+)/CHANGELOG\.md$", RegexOptions.Compiled);

        public static void ValidatePackageJson(string packageRoot, PackageJson packageJson, out SemVersion version)
        {
            Util.Info("Validating the name, version, url and changelogUrl in the package.json.");

            string packageName = packageJson.Name;
            string packageVersion = packageJson.Version;

            if (Path.GetFileName(packageRoot) != packageName)
                throw Util.Abort($"The package.json \"name\" ({packageName}) and the "
                    + $"folder name ({Path.GetFileName(packageRoot)}) must match."
                );

            version = Util.MayAbort(
                () => SemVersion.Parse(packageVersion, SemVersionStyles.Strict)
            );

            Match urlMatch = packageUrlRegex.Match(packageJson.Url);
            if (!urlMatch.Success)
                throw Util.Abort($"The package.json \"url\" must match the "
                    + $"regex \"{packageUrlRegex}\", got \"{packageJson.Url}\"."
                );

            string versionInUrl = urlMatch.Groups["version"].Value;
            if (versionInUrl != packageVersion)
                throw Util.Abort($"The package.json \"url\" contains the version \"{versionInUrl}\" "
                    + $"while the \"version\" is \"{packageVersion}\", which is a mismatch."
                );

            string nameInUrl = urlMatch.Groups["name"].Value;
            if (nameInUrl != packageName)
                throw Util.Abort($"The package.json \"url\" contains the name \"{nameInUrl}\" "
                    + $"while the \"name\" is \"{packageName}\", which is a mismatch."
                );

            if (packageJson.ChangelogUrl == null)
                throw Util.Abort($"This publish program requires a \"changelogUrl\" in the package.json "
                    + $"(note, it'll have to match the regex {changelogUrlRegex})."
                );

            Match changelogUrlMatch = changelogUrlRegex.Match(packageJson.ChangelogUrl);
            if (!changelogUrlMatch.Success)
                throw Util.Abort($"The package.json \"changelogUrl\" must match the "
                    + $"regex \"{changelogUrlMatch}\", got \"{packageJson.ChangelogUrl}\"."
                );

            string versionInChangelogUrl = changelogUrlMatch.Groups["version"].Value;
            if (versionInChangelogUrl != packageVersion)
                throw Util.Abort($"The package.json \"changelogUrl\" contains the version \"{versionInChangelogUrl}\" "
                    + $"while the \"version\" is \"{packageVersion}\", which is a mismatch."
                );

            string userInUrl = urlMatch.Groups["user"].Value;
            string userInChangelogUrl = changelogUrlMatch.Groups["user"].Value;
            if (userInUrl != userInChangelogUrl)
                throw Util.Abort($"The package.json \"url\" contains the github username \"{userInUrl}\" "
                    + $"while the \"changelogUrl\" contains \"{userInChangelogUrl}\", which is a mismatch."
                );

            string repoInUrl = urlMatch.Groups["repo"].Value;
            string repoInChangelogUrl = changelogUrlMatch.Groups["repo"].Value;
            if (repoInUrl != repoInChangelogUrl)
                throw Util.Abort($"The package.json \"url\" contains the github repo name \"{repoInUrl}\" "
                    + $"while the \"changelogUrl\" contains \"{repoInChangelogUrl}\", which is a mismatch."
                );
        }

        public static void EnsureTagDoesNotExist(PackageJson packageJson)
        {
            string expectedTag = $"v{packageJson.Version}";
            Util.Info($"Ensuring that the git tag '{expectedTag}' doesn't already exist.");

            List<string> tags = Util.RunProcess("git", "tag", "--list", expectedTag);
            if (tags.Any(t => t == expectedTag))
                throw Util.Abort($"The git tag '{expectedTag}' already exists. If you are rerunning this program "
                    + $"after an error occurred and now you're getting this error, there's a very high chance "
                    + $"that all you have to do is run 'git tag --delete {expectedTag}' and if it's already "
                    + $"been pushed also 'git push origin :refs/tags/{expectedTag}'. For reference: "
                    + $"https://stackoverflow.com/questions/5480258/how-can-i-delete-a-remote-tag"
                );
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

        public static void ValidateChangelog(string wholeChangelog, PackageJson packageJson, string currentDateStr, out string changelogEntry)
        {
            Util.Info("Validating the top changelog entry in CHANGELOG.md, its version and date.");

            Match entryMatch = changelogEntryRegex.Match(wholeChangelog);

            if (!entryMatch.Success)
                throw Util.Abort($"The changelog is malformed, please refer to "
                    + $"https://common-changelog.org and verify your changelog."
                );

            if (packageJson.Version != entryMatch.Groups["version"].Value)
                throw Util.Abort($"The version of the top entry in the changelog is '{entryMatch.Groups["version"].Value}' "
                    + $"while the version in the package.json is '{packageJson.Version}', which is a mismatch. \n"
                    + $"There's a good chance you forgot to update the changelog for this version, please refer to "
                    + $"https://github.com/JanSharp/VPMPublish#creating-a-release"
                );

            string dateStr = entryMatch.Groups["date"].Value;
            if (!dateRegex.IsMatch(dateStr))
                throw Util.Abort($"The date for the top entry in the changelog is '{dateStr}' "
                    + $"which does not match the ISO-8601 format YYYY-MM-DD."
                );

            if (dateStr != currentDateStr)
                throw Util.Abort($"The date for the top entry in the changelog is '{dateStr}' "
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
    }
}
