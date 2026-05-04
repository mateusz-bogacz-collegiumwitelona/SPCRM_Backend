using System;
using System.Collections.Generic;
using System.Text;

namespace DTO.Domain
{
    public record EmailDomain(string To, string Subject, string Body);
}
