using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Api.Transformers
{
    public class LoginRequestExamplesTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken
            )
        {
            if (context.Description.ActionDescriptor is ControllerActionDescriptor actionDescriptor &&
                (actionDescriptor.ActionName == "Login" || actionDescriptor.ActionName == "LoginAsync"))
            {
                if (operation.RequestBody != null &&
                    operation.RequestBody.Content.TryGetValue("application/json", out var content))
                {
                    content.Examples = new Dictionary<string, IOpenApiExample>
                    {
                        ["Admin"] = new OpenApiExample
                        {
                            Summary = "Login for Admin role",
                            Value = new JsonObject
                            {
                                ["name"] = "admin@example.pl",
                                ["password"] = "Admin123!"
                            }
                        },
                        ["Manager"] = new OpenApiExample
                        {
                            Summary = "Login for Manager role",
                            Value = new JsonObject
                            {
                                ["name"] = "manager@example.pl",
                                ["password"] = "Manager123!"
                            }
                        },
                        ["User"] = new OpenApiExample
                        {
                            Summary = "Login for User role",
                            Value = new JsonObject
                            {
                                ["name"] = "user@example.pl",
                                ["password"] = "User123!"
                            }
                        }
                    };
                }
            }

            return Task.CompletedTask;
        }
    }
}