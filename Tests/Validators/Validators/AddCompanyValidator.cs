using Api.Request.Company;
using Api.Validators.Validators.Company;
using Domain.Constants;
using Domain.Enum;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class AddCompanyValidatorTest
    {
        private readonly AddCompanyValidator _validator = new();

        // Metoda pomocnicza ułatwiająca tworzenie poprawnego pod kątem kompilatora obiektu adresu
        private static AddCompanyAdressesRequest CreateAddress(string type) => new()
        {
            Type = type,
            Street = "Test Street",
            City = "Test City",
            ZipCode = "00-000",
            Longitude = 0f,
            Latitude = 0f
        };

        // ─── Addresses (Headquarters Rule) ───────────────────────────────────

        [Test]
        public async Task Validate_WhenAddressesHasExactlyOneHeadquarters_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddCompanyRequest
            {
                Name = "Company Name",
                NIP = "1234567890",
                Addresses = new List<AddCompanyAdressesRequest>
                {
                    CreateAddress(nameof(AddressTypeEnum.Headquarters)),
                    CreateAddress("Branch")
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Addresses);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenAddressesHasHeadquartersInLowerCase_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddCompanyRequest
            {
                Name = "Company Name",
                NIP = "1234567890",
                Addresses = new List<AddCompanyAdressesRequest>
                {
                    CreateAddress("headquarters")
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Addresses);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenAddressesHasNoHeadquarters_ShouldHaveHeadquartersAddressRequiredErrorCode()
        {
            // Arrange
            var request = new AddCompanyRequest
            {
                Name = "Company Name",
                NIP = "1234567890",
                Addresses = new List<AddCompanyAdressesRequest>
                {
                    CreateAddress("Branch"),
                    CreateAddress("Warehouse")
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Addresses)
                  .WithErrorCode(ErrorCodes.HeadquartersAddressRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenAddressesHasMultipleHeadquarters_ShouldHaveHeadquartersAddressRequiredErrorCode()
        {
            // Arrange
            var request = new AddCompanyRequest
            {
                Name = "Company Name",
                NIP = "1234567890",
                Addresses = new List<AddCompanyAdressesRequest>
                {
                    CreateAddress(nameof(AddressTypeEnum.Headquarters)),
                    CreateAddress(nameof(AddressTypeEnum.Headquarters))
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Addresses)
                  .WithErrorCode(ErrorCodes.HeadquartersAddressRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenAddressesIsEmpty_ShouldHaveBothErrorCodes()
        {
            // Arrange
            var request = new AddCompanyRequest
            {
                Name = "Company Name",
                NIP = "1234567890",
                Addresses = new List<AddCompanyAdressesRequest>()
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Addresses)
                  .WithErrorCode(ErrorCodes.AddressRequired);

            result.ShouldHaveValidationErrorFor(x => x.Addresses)
                  .WithErrorCode(ErrorCodes.HeadquartersAddressRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenAddressesIsNull_ShouldHaveBothErrorCodes()
        {
            // Arrange
            var request = new AddCompanyRequest
            {
                Name = "Company Name",
                NIP = "1234567890",
                Addresses = null!
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Addresses)
                  .WithErrorCode(ErrorCodes.AddressRequired);

            result.ShouldHaveValidationErrorFor(x => x.Addresses)
                  .WithErrorCode(ErrorCodes.HeadquartersAddressRequired);
            await Task.CompletedTask;
        }
    }
}
