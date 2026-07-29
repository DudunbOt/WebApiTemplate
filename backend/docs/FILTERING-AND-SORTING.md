# Advanced Filtering & Sorting

Your backend template now supports powerful filtering and sorting capabilities out of the box.

## Sorting

### Basic Usage

Sort by a single field:

```http
GET /api/userinfo?sortBy=UserName&sortOrder=asc
GET /api/userinfo?sortBy=CreatedDate&sortOrder=desc
```

### Multiple Sort Fields

Sort by multiple fields (e.g., by UserName ascending, then by CreatedDate descending):

```http
GET /api/userinfo?sortBy=UserName,CreatedDate&sortOrder=asc,desc
```

### Sort Orders

- `asc` or `ascending` - Ascending order (A-Z, 0-9, oldest first)
- `desc` or `descending` - Descending order (Z-A, 9-0, newest first)

### Default Sorting

If no sort is specified, entities are sorted by `UpdatedDate` descending (newest first).

## Filtering

### Dynamic Filtering (New!)

The template now supports powerful dynamic filtering with various operators:

#### Basic Syntax

```http
GET /api/userinfo?filter[PropertyName][op]=operator&filter[PropertyName][value]=value
```

#### Supported Operators

- `eq` or `equal` - Exact match
- `ne` or `neq` or `notequal` - Not equal
- `gt` or `greaterthan` - Greater than
- `gte` or `ge` - Greater than or equal
- `lt` or `lessthan` - Less than
- `lte` or `le` - Less than or equal
- `contains` - String contains (case-sensitive)
- `startswith` - String starts with
- `endswith` - String ends with
- `in` - Value in list (use `[values]` instead of `[value]`)
- `notin` or `nin` - Value not in list
- `isnull` or `null` - Is null
- `isnotnull` or `notnull` - Is not null

#### Examples

```http
# Exact match
GET /api/userinfo?filter[UserName][op]=eq&filter[UserName][value]=john

# Contains search
GET /api/userinfo?filter[UserName][op]=contains&filter[UserName][value]=john

# Greater than or equal
GET /api/userinfo?filter[Id][op]=gte&filter[Id][value]=10

# In list (multiple values)
GET /api/userinfo?filter[UserName][op]=in&filter[UserName][values]=john,jane,bob

# Check if null
GET /api/userinfo?filter[DeletedDate][op]=isnull

# Multiple filters (AND logic)
GET /api/userinfo?filter[UserName][op]=contains&filter[UserName][value]=john&filter[Id][op]=gte&filter[Id][value]=5
```

### Specification-Based Filtering (Legacy, Still Supported)

The existing specification pattern is still used for custom filtering logic:

```http
# Exact match
GET /api/userinfo?userName=john

# Contains search
GET /api/userinfo?userNameContains=john
```

### Combined Filtering + Sorting

```http
# Dynamic filter + sort
GET /api/userinfo?filter[UserName][op]=contains&filter[UserName][value]=john&sortBy=CreatedDate&sortOrder=desc

# Specification filter + sort
GET /api/userinfo?userNameContains=john&sortBy=CreatedDate&sortOrder=desc

# Both filters + sort (specification AND dynamic filters are both applied)
GET /api/userinfo?userNameContains=john&filter[Id][op]=gte&filter[Id][value]=5&sortBy=CreatedDate&sortOrder=desc
```

## Including Related Data (Eager Loading)

The template supports eager loading of navigation properties through the `include` query parameter.

### Default Behavior

**Reference navigation properties** (foreign key relationships) are automatically included in queries. You don't need to do anything to get them.

**Collection navigation properties** (one-to-many relationships) are **not** included by default to avoid performance issues. You must explicitly request them.

### Basic Usage

Include a single collection:

```http
GET /api/userinfo?include=Orders
```

Include multiple collections:

```http
GET /api/userinfo?include=Orders,Addresses,EmailQueue
```

### Combined with Other Features

Include works seamlessly with filtering, sorting, and pagination:

```http
# Include with pagination
GET /api/userinfo?pageNumber=1&pageSize=10&include=Orders

# Include with sorting
GET /api/userinfo?sortBy=UserName&sortOrder=asc&include=Orders

# Include with filtering
GET /api/userinfo?filter[UserName][op]=contains&filter[UserName][value]=john&include=Orders

# All combined
GET /api/userinfo?filter[UserName][op]=contains&filter[UserName][value]=john&sortBy=CreatedDate&sortOrder=desc&pageNumber=1&pageSize=10&include=Orders,Addresses
```

