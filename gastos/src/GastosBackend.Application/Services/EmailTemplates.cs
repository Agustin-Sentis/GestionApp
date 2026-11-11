using System.Globalization;
using System.Net;
using GastosBackend.Application.dtos;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Services
{
    /// <summary>
    /// The mails the app sends, in Spanish, as plain text and as simple HTML. Everything that comes from the user (a category
    /// name) is HTML-encoded in the HTML part, and the amounts are formatted by hand: the production image may have no
    /// Argentine culture data, and the mail must read the same everywhere.
    /// </summary>
    public static class EmailTemplates
    {
        public static EmailMessage Confirmation(string to, string confirmUrl)
        {
            const string subject = "Confirmá tu correo en DualBalance";
            var text =
                "Pediste recibir avisos de presupuesto por correo.\n\n" +
                "Para activarlos, confirmá que esta dirección es tuya con este enlace (vale 24 horas):\n" +
                $"{confirmUrl}\n\n" +
                "Si no fuiste vos, ignorá este mensaje: no se activa nada.";

            var html = Layout(
                $"<p>Pediste recibir avisos de presupuesto por correo.</p>" +
                $"<p>Para activarlos, confirmá que esta dirección es tuya (el enlace vale 24 horas):</p>" +
                $"<p><a href=\"{Encode(confirmUrl)}\">Confirmar mi correo</a></p>" +
                $"<p style=\"color:#5b6472\">Si no fuiste vos, ignorá este mensaje: no se activa nada.</p>");

            return new EmailMessage(to, subject, text, html);
        }

        public static EmailMessage PasswordReset(string to, string resetUrl)
        {
            const string subject = "Restablecé tu contraseña en DualBalance";
            var text =
                "Pediste restablecer la contraseña de tu cuenta.\n\n" +
                "Para elegir una nueva, abrí este enlace (vale 1 hora y se puede usar una sola vez):\n" +
                $"{resetUrl}\n\n" +
                "Si no fuiste vos, ignorá este mensaje: tu contraseña no cambia.";

            var html = Layout(
                "<p>Pediste restablecer la contraseña de tu cuenta.</p>" +
                "<p>Para elegir una nueva, abrí este enlace (vale 1 hora y se puede usar una sola vez):</p>" +
                $"<p><a href=\"{Encode(resetUrl)}\">Elegir una contraseña nueva</a></p>" +
                "<p style=\"color:#5b6472\">Si no fuiste vos, ignorá este mensaje: tu contraseña no cambia.</p>");

            return new EmailMessage(to, subject, text, html);
        }

        /// <param name="appUrl">The public address of the app: the link to recover the account is built from it.</param>
        public static EmailMessage PasswordChanged(string to, string appUrl)
        {
            const string subject = "Tu contraseña de DualBalance fue cambiada";
            var recoverUrl = $"{appUrl.TrimEnd('/')}/login?modo=recuperar";
            var text =
                "La contraseña de tu cuenta se acaba de cambiar, y se cerraron todas las sesiones que estaban abiertas.\n\n" +
                "Si fuiste vos, no tenés que hacer nada.\n\n" +
                "Si no fuiste vos, alguien tiene acceso a tu correo. Recuperá la cuenta desde este enlace y revisá también la seguridad de tu correo:\n" +
                recoverUrl;

            var html = Layout(
                "<p>La contraseña de tu cuenta se acaba de cambiar, y se cerraron todas las sesiones que estaban abiertas.</p>" +
                "<p>Si fuiste vos, no tenés que hacer nada.</p>" +
                "<p><strong>Si no fuiste vos</strong>, alguien tiene acceso a tu correo. Recuperá la cuenta y revisá también la seguridad de tu correo:</p>" +
                $"<p><a href=\"{Encode(recoverUrl)}\">Recuperar mi cuenta</a></p>");

            return new EmailMessage(to, subject, text, html);
        }

        public static EmailMessage BudgetAlert(
            string to, string categoryName, decimal spent, decimal budget, decimal usedPercent, BudgetStatus level, string appUrl)
        {
            var exceeded = level == BudgetStatus.Exceeded;
            var subject = exceeded
                ? $"Superaste el presupuesto de {categoryName}"
                : $"Cerca del límite en {categoryName}";

            var summary = $"Llevás gastado {Pesos(spent)} de {Pesos(budget)} en {{0}} este mes ({Percent(usedPercent)}).";
            var verdict = exceeded
                ? $"Te pasaste por {Pesos(spent - budget)}."
                : "Estás cerca del límite.";
            const string optOut = "Para dejar de recibir estos avisos, desactivalos en la tarjeta Presupuestos de la app.";

            var text = string.Join("\n\n", string.Format(summary, categoryName), verdict, $"Ver mi resumen: {appUrl}", optOut);

            var html = Layout(
                $"<p>{string.Format(summary, $"<strong>{Encode(categoryName)}</strong>")}</p>" +
                $"<p><strong>{Encode(verdict)}</strong></p>" +
                $"<p><a href=\"{Encode(appUrl)}\">Ver mi resumen</a></p>" +
                $"<p style=\"color:#5b6472\">{Encode(optOut)}</p>");

            return new EmailMessage(to, subject, text, html);
        }

        private static string Layout(string body) =>
            "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:1.5;color:#22262e;max-width:32rem\">" +
            body +
            "<p style=\"color:#5b6472;font-size:13px\">DualBalance</p></div>";

        private static string Encode(string value) => WebUtility.HtmlEncode(value);

        /// <summary>"$ 1.234" for whole pesos, "$ 1.234,50" otherwise: dots for thousands, comma for decimals.</summary>
        private static string Pesos(decimal amount)
        {
            var whole = amount == decimal.Truncate(amount);
            return "$ " + SwapSeparators(amount.ToString(whole ? "#,##0" : "#,##0.00", CultureInfo.InvariantCulture));
        }

        /// <summary>"83,3%" (one decimal at most).</summary>
        private static string Percent(decimal percent) =>
            SwapSeparators(percent.ToString("0.#", CultureInfo.InvariantCulture)) + "%";

        private static string SwapSeparators(string invariant) =>
            new string(invariant.Select(c => c == ',' ? '.' : c == '.' ? ',' : c).ToArray());
    }
}
