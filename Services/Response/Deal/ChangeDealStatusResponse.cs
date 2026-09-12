namespace Services.Response.Deal
{
    public record ChangeDealStatusResponse
    {
        public required string Status { get; set; }
        public string? SentToEmail { get; set; }
    }
}
