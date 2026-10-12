using Api.Validators.Validators.Product;
using FluentValidation;

namespace Tests.Validators
{
    public class ValidatorInstantiationTests
    {
        public static IEnumerable<Type> GetValidatorTypes()
        {
            var assemblies = new[]
            {
                typeof(EditProductValidator).Assembly,
            };

            return assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract
                         && typeof(IValidator).IsAssignableFrom(t)
                         && t.GetConstructor(Type.EmptyTypes) != null);
        }

        [Test]
        [MethodDataSource(nameof(GetValidatorTypes))]
        public async Task Validator_ShouldInstantiateSuccessfully(Type validatorType)
        {
            var instance = Activator.CreateInstance(validatorType);
            await Assert.That(instance).IsNotNull();
        }
    }
}
