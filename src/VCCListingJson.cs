using System.Text.Json.Serialization;

namespace VPMPublish
{
    public class VCCListingJson
    {
        // https://vcc.docs.vrchat.com/vpm/repos

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("packages")]
        public Dictionary<string, PackageVersionsJson> Packages { get; set; }

        [JsonConstructor]
        public VCCListingJson(
            string name,
            string id,
            string url,
            string author,
            Dictionary<string, PackageVersionsJson> packages
        )
        {
            Name = name;
            Id = id;
            Url = url;
            Author = author;
            Packages = packages;
        }

        public class PackageVersionsJson
        {
            [JsonPropertyName("versions")]
            public Dictionary<string, PackageJson> Versions { get; set; }

            public PackageVersionsJson()
                : this(new Dictionary<string, PackageJson>())
            { }

            [JsonConstructor]
            public PackageVersionsJson(
                Dictionary<string, PackageJson> versions
            )
            {
                Versions = versions;
            }
        }
    }
}
