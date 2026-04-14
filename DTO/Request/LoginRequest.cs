using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DTO.Request
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        public required string Name { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        public required string Password { get; set; }
    }
}