### Case-Insensitive Property Names

Property names are matched case-insensitively:

```http
# All of these work
GET /api/userinfo?include=Orders
GET /api/userinfo?include=orders
GET /api/userinfo?include=ORDERS
```

### Implementation for New Entities

#### 1. Parse Includes in Your Controller

```csharp
[Authorize]
[HttpGet]
public async Task<IActionResult> GetProducts([FromQuery] Dictionary<string, string> filterParams, CancellationToken token = default)
{
    var spec = new DefaultSpecification<Product>();
    var sortDescriptors = ParseSortDescriptors(filterParams);
    var filterDescriptors = ParseFilterDescriptors(filterParams);
    var includes = ParseIncludes(filterParams);  // Parse the include parameter

    InitFilter(filterParams, out int pageNumber, out int pageSize);

    var pagination = await _service.GetCount(spec, pageNumber, pageSize, token);
    var result = await _service.GetList(spec, pageNumber, pageSize, sortDescriptors, filterDescriptors, includes, token);

    return Ok(new
    {
        pageInfo = pagination,
        products = _mapper.Map<List<ProductDTO>>(result)
    });
}
```

#### 2. For Single Entity Retrieval

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetProduct(int id, [FromQuery] Dictionary<string, string> filterParams, CancellationToken token = default)
{
    var includes = ParseIncludes(filterParams);
    var result = await _service.GetOne(id, token, includes);

    return Ok(_mapper.Map<ProductDTO>(result));
}
```

### How It Works Under the Hood

The `ServiceBase.ApplyIncludes` method:

1. **Auto-includes all reference navigations** - Foreign key relationships are always loaded
2. **Optionally includes collections** - Only when explicitly specified via `include` parameter
3. **Uses EF Core metadata** - Navigation properties are discovered dynamically
4. **Validates property names** - Only valid navigation properties are included

### Caching

Include configurations are factored into cache keys. Different include combinations are cached separately:

```http
# These are cached separately
GET /api/userinfo?include=Orders
GET /api/userinfo?include=Orders,Addresses
GET /api/userinfo  # No includes
```

### Performance Considerations

- **Be selective** - Only include collections you actually need
- **Avoid N+1** - Using `include` prevents N+1 query problems for related data
- **Watch payload size** - Including large collections increases response size

## Pagination

Works with both filtering and sorting:

```http
GET /api/userinfo?pageNumber=2&pageSize=20&sortBy=UserName&sortOrder=asc
```

## Implementation for New Entities

### Quick Start: Dynamic Filtering Only

For most cases, you don't need to create specifications anymore! Just use dynamic filtering:

```csharp
[Authorize]
[HttpGet]
public async Task<IActionResult> GetProducts([FromQuery] Dictionary<string, string> filterParams, CancellationToken token = default)
{
    var spec = new DefaultSpecification<Product>(); // Just filters out soft-deleted records
    var sortDescriptors = ParseSortDescriptors(filterParams);
    var filterDescriptors = ParseFilterDescriptors(filterParams); // Parse dynamic filters!

    InitFilter(filterParams, out int pageNumber, out int pageSize);

    var pagination = await _service.GetCount(spec, pageNumber, pageSize, token);
    var result = await _service.GetList(spec, pageNumber, pageSize, sortDescriptors, filterDescriptors, token);

    return Ok(new
    {
        pageInfo = pagination,
        products = _mapper.Map<List<ProductDTO>>(result)
    });
}
```

**That's it!** No need to create custom specifications for simple filtering. Use dynamic filters:

```http
# Filter by category, sort by price descending
GET /api/products?filter[Category][op]=eq&filter[Category][value]=Electronics&sortBy=Price&sortOrder=desc

