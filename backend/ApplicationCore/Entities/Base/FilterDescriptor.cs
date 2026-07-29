namespace ApplicationCore.Entities.Base;

public class FilterDescriptor
{
    public string PropertyName { get; set; } = string.Empty;
    public FilterOperator Operator { get; set; } = FilterOperator.Equal;
    public object? Value { get; set; }
    public object[]? Values { get; set; } // For In/NotIn operators

    public FilterDescriptor() { }

    public FilterDescriptor(string propertyName, FilterOperator op, object? value)
    {
        PropertyName = propertyName;
        Operator = op;
        Value = value;
    }

    public FilterDescriptor(string propertyName, FilterOperator op, object[] values)
    {
        PropertyName = propertyName;
        Operator = op;
        Values = values;
    }
}
