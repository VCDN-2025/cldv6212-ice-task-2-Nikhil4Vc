using Azure.Data.Tables;
using Azure.Storage.Blobs;
using CharityIce2.Models;
using Microsoft.AspNetCore.Mvc;

namespace CharityIce2.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly string _tableName;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("AzureStorage");
            _containerName = configuration.GetValue<string>("ContainerName");
            _tableName = configuration.GetValue<string>("TableName");
        }

        // -------------------- REGISTER --------------------
        // GET: Register
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterModel());
        }

        // POST: Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            // Only manually validate profile picture
            if (model.ProfilePicture == null)
            {
                ModelState.AddModelError("", "Please upload a profile picture.");
                return View(model);
            }

            try
            {
                // Upload profile picture to Blob Storage
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync();

                var profileFileName = Guid.NewGuid() + Path.GetExtension(model.ProfilePicture.FileName);
                var blobClient = containerClient.GetBlobClient(profileFileName);
                await blobClient.UploadAsync(model.ProfilePicture.OpenReadStream(), overwrite: true);

                // Save user data to Table Storage
                var tableClient = new TableClient(_connectionString, _tableName);
                await tableClient.CreateIfNotExistsAsync();

                var userEntity = new TableEntity("User", model.Email)
        {
            { "FullName", model.FullName },
            { "Password", model.Password },
            { "ProfilePictureFileName", profileFileName }
        };

                await tableClient.AddEntityAsync(userEntity);

                // ✅ Successful registration: redirect to Landing page
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Registration failed: " + ex.Message);
                return View(model);
            }
        }

        // -------------------- LOGIN --------------------
        // GET: Login
        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginModel());
        }

        // POST: Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var tableClient = new TableClient(_connectionString, _tableName);
                await tableClient.CreateIfNotExistsAsync();

                var response = await tableClient.GetEntityIfExistsAsync<TableEntity>("User", model.Email);

                if (!response.HasValue)
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }

                var userEntity = response.Value;
                string storedPassword = userEntity.GetString("Password");

                if (storedPassword != model.Password)
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }

                // ✅ Successful login: set session
                HttpContext.Session.SetString("LoggedInUserEmail", model.Email);

                // Redirect to Landing page
                return RedirectToAction("Index", "Landing");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Login failed: " + ex.Message);
                return View(model);
            }
        }

        // -------------------- LOGOUT --------------------
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("LoggedInUserEmail");
            return RedirectToAction("Login");
        }

    }
}
