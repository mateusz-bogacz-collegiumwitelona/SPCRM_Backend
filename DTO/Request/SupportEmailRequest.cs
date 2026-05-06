using System.ComponentModel.DataAnnotations;

namespace DTO.Request
{
    public class SupportEmailRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [Length(5, 100, ErrorMessage = "Title must be between 5 and 100 characters")]
        public string Title { get; set; }


        [Required(ErrorMessage = "Message is required")]
        [Length(5, 5000, ErrorMessage = "Message must be between 5 and 255 characters")]
        public string Message { get; set; }
    }
}
