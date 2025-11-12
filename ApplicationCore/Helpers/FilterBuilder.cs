using ApplicationCore.Entities.Base;
using System.Linq.Expressions;
using System.Reflection;

namespace ApplicationCore.Helpers;

public static class FilterBuilder<T> where T : class
{
    public static Expression<Func<T, bool>>? BuildFilterExpression(List<FilterDescriptor>? filterDescriptors)
    {
        if (filterDescriptors == null || !filterDescriptors.Any())
            return null;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combinedExpression = null;

        foreach (var descriptor in filterDescriptors)
        {
            var filterExpression = BuildSingleFilterExpression(parameter, descriptor);

            if (filterExpression != null)
            {
                combinedExpression = combinedExpression == null
                    ? filterExpression
                    : Expression.AndAlso(combinedExpression, filterExpression);
            }
        }

        if (combinedExpression == null)
            return null;

        return Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
    }

    private static Expression? BuildSingleFilterExpression(ParameterExpression parameter, FilterDescriptor descriptor)
    {
        var propertyInfo = typeof(T).GetProperty(descriptor.PropertyName,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (propertyInfo == null)
            throw new ArgumentException($"Property '{descriptor.PropertyName}' does not exist on type '{typeof(T).Name}'");

        var property = Expression.Property(parameter, propertyInfo);

        return descriptor.Operator switch
        {
            FilterOperator.Equal => BuildEqualExpression(property, descriptor.Value, propertyInfo.PropertyType),
            FilterOperator.NotEqual => BuildNotEqualExpression(property, descriptor.Value, propertyInfo.PropertyType),
            FilterOperator.GreaterThan => BuildComparisonExpression(property, descriptor.Value, propertyInfo.PropertyType, Expression.GreaterThan),
            FilterOperator.GreaterThanOrEqual => BuildComparisonExpression(property, descriptor.Value, propertyInfo.PropertyType, Expression.GreaterThanOrEqual),
            FilterOperator.LessThan => BuildComparisonExpression(property, descriptor.Value, propertyInfo.PropertyType, Expression.LessThan),
            FilterOperator.LessThanOrEqual => BuildComparisonExpression(property, descriptor.Value, propertyInfo.PropertyType, Expression.LessThanOrEqual),
            FilterOperator.Contains => BuildContainsExpression(property, descriptor.Value),
            FilterOperator.StartsWith => BuildStringMethodExpression(property, descriptor.Value, "StartsWith"),
            FilterOperator.EndsWith => BuildStringMethodExpression(property, descriptor.Value, "EndsWith"),
            FilterOperator.In => BuildInExpression(property, descriptor.Values, propertyInfo.PropertyType),
            FilterOperator.NotIn => Expression.Not(BuildInExpression(property, descriptor.Values, propertyInfo.PropertyType)),
            FilterOperator.IsNull => Expression.Equal(property, Expression.Constant(null)),
            FilterOperator.IsNotNull => Expression.NotEqual(property, Expression.Constant(null)),
            _ => throw new ArgumentException($"Unsupported filter operator: {descriptor.Operator}")
        };
    }

    private static Expression BuildEqualExpression(MemberExpression property, object? value, Type propertyType)
    {
        var constant = Expression.Constant(ConvertValue(value, propertyType), propertyType);
        return Expression.Equal(property, constant);
    }

    private static Expression BuildNotEqualExpression(MemberExpression property, object? value, Type propertyType)
    {
        var constant = Expression.Constant(ConvertValue(value, propertyType), propertyType);
        return Expression.NotEqual(property, constant);
    }

    private static Expression BuildComparisonExpression(MemberExpression property, object? value, Type propertyType,
        Func<Expression, Expression, BinaryExpression> comparisonFunc)
    {
        var constant = Expression.Constant(ConvertValue(value, propertyType), propertyType);
        return comparisonFunc(property, constant);
    }

    private static Expression BuildContainsExpression(MemberExpression property, object? value)
    {
        if (property.Type != typeof(string))
            throw new ArgumentException($"Contains operator can only be used with string properties");

        var stringValue = value?.ToString() ?? string.Empty;
        var constant = Expression.Constant(stringValue);
        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        return Expression.Call(property, containsMethod, constant);
    }

    private static Expression BuildStringMethodExpression(MemberExpression property, object? value, string methodName)
    {
        if (property.Type != typeof(string))
            throw new ArgumentException($"{methodName} operator can only be used with string properties");

        var stringValue = value?.ToString() ?? string.Empty;
        var constant = Expression.Constant(stringValue);
        var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
        return Expression.Call(property, method, constant);
    }

    private static Expression BuildInExpression(MemberExpression property, object[]? values, Type propertyType)
    {
        if (values == null || values.Length == 0)
            return Expression.Constant(false);

        var convertedValues = values.Select(v => ConvertValue(v, propertyType)).ToArray();
        var listType = typeof(List<>).MakeGenericType(propertyType);
        var list = Activator.CreateInstance(listType)!;
        var addMethod = listType.GetMethod("Add")!;

        foreach (var value in convertedValues)
        {
            addMethod.Invoke(list, new[] { value });
        }

        var containsMethod = listType.GetMethod("Contains")!;
        var listConstant = Expression.Constant(list);
        return Expression.Call(listConstant, containsMethod, property);
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
            return null;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // If value is already the correct type
        if (value.GetType() == underlyingType)
            return value;

        // Convert string to target type
        if (value is string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue))
                return null;

            return Convert.ChangeType(stringValue, underlyingType);
        }

        return Convert.ChangeType(value, underlyingType);
    }
}
