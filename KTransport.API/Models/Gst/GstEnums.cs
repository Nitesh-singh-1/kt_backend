using System.Text.Json.Serialization;

namespace KTransport.API.Models.Gst;

// All GST enums carry [JsonStringEnumConverter] so API wire format uses the
// name strings ("NotApplicable", "Fcm5NoItc", …) matching the OpenAPI contract
// in .agent/contracts/TASK-033-gst-registration.yaml. Without this, the
// default System.Text.Json serializer reads/writes them as integers and
// requests like {"gtaTaxScheme":"NotApplicable"} fail with a 400.

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GstServiceType
{
    GtaRoadTransport = 1,
    NonGtaRoadTransport = 2,
    OwnGoodsMovement = 3,
    Other = 99
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GtaTaxScheme
{
    NotApplicable = 0,
    Fcm5NoItc = 1,
    Fcm12WithItc = 2,
    Rcm = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecipientCategory
{
    RegisteredBusiness = 1,
    UnregisteredBusiness = 2,
    Government = 3,
    Individual = 4,
    SpecifiedPersonRcm = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PosRelation
{
    IntraState = 1,
    InterState = 2,
    UnionTerritory = 3,
    Export = 4,
    Import = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GstTaxTreatment
{
    ForwardCharge = 1,
    ReverseCharge = 2,
    Exempt = 3,
    OutOfScope = 4
}
