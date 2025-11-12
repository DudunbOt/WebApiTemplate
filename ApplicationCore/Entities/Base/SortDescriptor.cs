namespace ApplicationCore.Entities.Base;

public class SortDescriptor
{
    public string PropertyName { get; set; } = string.Empty;
    public SortOrder Order { get; set; } = SortOrder.Ascending;

    public SortDescriptor() { }

    public SortDescriptor(string propertyName, SortOrder order = SortOrder.Ascending)
    {
        PropertyName = propertyName;
        Order = order;
    }
}
