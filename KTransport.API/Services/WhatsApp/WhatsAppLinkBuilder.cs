using System;
using System.Linq;

namespace KTransport.API.Services
{
    /// <summary>
    /// Builds free WhatsApp "click-to-chat" (wa.me) deep links. No WhatsApp
    /// Business API, no SMS gateway, no per-message cost: the link opens
    /// WhatsApp with the recipient + message pre-filled and the operator taps
    /// send from their own WhatsApp. Pure string logic — unit tested.
    /// </summary>
    public static class WhatsAppLinkBuilder
    {
        /// <summary>
        /// Normalises an Indian-style mobile into the digits-only international
        /// form wa.me expects (country code + number, no '+', spaces or dashes).
        /// Returns null when the input can't be a dialable number.
        /// </summary>
        public static string? NormalizeMobile(string? raw, string defaultCountryCode = "91")
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return null;

            // Drop an international access prefix ("00" -> drop).
            if (digits.StartsWith("00")) digits = digits.Substring(2);

            // 10-digit local number -> prepend the default country code.
            if (digits.Length == 10) return defaultCountryCode + digits;

            // Trunk-prefixed local "0XXXXXXXXXX" -> strip 0, prepend country code.
            if (digits.Length == 11 && digits.StartsWith("0"))
                return defaultCountryCode + digits.Substring(1);

            // Already carries a country code (E.164 is up to 15 digits).
            if (digits.Length >= 11 && digits.Length <= 15) return digits;

            // Too short / implausible.
            return null;
        }

        /// <summary>
        /// Builds a wa.me share URL. When the mobile normalises, the chat opens
        /// straight to that contact; otherwise it opens the contact picker so the
        /// operator can still send the same pre-filled message.
        /// </summary>
        public static string BuildShareUrl(string? mobile, string message)
        {
            var text = Uri.EscapeDataString(message ?? string.Empty);
            var normalized = NormalizeMobile(mobile);
            return normalized is null
                ? $"https://wa.me/?text={text}"
                : $"https://wa.me/{normalized}?text={text}";
        }
    }
}
