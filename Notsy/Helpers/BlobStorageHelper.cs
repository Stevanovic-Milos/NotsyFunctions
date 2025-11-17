using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Notsy.Helpers
{
    public class BlobStorageHelper
    {
        private readonly BlobContainerClient _containerClient;
        private readonly string _endpoint;

        public BlobStorageHelper(string accountName, string containerName)
        {
            _endpoint = $"https://{accountName}.blob.core.windows.net/{containerName}";
            _containerClient = new BlobContainerClient(
                new Uri(_endpoint),
                new DefaultAzureCredential()
            );
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                await _containerClient.CreateIfNotExistsAsync();

                var blobClient = _containerClient.GetBlobClient(fileName);

                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType
                    }
                });

                return $"{_endpoint}/{fileName}";
            }
            catch (Exception ex)
            {
                throw new Exception($"Upload failed: {ex.Message}", ex);
            }
        }
    }
}
