using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GastosBackend.Presentation.Middlewares
{
    /// <summary>
    /// Turns the binding errors ASP.NET produces (an unparseable query value, a malformed JSON body...) into one Spanish message,
    /// so they come back as { "error": "..." } like every other error instead of an English ProblemDetails.
    /// </summary>
    public static class ModelStateErrorMessage
    {
        public const string BodyMessage = "El cuerpo de la solicitud no es válido.";

        public static string Build(ModelStateDictionary modelState)
        {
            var fields = new List<string>();
            var bodyIsInvalid = false;

            foreach (var (key, entry) in modelState)
            {
                if (entry.Errors.Count == 0)
                {
                    continue;
                }

                // JSON body errors use a path: "$" for malformed JSON, "$.currency" for a bad value. An empty key
                // means the whole body was unreadable.
                var field = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
                if (string.IsNullOrEmpty(field) || field == "$")
                {
                    bodyIsInvalid = true;
                }
                else if (field == "request")
                {
                    // The framework adds a "request" entry next to a field error ("the request field is required").
                    // It only matters when it is the only error, which the fallback below covers.
                    continue;
                }
                else if (!fields.Contains(field))
                {
                    fields.Add(field);
                }
            }

            var messages = fields.Select(f => $"El valor del campo '{f}' no es válido.").ToList();
            if (bodyIsInvalid || messages.Count == 0)
            {
                messages.Add(BodyMessage);
            }

            return string.Join(" ", messages);
        }
    }
}
