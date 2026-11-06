using System.Globalization;
using System.Text;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Exports
{
    /// <summary>
    /// Builds the CSV file of a list of transactions, meant to open in Excel with Argentine regional settings:
    /// columns separated by ";", decimal comma, UTF-8 with a byte order mark so the accents show up right.
    /// </summary>
    public static class TransactionCsv
    {
        private const char Separator = ';';
        private const string NewLine = "\r\n";

        private static readonly string[] Header =
            { "Fecha", "Tipo", "Categoría", "Nota", "Monto", "Moneda", "Cotización", "Monto en ARS" };

        public static byte[] Build(IEnumerable<TransactionResponse> transactions)
        {
            var text = new StringBuilder();
            text.Append(string.Join(Separator, Header)).Append(NewLine);

            foreach (var t in transactions)
            {
                var cells = new[]
                {
                    t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    t.Type == TransactionType.Expense ? "Gasto" : "Ingreso",
                    Text(t.CategoryName),
                    Text(t.Note),
                    Number(t.Amount, "0.00"),
                    t.Currency.ToString(),
                    Number(t.ExchangeRate, "0.00##"),
                    Number(t.ArsAmount, "0.00")
                };
                text.Append(string.Join(Separator, cells)).Append(NewLine);
            }

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return encoding.GetPreamble().Concat(encoding.GetBytes(text.ToString())).ToArray();
        }

        /// <summary>Decimal comma, no thousands separator, independent of the server's culture (containers often have none).</summary>
        private static string Number(decimal? value, string format)
        {
            return value == null ? string.Empty : value.Value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>
        /// Free text made safe for a CSV cell: quoted when it holds the separator, quotes or line breaks, and with a leading
        /// apostrophe when a spreadsheet would read it as a formula (a note like "=HYPERLINK(...)" must stay text).
        /// </summary>
        private static string Text(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                value = "'" + value;
            }

            var needsQuotes = value.Contains(Separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
