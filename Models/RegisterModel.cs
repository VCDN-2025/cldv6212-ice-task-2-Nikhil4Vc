using System.ComponentModel.DataAnnotations;

namespace CharityIce2.Models
{
    public class RegisterModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email address")]
        public string Email { get; set; }

        // Make Password optional when updating
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public IFormFile ProfilePicture { get; set; }

        public string ProfilePicturePath { get; set; }

    }
}
