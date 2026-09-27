using Microsoft.AspNetCore.Authorization;
using FarmMonitoring.Api.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FarmMonitoring.Api.Filters;

public sealed class BearerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
            return;

        if (metadata.OfType<IAuthorizeData>().Any(x => x.AuthenticationSchemes == DeviceAuthenticationHandler.SchemeName))
        {
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("DeviceKey", context.Document)] = [],
                [new OpenApiSecuritySchemeReference("GatewayCode", context.Document)] = []
            }];
            return;
        }
        operation.Security = [new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        }];
    }
}
