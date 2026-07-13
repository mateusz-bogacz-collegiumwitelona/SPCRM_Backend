using Domain.Enum;
using Services.Helpers;

namespace Tests.Helpers
{
    public class DimensionsFormatterTests
    {
        [Test]
        [Arguments(ProductCategoryEnum.Pipe, 50, 5, 0, 6000, "fi 50 x 5 (L=6000)")]
        [Arguments(ProductCategoryEnum.Pipe, null, 4, 40, 6000, "fi 40 x 4 (L=6000)")]
        [Arguments(ProductCategoryEnum.Bar, 12, 0, 0, 3000, "fi 12 (L=3000)")]
        [Arguments(ProductCategoryEnum.Bar, null, 20, 20, 3000, "20 x 20 (L=3000)")]
        [Arguments(ProductCategoryEnum.Profile, null, 3, 60, 6000, "60 x 3 (L=6000)")]
        [Arguments(ProductCategoryEnum.Profile, null, 5, 50, 6000, "50 x 5 (L=6000)")]
        [Arguments(ProductCategoryEnum.Standard, null, 2, 1000, 2000, "2 x 1000 x 2000")]
        public async Task Format_ReturnsCorrectDimensionsString(
            ProductCategoryEnum category,
            int? diameter,
            int thickness,
            int width,
            int length,
            string expectedResult)
        {
            // Act
            var result = DimensionsFormatter.Format(category, diameter, thickness, width, length);

            // Assert
            await Assert.That(result).IsEqualTo(expectedResult);
        }
    }
}
