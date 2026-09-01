using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Command.Product
{
    public record SearchProductAutocompleteCommand
    {
        public string? Query { get; init; }
        public int Limit { get; init; } = 20;
    }
}
