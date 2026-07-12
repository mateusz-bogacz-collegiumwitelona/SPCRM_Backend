using Services.Helpers;

namespace Tests.Helpers
{
    public class DimensionsFormatterTests
    {
        [Test]
        [Arguments("Rury stalowe", "Rura", 50, 5, 0, 6000, "fi 50 x 5 (L=6000)")]
        [Arguments("Rury czarne", "Rura", null, 4, 40, 6000, "fi 40 x 4 (L=6000)")]
        [Arguments("Stal", "Pręt okrągły", 12, 0, 0, 3000, "fi 12 (L=3000)")]
        [Arguments("Stal", "Pręt kwadratowy", null, 20, 20, 3000, "20 x 20 (L=3000)")]
        [Arguments("Konstrukcje", "Profil zamknięty", null, 3, 60, 6000, "60 x 3 (L=6000)")]
        [Arguments("Konstrukcje", "Kątownik gorącowalcowany", null, 5, 50, 6000, "50 x 5 (L=6000)")]
        [Arguments("Blachy", "Blacha płaska", null, 2, 1000, 2000, "2 x 1000 x 2000")]
        public async Task Format_ReturnsCorrectDimensionsString(
            string category,
            string type,
            int? diameter,
            int thickness,
            int width,
            int length,
            string expectedResult)
        {
            // Act
            var result = DimensionsFormatter.Format(category, type, diameter, thickness, width, length);

            // Assert
            await Assert.That(result).IsEqualTo(expectedResult);
        }
    }
}
