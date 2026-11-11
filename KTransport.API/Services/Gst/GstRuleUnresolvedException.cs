using System;
using KTransport.API.DTOs.Gst;

namespace KTransport.API.Services.Gst;

public class GstRuleUnresolvedException : Exception
{
    public TaxCalculationInputDto Input { get; }

    public GstRuleUnresolvedException(TaxCalculationInputDto input)
        : base("No active GST rule matches the given input tuple on the transaction date. Platform admin must seed the rule before this transaction can be priced.")
    {
        Input = input;
    }
}
