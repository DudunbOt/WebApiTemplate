using System;

namespace ApplicationCore.Attributes
{
    /// <summary>
    /// Marks a property to be preserved during update operations.
    /// Properties with this attribute will not be overwritten by incoming values during Upsert.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class PreserveOnUpdateAttribute : Attribute
    {
    }
}
