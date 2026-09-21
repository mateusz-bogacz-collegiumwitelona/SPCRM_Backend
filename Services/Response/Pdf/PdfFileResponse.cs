namespace Services.Response.Pdf
{
    public record PdfFileResponse
    {
        public required byte[] FileContents { get; init; }
        public required string ContentType { get; init; }
        public required string FileName { get; init; }
    }
}
