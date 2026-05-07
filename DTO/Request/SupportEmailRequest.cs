using System.ComponentModel.DataAnnotations;

namespace DTO.Request
{
    public class SupportEmailRequest
    {
        public string Email { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
    }
}
