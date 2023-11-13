using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO;

namespace AzureAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BlobController : Controller
    {
        private readonly AzureStorageConfig storageConfig = null;
        private readonly BlobContainerClient _container = null;

        public BlobController(IOptions<AzureStorageConfig> config)
        {
            storageConfig = config.Value;
            _container = new BlobContainerClient(storageConfig.connectionString, storageConfig.ImageContainer);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Upload(ICollection<IFormFile> files)
        {
            bool isUploaded = false;

            try
            {
                if (files.Count == 0)
                    return BadRequest("No files received from the upload");

                if (storageConfig.AccountKey == string.Empty || storageConfig.AccountName == string.Empty)
                    return BadRequest("sorry, can't retrieve your azure storage details from appsettings.js, make sure that you add azure storage details there");

                if (storageConfig.ImageContainer == string.Empty)
                    return BadRequest("Please provide a name for your image container in the azure blob storage");

                foreach (var formFile in files)
                {
                    if (StorageHelper.IsImage(formFile))
                    {
                        if (formFile.Length > 0)
                        {
                            using (Stream stream = formFile.OpenReadStream())
                            {
                                var extension = Path.GetExtension(formFile.FileName);
                                string fileName = Guid.NewGuid().ToString("N").ToUpper() + extension;
                                isUploaded = await StorageHelper.UploadFileToStorage(stream, fileName, storageConfig);
                            }
                        }
                    }
                    else
                    {
                        return new UnsupportedMediaTypeResult();
                    }
                }

                if (isUploaded)
                {
                    if (storageConfig.ThumbnailContainer != string.Empty)
                    {
                        return Ok();
                    }

                    else
                        return new AcceptedResult();
                }
                else
                    return BadRequest("Look like the image couldnt upload to the storage");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost("[action]")]
        public async Task<IActionResult> ListAll()
        {
            try
            {
                List<string> thumbnailUrls = new List<string>();
                List<ImageObject> ImageslIST = new List<ImageObject>();
                if (_container.Exists())
                {
                    foreach (BlobItem blobItem in _container.GetBlobs())
                    {
                        ImageObject image = new ImageObject();
                        image.Name = blobItem.Name;
                        image.Uri = _container.Uri + "/" + blobItem.Name;
                        var memoryStream = new MemoryStream();
                        _container.GetBlobClient(blobItem.Name).DownloadTo(memoryStream);
                        byte[] imageBytes = memoryStream.ToArray();
                        image.ImageString = imageBytes;
                        ImageslIST.Add(image);
                    }
                }
                return new ObjectResult(ImageslIST);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPut("[action]")]
        public async Task<IActionResult> DeleteFromAzure(string blobName)
        {
            try
            {
                var blobClient = _container.GetBlobClient(blobName);

                if (blobClient.Exists())
                {
                    blobClient.Delete();
                    return Ok();
                }
                else
                {
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> GetBlob(string blobName)
        {
            try
            {
                var blobClient = _container.GetBlobClient(blobName);
                if (blobClient.Exists())
                {
                    Stream imageStream = new MemoryStream();
                    blobClient.DownloadTo(imageStream);

                    imageStream.Seek(0, SeekOrigin.Begin);
                    return File(imageStream, "image/jpeg");
                }
                else
                {
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"{ex.Message}");
            }
        }
    }
}