# Search products by name, price range
GET /api/products?filter[Name][op]=contains&filter[Name][value]=phone&filter[Price][op]=gte&filter[Price][value]=100&filter[Price][op]=lte&filter[Price][value]=1000&sortBy=Name&sortOrder=asc
```

### Advanced: Custom Specification (Optional)

Only create specifications when you need complex business logic or reusable filters:

#### 1. Create Your Entity

```csharp
public class Product : EntityBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
}
```

#### 2. Create Specification (optional - for custom filter logic)

```csharp
public class ProductSpecification : ISpecificationBase<Product>
{
    public string? NameContains { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Category { get; set; }

    public Expression<Func<Product, bool>> ToExpression()
    {
        var predicate = PredicateBuilder.New<Product>(true);
        predicate = predicate.And(p => p.DeletedDate == null);

        if (!string.IsNullOrEmpty(NameContains))
            predicate = predicate.And(p => p.Name.Contains(NameContains));

        if (MinPrice.HasValue)
            predicate = predicate.And(p => p.Price >= MinPrice.Value);

        if (MaxPrice.HasValue)
            predicate = predicate.And(p => p.Price <= MaxPrice.Value);

        if (!string.IsNullOrEmpty(Category))
            predicate = predicate.And(p => p.Category == Category);

        return predicate;
    }
}
```

#### 3. Controller Implementation (with both)

```csharp
[Authorize]
[HttpGet]
public async Task<IActionResult> GetProducts([FromQuery] Dictionary<string, string> filterParams, CancellationToken token = default)
{
    var spec = CreateFilter(filterParams, out int pageNumber, out int pageSize);
    var sortDescriptors = ParseSortDescriptors(filterParams);
    var filterDescriptors = ParseFilterDescriptors(filterParams); // Add dynamic filters!

    var pagination = await _service.GetCount(spec, pageNumber, pageSize, token);
    var result = await _service.GetList(spec, pageNumber, pageSize, sortDescriptors, filterDescriptors, token);

    return Ok(new
    {
        pageInfo = pagination,
        products = _mapper.Map<List<ProductDTO>>(result)
    });
}

private ProductSpecification CreateFilter(Dictionary<string, string> filterParams, out int pageNumber, out int pageSize)
{
    InitFilter(filterParams, out pageNumber, out pageSize);

    var filter = new ProductSpecification();

    if (filterParams.ContainsKey("nameContains"))
        filter.NameContains = filterParams["nameContains"];

    if (filterParams.ContainsKey("minPrice"))
        filter.MinPrice = decimal.Parse(filterParams["minPrice"]);

    if (filterParams.ContainsKey("maxPrice"))
        filter.MaxPrice = decimal.Parse(filterParams["maxPrice"]);

    if (filterParams.ContainsKey("category"))
        filter.Category = filterParams["category"];

    return filter;
}
```

#### 4. Use It!

```http
# Specification-based filter (legacy syntax)
GET /api/products?category=Electronics&sortBy=Price&sortOrder=desc

# Dynamic filter (new syntax)
GET /api/products?filter[Category][op]=eq&filter[Category][value]=Electronics&sortBy=Price&sortOrder=desc

# Mix both! Specification AND dynamic filters
GET /api/products?nameContains=phone&filter[Price][op]=gte&filter[Price][value]=100&sortBy=Name&sortOrder=asc
```

## Advanced Sorting Features

### Case-Insensitive Property Matching

Property names are case-insensitive:

```http
# All of these work
GET /api/userinfo?sortBy=UserName
GET /api/userinfo?sortBy=username
GET /api/userinfo?sortBy=USERNAME
```

### Property Validation

If you specify an invalid property name, you'll get a clear error:

```json
{
  "statusCode": 400,
  "message": "Invalid argument",
  "details": "Property 'InvalidField' does not exist on type 'UserInfo'",
  "traceId": "..."
}
```

### Caching

Sorted results are cached with the sort configuration included in the cache key. Different sort orders are cached separately.

## Models Reference

### SortDescriptor

```csharp
public class SortDescriptor
{
    public string PropertyName { get; set; }  // Property to sort by
    public SortOrder Order { get; set; }      // Ascending or Descending
}
```

### SortOrder Enum

```csharp
public enum SortOrder
{
    Ascending,
    Descending
}
```

## Examples from Swagger

Once your API is running, you can test in Swagger:

### Get Users Sorted by Username

```
GET /api/userinfo?sortBy=UserName&sortOrder=asc
```

**Response:**
```json
{
  "pageInfo": {
    "currentPage": 1,
    "pageSize": 10,
    "totalPages": 1,
    "totalItems": 3
  },
  "users": [
    { "id": 2, "userName": "alice", "email": "alice@example.com" },
    { "id": 1, "userName": "bob", "email": "bob@example.com" },
    { "id": 3, "userName": "charlie", "email": "charlie@example.com" }
  ]
}
```

### Multiple Sorts + Filter

```
GET /api/userinfo?userNameContains=a&sortBy=UserName,CreatedDate&sortOrder=asc,desc
```

## Performance Considerations

### Indexing

For optimal performance, add database indexes on commonly sorted fields:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<UserInfo>()
        .HasIndex(u => u.UserName);

    modelBuilder.Entity<UserInfo>()
        .HasIndex(u => u.CreatedDate);

    // Composite index for multiple sort fields
    modelBuilder.Entity<UserInfo>()
        .HasIndex(u => new { u.UserName, u.CreatedDate });
}
```

### Caching

Sorting is included in the cache key, so different sort orders are cached independently. This prevents cache misses but increases memory usage if many different sort combinations are used.

## Troubleshooting

### "Property does not exist" Error

**Problem:** Getting error about property not found

**Solution:**
1. Check property name spelling
2. Ensure property is public
3. Property names are matched case-insensitively

### Sorting Not Working

**Problem:** Results don't appear sorted

**Checklist:**
1. Verify `sortBy` parameter is passed correctly
2. Check property name exists on entity
3. Ensure ServiceBase.GetList is called with sortDescriptors parameter
4. Clear cache if testing: set `UseCache: false` in appsettings.json temporarily

### Mixed Sort Orders

**Problem:** Want some fields ascending, others descending

**Solution:** Use comma-separated values:
```
?sortBy=Field1,Field2,Field3&sortOrder=asc,desc,asc
```

## Advanced Dynamic Filtering Features

### Operator Flexibility

All operators support multiple alias formats for convenience:
- `gte`, `ge`, `greaterthanorequal` all work the same
- `contains`, `startswith`, `endswith` for string operations
- `isnull`, `null` for null checks

### Type Conversion

Values are automatically converted to the correct property type:
```http
# String to int
GET /api/userinfo?filter[Id][op]=eq&filter[Id][value]=123

# String to decimal
GET /api/products?filter[Price][op]=gte&filter[Price][value]=99.99

# String to DateTime (standard formats)
GET /api/userinfo?filter[CreatedDate][op]=gte&filter[CreatedDate][value]=2024-01-01
```

### Property Validation

If you specify an invalid property name, you'll get a clear error:
```json
{
  "statusCode": 400,
  "message": "Invalid argument",
  "details": "Property 'InvalidField' does not exist on type 'UserInfo'",
  "traceId": "..."
}
```

### Combining with Specifications

Dynamic filters and specifications work together with AND logic:
```http
# Custom specification + dynamic filter
GET /api/userinfo?userName=john&filter[Id][op]=gte&filter[Id][value]=10
```

This applies BOTH:
1. The `userName` specification filter (DeletedDate == null AND UserName == "john")
2. The dynamic filter (Id >= 10)

Result: Users named "john" with Id >= 10 that are not deleted

## Future Enhancements

### OData Support (Optional)

Consider adding OData for standardized querying:
```http
GET /api/products?$filter=Price ge 100 and Price le 500&$orderby=Name desc
```

## Summary

✅ **Dynamic Filtering** - 13 operators (eq, ne, gt, gte, lt, lte, contains, startswith, endswith, in, notin, isnull, isnotnull)
✅ **Sorting** - Sort by any property, multiple fields supported
✅ **Including Related Data** - Eager load collections via `?include=Collection1,Collection2`
✅ **Specification Pattern** - Still available for complex business logic
✅ **Pagination** - Works seamlessly with sorting and filtering
✅ **Caching** - Results cached including sort, filter, and include configuration
✅ **Type-Safe** - Property and operator validation at runtime
✅ **Generic** - Works for all entities automatically
✅ **Backward Compatible** - Existing specifications continue to work
✅ **Flexible Syntax** - Multiple operator aliases supported

**Default Behavior:**
- No sorting? Defaults to `UpdatedDate` descending (newest first)
- No filtering? Returns all non-deleted records (via DefaultSpecification)
- No includes? Reference navigations auto-loaded, collections excluded
