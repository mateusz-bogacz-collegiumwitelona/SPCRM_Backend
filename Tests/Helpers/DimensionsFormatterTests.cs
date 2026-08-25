using Domain.Enum;
using Services.Helpers;

namespace Tests.Helpers
{
    public class DimensionsFormatterTests
    {
        [Test]
        [Arguments(ProductCategoryEnum.Pipe, 500, 50, 0, 60000, "fi 50 x 5 (L=6000)")]
        [Arguments(ProductCategoryEnum.Pipe, null, 40, 400, 60000, "fi 40 x 4 (L=6000)")]
        [Arguments(ProductCategoryEnum.Bar, 120, 0, 0, 30000, "fi 12 (L=3000)")]
        [Arguments(ProductCategoryEnum.Bar, null, 200, 200, 30000, "20 x 20 (L=3000)")]
        [Arguments(ProductCategoryEnum.Profile, null, 30, 600, 60000, "60 x 3 (L=6000)")]
        [Arguments(ProductCategoryEnum.Beam, null, 50, 1000, 120000, "100 x 5 (L=12000)")]
        [Arguments(ProductCategoryEnum.Wire, 40, 0, 0, 0, "fi 4")]
        [Arguments(ProductCategoryEnum.Wire, null, 25, 0, 0, "fi 2.5")]
        [Arguments(ProductCategoryEnum.Sheet, null, 20, 10000, 20000, "2 x 1000 x 2000")]
        [Arguments(ProductCategoryEnum.Mesh, null, 60, 21500, 50000, "6 x 2150 x 5000")]
        [Arguments(ProductCategoryEnum.Fitting, 890, 0, 0, 1500, "fi 89 x 150")]
        [Arguments(ProductCategoryEnum.Other, null, 15, 500, 1200, "1.5 x 50 x 120")]
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
