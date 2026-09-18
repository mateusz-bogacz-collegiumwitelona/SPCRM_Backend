namespace Services.Response.Invoice
{
    public record InvoicePdfFileResponse
    {
        public required byte[] FileContents { get; init; }
        public required string ContentType { get; init; }
        public required string FileName { get; init; }
    }
}
