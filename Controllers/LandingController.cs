using Azure.Storage.Files.Shares;
using Azure;
using Microsoft.AspNetCore.Mvc;
using CharityIce2.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace CharityIce2.Controllers
{
    public class LandingController : Controller
    {
        private readonly IConfiguration _configuration;

        public LandingController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            // Load user profile here
            // For example purposes, using session/email to fetch user
            var email = HttpContext.Session.GetString("LoggedInUserEmail");

            RegisterModel user = new RegisterModel(); 
            if (!string.IsNullOrEmpty(email))
            {
                string tableName = _configuration["TableName"];
                string connectionString = _configuration.GetConnectionString("AzureStorage");
                var tableClient = new TableClient(connectionString, tableName);

                try
                {
                    var entity = await tableClient.GetEntityAsync<TableEntity>(email, email);


                    user.FullName = entity.Value.ContainsKey("FullName") ? entity.Value.GetString("FullName") : "";
                    user.Email = entity.Value.ContainsKey("Email") ? entity.Value.GetString("Email") : "";
                    user.Password = entity.Value.ContainsKey("Password") ? entity.Value.GetString("Password") : "";

                    if (entity.Value.ContainsKey("ProfilePicture"))
                    {
                        user.ProfilePicturePath = entity.Value.GetString("ProfilePicture");
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    user = new RegisterModel(); // user not found
                }
            }

            return View(user ?? new RegisterModel());
        }


        [HttpPost]
        public async Task<IActionResult> UpdateProfile(RegisterModel model)
        {
            // Get the logged-in user's email from session
            var email = HttpContext.Session.GetString("LoggedInUserEmail");
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.ProfileMessage = "Unable to determine user email.";
                return View("Index", model);
            }

            string tableName = _configuration["TableName"];
            string connectionString = _configuration.GetConnectionString("AzureStorage");
            var tableClient = new TableClient(connectionString, tableName);

            // Fetch existing entity using session email
            TableEntity entity;
            try
            {
                var existingEntity = await tableClient.GetEntityAsync<TableEntity>("User", email);
                entity = existingEntity.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // User not found, create new entity
                entity = new TableEntity("User", email);
            }

            // Update entity properties
            entity["FullName"] = model.FullName;
            entity["Password"] = model.Password; // You may hash passwords in production

            // Handle profile picture upload
            if (model.ProfilePicture != null && model.ProfilePicture.Length > 0)
            {
                string containerName = _configuration["ContainerName"];
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();

                string fileName = Guid.NewGuid() + Path.GetExtension(model.ProfilePicture.FileName);
                var blobClient = containerClient.GetBlobClient(fileName);
                await blobClient.UploadAsync(model.ProfilePicture.OpenReadStream(), overwrite: true);

                string fileUrl = blobClient.Uri.ToString();
                entity["ProfilePictureFileName"] = fileName;
                model.ProfilePicturePath = fileUrl; // so view shows updated picture
            }
            else if (entity.ContainsKey("ProfilePictureFileName"))
            {
                // Keep existing profile picture
                var containerName = _configuration["ContainerName"];
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(entity.GetString("ProfilePictureFileName"));
                model.ProfilePicturePath = blobClient.Uri.ToString();
            }

            // Upsert entity
            await tableClient.UpsertEntityAsync(entity);

            ViewBag.ProfileMessage = "Profile updated successfully!";
            return View("Index", model);
        }





        [HttpPost]
        public async Task<IActionResult> UploadCharityDocument(IFormFile charityDocument)
        {
            // Prepare model for returning to view
            var email = HttpContext.Session.GetString("LoggedInUserEmail");
            RegisterModel user = new RegisterModel();

            if (!string.IsNullOrEmpty(email))
            {
                string tableName = _configuration["TableName"];
                string connectionString = _configuration.GetConnectionString("AzureStorage");
                var tableClient = new TableClient(connectionString, tableName);

                try
                {
                    var entity = await tableClient.GetEntityAsync<TableEntity>("User", email);

                    user.FullName = entity.Value.ContainsKey("FullName") ? entity.Value.GetString("FullName") : "";
                    user.Email = entity.Value.ContainsKey("Email") ? entity.Value.GetString("Email") : "";
                    user.Password = entity.Value.ContainsKey("Password") ? entity.Value.GetString("Password") : "";

                    if (entity.Value.ContainsKey("ProfilePictureFileName"))
                    {
                        var blobServiceClient = new BlobServiceClient(connectionString);
                        var containerClient = blobServiceClient.GetBlobContainerClient(_configuration["ContainerName"]);
                        var blobClient = containerClient.GetBlobClient(entity.Value.GetString("ProfilePictureFileName"));
                        user.ProfilePicturePath = blobClient.Uri.ToString();
                    }
                }
                catch
                {
                    // ignore, use empty model
                }
            }

            if (charityDocument == null || charityDocument.Length == 0)
            {
                ViewBag.Message = "No file selected!";
                return View("Index", user);
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("AzureStorage");
                string shareName = _configuration["FileShareName"];

                ShareClient share = new ShareClient(connectionString, shareName);
                await share.CreateIfNotExistsAsync();

                ShareDirectoryClient directory = share.GetRootDirectoryClient();
                ShareFileClient file = directory.GetFileClient(charityDocument.FileName);

                using (var stream = charityDocument.OpenReadStream())
                {
                    await file.CreateAsync(stream.Length);
                    await file.UploadRangeAsync(new HttpRange(0, stream.Length), stream);
                }

                ViewBag.Message = "File uploaded successfully to Azure!";
                ViewBag.FileUrl = file.Uri.ToString(); // optional link to uploaded file
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error uploading file: " + ex.Message;
            }

            // Always pass a valid model to the view
            return View("Index", user);
        }
    }
}
