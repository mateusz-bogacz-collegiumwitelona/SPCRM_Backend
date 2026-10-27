using Api.Controllers.Base;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Tests.Controllers
{
    public class BaseControllerTests
    {
        private TestController _sut = null!;

        private class TestPayload
        {
            public int OrderId { get; set; }
            public decimal Total { get; set; }
        }

        private class TestController : BaseController
        {
            public Guid ExposedCurrentUserId => CurrentUserId;

            public IActionResult ExecuteHandleResult<T>(Result<T> result) => HandleResult(result);

            public IActionResult ExecuteHandleResult(Result result) => HandleResult(result);
        }

        [Before(Test)]
        public void Setup()
        {
            _sut = new TestController
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        #region CurrentUserId 

        [Test]
        public async Task CurrentUserId_WhenClaimExists_ReturnsParsedGuid()
        {
            // Arrange
            var expectedUserId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, expectedUserId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

            // Act
            var actualUserId = _sut.ExposedCurrentUserId;

            // Assert
            await Assert.That(actualUserId).IsEqualTo(expectedUserId);
        }

        [Test]
        public async Task CurrentUserId_WhenClaimIsMissing_ThrowsArgumentNullException()
        {
            // Arrange
            _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act & Assert
            await Assert.That(() => _sut.ExposedCurrentUserId).Throws<ArgumentNullException>();
        }
        #endregion


        #region HandleResult(Result) 

        [Test]
        public async Task HandleResult_WhenResultIsNull_Returns500WithMessage()
        {
            // Act
            var actionResult = _sut.ExecuteHandleResult((Result)null!);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);

            var result = objectResult.Value as Result;
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.IsSuccess).IsFalse();
            await Assert.That(result.Message).IsEqualTo("Result cannot be null.");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InternalError);
        }

        [Test]
        public async Task HandleResult_WhenSuccess_ReturnsStatusCodeAndPayload()
        {
            // Arrange
            var result = Result.Success("Operation successful", StatusCodes.Status200OK);

            // Act
            var actionResult = _sut.ExecuteHandleResult(result);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var actualResult = objectResult.Value as Result;
            await Assert.That(actualResult).IsNotNull();
            await Assert.That(actualResult!.IsSuccess).IsTrue();
            await Assert.That(actualResult.Message).IsEqualTo("Operation successful");
        }

        [Test]
        public async Task HandleResult_WhenFailure_ReturnsErrorStatusCodeErrorCodeAndErrors()
        {
            // Arrange
            var errors = new List<string> { "Field is required", "Invalid format" };
            var result = Result.Failure("Validation failed", "VAL_001", StatusCodes.Status400BadRequest, errors);

            // Act
            var actionResult = _sut.ExecuteHandleResult(result);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);

            var actualResult = objectResult.Value as Result;
            await Assert.That(actualResult).IsNotNull();
            await Assert.That(actualResult!.IsSuccess).IsFalse();
            await Assert.That(actualResult.Message).IsEqualTo("Validation failed");
            await Assert.That(actualResult.ErrorCode).IsEqualTo("VAL_001");
            await Assert.That(actualResult.Errors).IsNotNull();
            await Assert.That(actualResult.Errors!.Count).IsEqualTo(2);
            await Assert.That(actualResult.Errors).Contains("Field is required");
        }
        #endregion

        #region HandleResult<T>(Result<T>) 

        [Test]
        public async Task HandleResultGeneric_WhenSuccess_ReturnsDataPayload()
        {
            // Arrange
            var payload = new TestPayload { OrderId = 123, Total = 99.99m };
            var result = Result<TestPayload>.Success("Data retrieved", StatusCodes.Status200OK, payload);

            // Act
            var actionResult = _sut.ExecuteHandleResult(result);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var actualResult = objectResult.Value as Result<TestPayload>;
            await Assert.That(actualResult).IsNotNull();
            await Assert.That(actualResult!.IsSuccess).IsTrue();
            await Assert.That(actualResult.Data).IsNotNull();
            await Assert.That(actualResult.Data!.OrderId).IsEqualTo(123);
            await Assert.That(actualResult.Data.Total).IsEqualTo(99.99m);
        }

        [Test]
        public async Task HandleResultGeneric_WhenFailure_ReturnsErrorsAndNullData()
        {
            // Arrange
            var result = Result<string>.Failure("Resource not found", "NOT_FOUND", StatusCodes.Status404NotFound);

            // Act
            var actionResult = _sut.ExecuteHandleResult(result);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);

            var actualResult = objectResult.Value as Result<string>;
            await Assert.That(actualResult).IsNotNull();
            await Assert.That(actualResult!.IsSuccess).IsFalse();
            await Assert.That(actualResult.ErrorCode).IsEqualTo("NOT_FOUND");
            await Assert.That(actualResult.Data).IsNull();
        }
        #endregion
    }
}
