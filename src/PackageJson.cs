using System.Text.Json;
using System.Text.Json.Serialization;

namespace VPMPublish
{
    public class PackageJson
    {
        // Unity fields. https://docs.unity3d.com/2019.4/Documentation/Manual/upm-manifestPkg.html

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        ///<summary>
        ///This is optional according to Unity, VRChat makes it mandatory.
        ///</summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("unity")]
        public string? Unity { get; set; }

        ///<summary>
        ///This is optional according to Unity, VRChat makes it mandatory.
        ///</summary>
        [JsonPropertyName("author")]
        public AuthorJson Author { get; set; }

        [JsonPropertyName("dependencies")]
        public Dictionary<string, string>? Dependencies { get; set; }

        [JsonPropertyName("hideInEditor")]
        public bool? HideInEditor { get; set; }

        [JsonPropertyName("keywords")]
        public string[]? Keywords { get; set; }

        [JsonPropertyName("license")]
        public string? License { get; set; }

        [JsonPropertyName("samples")]
        public SampleJson[]? Samples { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("unityRelease")]
        public string? UnityRelease { get; set; }

        // VRChat fields. https://vcc.docs.vrchat.com/vpm/packages#vpm-manifest-additions

        /// <summary>
        /// This field is a Schrödinger's cat experiment. It is both required and optional until we go test it ourselves.
        /// </summary>
        [JsonPropertyName("vpmDependencies")]
        public Dictionary<string, string> VpmDependencies { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("legacyFolders")]
        public Dictionary<string, string>? LegacyFolders { get; set; }

        [JsonPropertyName("legacyFiles")]
        public Dictionary<string, string>? LegacyFiles { get; set; }

        [JsonPropertyName("zipSHA256")]
        public string? ZipSHA256 { get; set; }

        [JsonPropertyName("changelogUrl")]
        public string? ChangelogUrl { get; set; }

        // Any other custom fields. https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/handle-overflow?pivots=dotnet-7-0

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? TheRest { get; set; }

        [JsonConstructor]
        public PackageJson(
            string name,
            string displayName,
            string version,
            AuthorJson author,
            string url,
            Dictionary<string, string> vpmDependencies
        )
        {
            Name = name;
            DisplayName = displayName;
            Version = version;
            Author = author;
            Url = url;
            VpmDependencies = vpmDependencies;
        }

        public class AuthorJson
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            ///<summary>
            ///This is optional according to Unity, VRChat makes it mandatory.
            ///</summary>
            [JsonPropertyName("email")]
            public string Email { get; set; }

            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonConstructor]
            public AuthorJson(
                string name,
                string email
            )
            {
                Name = name;
                Email = email;
            }
        }

        public class SampleJson
        {
            [JsonPropertyName("displayName")]
            public string DisplayName { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("path")]
            public string Path { get; set; }

            [JsonConstructor]
            public SampleJson(
                string displayName,
                string description,
                string path
            )
            {
                DisplayName = displayName;
                Description = description;
                Path = path;
            }
        }
    }
}
