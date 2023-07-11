
namespace VPMPublish
{
    public class ListingExecutionState
    {
        private string name;
        private string id;
        private string url;
        private string author;
        private string outputDir;
        private string[] packages;

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
            this.packages = packages;
        }

        public int GenerateVCCListing()
        {
            try
            {
                Validation.EnsureCommandAvailability();
                Validation.ValidateListingUrl(url);
                Validation.ValidateAuthorEmail(author);
                Validation.ValidateListingOutputDir(outputDir);
                foreach (string package in packages)
                    Validation.ValidatePackageDir(package);
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
    }
}
