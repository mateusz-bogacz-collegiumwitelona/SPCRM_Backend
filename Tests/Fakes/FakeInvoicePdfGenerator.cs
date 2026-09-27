using Domain.Models;
using Infrastructure.Pdf.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Tests.Fakes
{
    public class FakeInvoicePdfGenerator : IInvoicePdfGenerator
    {
        public Invoice? LastInvoice { get; private set; }
        public string? LastLanguage { get; private set; }
        public int GenerateCallCount { get; private set; }
        public byte[] FakePdfBytes { get; set; } = new byte[] { 1, 2, 3, 4 };

        public byte[] GenerateInvoicePdf(Invoice invoice, string language)
        {
            GenerateCallCount++;
            LastInvoice = invoice;
            LastLanguage = language;
            return FakePdfBytes;
        }
    }
}
