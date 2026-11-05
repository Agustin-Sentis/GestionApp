using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GastosBackend.Presentation.OpenApi
{
    /// <summary>Declara el esquema Bearer en el documento OpenAPI para que Scalar muestre el botón de autenticación.</summary>
    public class BearerSecurityTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Ingresá el token JWT. No hace falta escribir 'Bearer'."
            };

            document.Security ??= [];
            document.Security.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer", document), [] }
            });

            return Task.CompletedTask;
        }
    }
}
