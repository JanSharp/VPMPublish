using System.Text.Json;

namespace VPMPublish
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // JsonSerializerOptions options = new JsonSerializerOptions();
            string fileContent = File.ReadAllText(args[0]);
            PackageJson? packageJson = JsonSerializer.Deserialize<PackageJson>(fileContent);
            if (packageJson == null)
                return;

            // if (packageJson.TheRest != null)
            //     foreach (var kvp in packageJson.TheRest)
            //         Console.WriteLine($"{kvp.Key}: {kvp.Value.ValueKind}, {kvp.Value.ToString()}");

            // Console.WriteLine(packageJson.HideInEditor.HasValue);

            await Task.Delay(0); // Stop complaining!
        }
    }
}
