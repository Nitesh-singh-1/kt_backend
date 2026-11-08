using System;

namespace KTransport.API.Common
{
    /// <summary>
    /// A business-rule violation in the accounting domain that maps to a specific HTTP status
    /// (e.g. 422 unbalanced voucher / posting into a closed period, 409 duplicate code).
    /// Controllers catch this and return <c>{ success = false, message }</c> with <see cref="StatusCode"/>.
    /// </summary>
    public class AccountingException : Exception
    {
        public int StatusCode { get; }

        public AccountingException(string message, int statusCode = 422) : base(message)
        {
            StatusCode = statusCode;
        }

        public static AccountingException Unprocessable(string message) => new(message, 422);
        public static AccountingException Conflict(string message) => new(message, 409);
    }
}
