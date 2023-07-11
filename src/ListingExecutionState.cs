using Semver;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VPMPublish
{
    public class ListingExecutionState
    {
        private string name;
        private string id;
        private string url;
        private string author;
        private string outputDir;
        private List<PackageData> packages;

        private struct PackageData
        {
            public string dir;
            public string name;
            public List<PackageVersion> versions;

            public PackageData(string dir, string name)
            {
                this.dir = dir;
                this.name = name;
                this.versions = new List<PackageVersion>();
            }
        }

        private struct PackageVersion
        {
            public PackageJson json;
            public string versionStr;
            public SemVersion version;

            public PackageVersion(PackageJson json, string versionStr, SemVersion version)
            {
                this.json = json;
                this.versionStr = versionStr;
                this.version = version;
            }
        }

        public ListingExecutionState(
            string name,
            string id,
            string url,
            string author,
            string outputDir,
            string[] packages)
        {
            this.name = name;
            this.id = id;
            this.url = url;
            this.author = author;
            this.outputDir = outputDir;
            this.packages = packages
                .Select(p => new PackageData(p, new DirectoryInfo(p).Name))
                .ToList();
        }

        public int GenerateVCCListing()
        {
            try
            {
                Validation.EnsureCommandAvailability();
                Validation.ValidateListingUrl(url);
                Validation.ValidateAuthorEmail(author);
                Validation.ValidateListingOutputDir(outputDir);
                foreach (PackageData package in packages)
                    Validation.ValidatePackageDir(package.dir);
                foreach (PackageData package in packages)
                    LoadTags(package);
                GenerateListingJson();
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

        private void LoadTags(PackageData package)
        {
            Util.Info($"Loading tags and their respective version of the package.json for package '{package.name}'.");

            Util.SetChildProcessWorkingDirectory(package.dir);

            foreach (string tag in Util.RunProcess("git", "tag", "--list", "v*"))
            {
                string versionStr = tag.Substring(1);
                if (!SemVersion.TryParse(versionStr, SemVersionStyles.Strict, out SemVersion version))
                    continue;

                string tagMessage = string.Join('\n', Util.RunProcess(
                    "git",
                    "for-each-ref",
                    $"refs/tags/{tag}",
                    "--count=1",
                    "--format=%(contents)"
                ));
                if (!Util.TryGetChecksumFromTagMessage(tagMessage, out string checksum))
                    continue;

                string packageJsonStr = string.Join('\n', Util.RunProcess(
                    "git",
                    "show",
                    $"refs/tags/{tag}:package.json"
                ));
                PackageJson? packageJson = Util.MayAbort(() => JsonSerializer.Deserialize<PackageJson>(packageJsonStr));
                if (packageJson == null)
                    throw Util.Abort("Invalid package.json... I don't have an error message to pass along.");

                Validation.ValidatePackageJson(package.dir, packageJson, out _, silent: true);

                if (packageJson.Version != versionStr)
                    throw Util.Abort($"The package.json for the tag {tag} has the version {packageJson.Version}, "
                        + $"which is a mismatch. Generally this shouldn't be possible when using just this tool, "
                        + $"so there probably was some git history rewriting going on, so uh, have fun fixing this."
                    );

                packageJson.ZipSHA256 = checksum;

                package.versions.Add(new PackageVersion(packageJson, versionStr, version));
            }
        }

        private void GenerateListingJson()
        {
            string outputFilename = Path.Combine(outputDir, Path.GetFileName(url));
            Util.Info($"Generating VCC listing json file at {outputFilename}");

            VCCListingJson listing = new VCCListingJson(
                name,
                id,
                url,
                author,
                packages.ToDictionary(
                    p => p.name,
                    p => new VCCListingJson.PackageVersionsJson(
                        p.versions.ToDictionary(v => v.versionStr, v => v.json)
                    )
                )
            );

            using FileStream fileStream = File.OpenWrite(outputFilename);
            JsonSerializer.Serialize(fileStream, listing!, new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
            fileStream.Close();
        }
    }
}
