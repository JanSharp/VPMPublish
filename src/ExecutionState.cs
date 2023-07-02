using System.Text.Json;

namespace VPMPublish
{
    public class ExecutionState
    {
        public void Publish(string packageRoot)
        {
            string fileContent = File.ReadAllText(Path.Combine(packageRoot, "package.json"));
            PackageJson? packageJson = JsonSerializer.Deserialize<PackageJson>(fileContent);
        }
    }
}
