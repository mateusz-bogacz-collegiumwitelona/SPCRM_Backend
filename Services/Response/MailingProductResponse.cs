namespace Services.Response
{
    public record MailingProductResponse
    {
        public required Guid ProductId { get; set; }
        public required string Name { get; set; }
        public required string Dimmension { get; set; }
        public required int StockQuantity { get; set; }
        public required long StockPrice { get; set; }
    }
}
