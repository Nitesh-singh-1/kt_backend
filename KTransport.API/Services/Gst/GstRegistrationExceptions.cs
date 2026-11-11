using System;

namespace KTransport.API.Services.Gst;

/// <summary>
/// Thrown by <see cref="GstRegistrationService"/> and
/// <see cref="GstRegistrationProfileService"/> to signal a domain-rule
/// violation that controllers map to the task's error envelope.
/// </summary>
public class GstRegistrationException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public GstRegistrationException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}
