using ApplicationCore.Entities.Base;
using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    public class ControllerBase<TService>(TService service, IMapper mapper) : ControllerBase where TService : class
    {
        protected readonly TService _service = service;
        protected readonly IMapper _mapper = mapper;

        protected void InitFilter(Dictionary<string, string> filterParams, out int pageNumber, out int pageSize)
        {
            pageNumber = 1;
            pageSize = 10;

            if (filterParams.ContainsKey("pageNumber"))
            {
                pageNumber = int.Parse(filterParams["pageNumber"]);
            }

            if (filterParams.ContainsKey("pageSize"))
            {
                pageSize = int.Parse(filterParams["pageSize"]);
            }
        }

        /// <summary>
        /// Parse sort descriptors from query parameters
        /// Supports: ?sortBy=PropertyName&sortOrder=asc
        /// Or multiple: ?sortBy=Name,CreatedDate&sortOrder=asc,desc
        /// </summary>
        protected List<SortDescriptor>? ParseSortDescriptors(Dictionary<string, string> filterParams)
        {
            if (!filterParams.ContainsKey("sortBy"))
                return null;

            var sortByFields = filterParams["sortBy"].Split(',', StringSplitOptions.RemoveEmptyEntries);
            var sortOrders = filterParams.ContainsKey("sortOrder")
                ? filterParams["sortOrder"].Split(',', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();

            var sortDescriptors = new List<SortDescriptor>();

            for (int i = 0; i < sortByFields.Length; i++)
            {
                var field = sortByFields[i].Trim();
                var order = SortOrder.Ascending;

                // Get corresponding sort order, or default to Ascending
                if (i < sortOrders.Length)
                {
                    var orderStr = sortOrders[i].Trim().ToLower();
                    order = orderStr == "desc" || orderStr == "descending"
                        ? SortOrder.Descending
                        : SortOrder.Ascending;
                }

                sortDescriptors.Add(new SortDescriptor(field, order));
            }

            return sortDescriptors.Any() ? sortDescriptors : null;
        }

        /// <summary>
        /// Parse filter descriptors from query parameters
        /// Supports: ?filter[PropertyName][op]=eq&filter[PropertyName][value]=SomeValue
        /// Or: ?filter[Age][op]=gte&filter[Age][value]=18
        /// </summary>
        protected List<FilterDescriptor>? ParseFilterDescriptors(Dictionary<string, string>? filterParams = null)
        {
            var filterDescriptors = new List<FilterDescriptor>();

            // Use Request.Query directly if Dictionary is not provided or empty
            var queryKeys = filterParams != null && filterParams.Any()
                ? filterParams.Keys
                : Request.Query.Keys;

            var queryDict = filterParams != null && filterParams.Any()
                ? filterParams
                : Request.Query.ToDictionary(k => k.Key, k => k.Value.ToString());

            // Group keys by property name
            var filterGroups = queryKeys
                .Where(k => k.StartsWith("filter[", StringComparison.OrdinalIgnoreCase))
                .GroupBy(k => ExtractPropertyName(k))
                .Where(g => !string.IsNullOrEmpty(g.Key));

            foreach (var group in filterGroups)
            {
                var propertyName = group.Key!;
                var operatorKey = group.FirstOrDefault(k => k.Contains("[op]", StringComparison.OrdinalIgnoreCase));
                var valueKey = group.FirstOrDefault(k => k.Contains("[value]", StringComparison.OrdinalIgnoreCase));
                var valuesKey = group.FirstOrDefault(k => k.Contains("[values]", StringComparison.OrdinalIgnoreCase));

                if (operatorKey == null)
                    continue;

                var operatorStr = queryDict[operatorKey].ToLower();
                var filterOperator = ParseOperator(operatorStr);

                // Handle In/NotIn operators with multiple values
                if ((filterOperator == FilterOperator.In || filterOperator == FilterOperator.NotIn) && valuesKey != null)
                {
                    var valuesStr = queryDict[valuesKey];
                    var values = valuesStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => (object)v.Trim())
                        .ToArray();

                    filterDescriptors.Add(new FilterDescriptor(propertyName, filterOperator, values));
                }
                // Handle IsNull/IsNotNull operators (no value needed)
                else if (filterOperator == FilterOperator.IsNull || filterOperator == FilterOperator.IsNotNull)
                {
                    filterDescriptors.Add(new FilterDescriptor(propertyName, filterOperator, null));
                }
                // Handle regular operators with single value
                else if (valueKey != null)
                {
                    var value = queryDict[valueKey];
                    filterDescriptors.Add(new FilterDescriptor(propertyName, filterOperator, value));
                }
            }

            return filterDescriptors.Any() ? filterDescriptors : null;
        }

        private string? ExtractPropertyName(string filterKey)
        {
            // Extract property name from "filter[PropertyName][op]" or "filter[PropertyName][value]"
            var match = System.Text.RegularExpressions.Regex.Match(filterKey, @"filter\[([^\]]+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private FilterOperator ParseOperator(string operatorStr)
        {
            return operatorStr.ToLower() switch
            {
                "eq" or "equal" => FilterOperator.Equal,
                "ne" or "neq" or "notequal" => FilterOperator.NotEqual,
                "gt" or "greaterthan" => FilterOperator.GreaterThan,
                "gte" or "ge" or "greaterthanorequal" => FilterOperator.GreaterThanOrEqual,
                "lt" or "lessthan" => FilterOperator.LessThan,
                "lte" or "le" or "lessthanorequal" => FilterOperator.LessThanOrEqual,
                "contains" => FilterOperator.Contains,
                "startswith" => FilterOperator.StartsWith,
                "endswith" => FilterOperator.EndsWith,
                "in" => FilterOperator.In,
                "notin" or "nin" => FilterOperator.NotIn,
                "isnull" or "null" => FilterOperator.IsNull,
                "isnotnull" or "notnull" => FilterOperator.IsNotNull,
                _ => throw new ArgumentException($"Unsupported filter operator: {operatorStr}")
            };
        }
    }
}
