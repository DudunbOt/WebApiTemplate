namespace ApplicationCore.Entities.Base;

public enum FilterOperator
{
    Equal,              // ==
    NotEqual,           // !=
    GreaterThan,        // >
    GreaterThanOrEqual, // >=
    LessThan,           // <
    LessThanOrEqual,    // <=
    Contains,           // string.Contains()
    StartsWith,         // string.StartsWith()
    EndsWith,           // string.EndsWith()
    In,                 // value in list
    NotIn,               // value not in list
    IsNull,             // is null
    IsNotNull          // is not null
}
