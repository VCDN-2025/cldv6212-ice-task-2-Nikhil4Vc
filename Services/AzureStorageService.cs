using Azure.Storage.Blobs;

namespace CharityIce2.Services
{
    public class AzureStorageService
    {
        private readonly BlobContainerClient _containerClient;

        public AzureStorageService(IConfiguration configuration)
        {
            var connectionString = configuration["AzureStorage:ConnectionString"];
            var containerName = configuration["AzureStorage:ContainerName"];
            _containerClient = new BlobContainerClient(connectionString, containerName);
            _containerClient.CreateIfNotExists();
        }

        public async Task<string> UploadFileAsync(IFormFile file)
        {
            var blobClient = _containerClient.GetBlobClient(Guid.NewGuid() + Path.GetExtension(file.FileName));
            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, true);
            return blobClient.Uri.ToString(); // Return URL
        }
    }
}
