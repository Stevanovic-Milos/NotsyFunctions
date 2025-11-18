using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Notsy.Helpers
{
    public class BlobStorageHelper
    {
        private readonly BlobContainerClient _containerClient;
        private readonly string _endpoint;


        public BlobStorageHelper(string accountName, string containerName, string clientId)
        {
            var managedIdentityCredential = new ManagedIdentityCredential(clientId);
            _endpoint = $"https://{accountName}.blob.core.windows.net/{containerName}";
            _containerClient = new BlobContainerClient(
                new Uri(_endpoint),
                managedIdentityCredential
            );
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken)
        {
            try
            {
                await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                var blobClient = _containerClient.GetBlobClient(fileName);

                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType
                    }
                }, cancellationToken);

                return $"{_endpoint}/{fileName}";
            }
            catch (Exception ex)
            {
                throw new Exception($"Upload failed: {ex.Message}", ex);
            }
        }
    }
}
