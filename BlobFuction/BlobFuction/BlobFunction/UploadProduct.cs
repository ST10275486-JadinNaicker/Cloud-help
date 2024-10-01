using System.IO;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
}

public static class UploadProduct
{
    private static readonly string _blobServiceConnectionString = "DefaultEndpointsProtocol=https;AccountName=st10275496;AccountKey=CpfBmfw/u2CiDAGJGrNOYWedlAYXqrYgH2D+9lPjyacwFuTX+ZR7gv3DugtodgImsQQ2MbypK40f+AStDs84jQ==;EndpointSuffix=core.windows.net"; // Ensure to replace this
    private static readonly string _containerName = "multimedia";

    [Function("UploadProduct")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("UploadProduct");
        logger.LogInformation("Processing a product upload.");

        // Ensure that the request is multipart/form-data
        if (!req.ContentType.StartsWith("multipart/form-data"))
        {
            return new BadRequestObjectResult("Invalid content type.");
        }

        var formCollection = await req.ReadFormAsync();
        var productJson = formCollection["product"];
        var imageFile = formCollection.Files.GetFile("imageFile");

        if (string.IsNullOrEmpty(productJson) || imageFile == null)
        {
            return new BadRequestObjectResult("Product data or image file is missing.");
        }

        // Deserialize the product information
        var product = JsonSerializer.Deserialize<Product>(productJson);
        if (product == null)
        {
            return new BadRequestObjectResult("Invalid product data.");
        }

        // Upload the image to Blob Storage
        var imageUrl = await UploadImageToBlobAsync(imageFile);
        product.ImageUrl = imageUrl;

        // Save product metadata as JSON
        await SaveProductMetadataToBlobAsync(product);

        return new OkObjectResult("Product uploaded successfully.");
    }

    private static async Task<string> UploadImageToBlobAsync(IFormFile imageFile)
    {
        var blobServiceClient = new BlobServiceClient(_blobServiceConnectionString);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        await blobContainerClient.CreateIfNotExistsAsync();

        var blobClient = blobContainerClient.GetBlobClient(imageFile.FileName);
        using (var stream = imageFile.OpenReadStream())
        {
            await blobClient.UploadAsync(stream, true);
        }

        return blobClient.Uri.ToString();
    }

    private static async Task SaveProductMetadataToBlobAsync(Product product)
    {
        var blobServiceClient = new BlobServiceClient(_blobServiceConnectionString);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        await blobContainerClient.CreateIfNotExistsAsync();

        var metadataBlobClient = blobContainerClient.GetBlobClient($"{product.Name}.json");
        var metadataJson = JsonSerializer.Serialize(product);
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(metadataJson)))
        {
            await metadataBlobClient.UploadAsync(stream, true);
        }
    }
}
