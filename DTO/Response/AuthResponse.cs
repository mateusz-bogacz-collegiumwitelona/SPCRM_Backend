using System;
using System.Collections.Generic;
using System.Text;

namespace DTO.Response
{
    public class AuthResponse
    {
        public required string Token { get; set; }
        public Guid UserId { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public IList<string> Roles { get; set; }
    }
}
