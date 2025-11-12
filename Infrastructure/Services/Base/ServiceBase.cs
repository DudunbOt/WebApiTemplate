using ApplicationCore.Entities;
using ApplicationCore.Entities.Base;
using ApplicationCore.Exceptions;
using ApplicationCore.Helpers;
using ApplicationCore.Interfaces;
using ApplicationCore.Specifications;
using AutoMapper;
using Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Objects.DataClasses;
using System.Drawing.Printing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ServiceBase<T> : IServiceBase<T> where T : EntityBase
    {
        protected AppDbContext _context;
        protected readonly IDistributedCache _cache;
        protected AppConfig _config;

        private string ENTITY_COUNT_KEY = $"EntityCount_{typeof(T).Name}";
        private string ENTITY_KEY = $"Entity_{typeof(T).Name}";

        public ServiceBase(AppDbContext context, IDistributedCache cache, IOptions<AppConfig> config)
        {
            _context = context;
            _cache = cache;
            _config = config.Value;
        }

        public async Task Delete(int id, CancellationToken token = default)
        {
            var entity = await _context.Set<T>().FindAsync(id);
            if (entity == null)
            {
                throw new NotFoundException(typeof(T).Name, id);
            }

            var property = entity.GetType().GetProperty("DeletedDate");
            if (property != null && property.PropertyType == typeof(DateTime?))
            {
                property.SetValue(entity, DateTime.Now);
                _context.Entry(entity).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                if(_config.UseCache)
                {
                    // Clear cache for this entity
                    string cacheKey = $"{ENTITY_KEY}_{id}";
                    await _cache.RemoveAsync(cacheKey, token);
                }
            }
            else
            {
                throw new InvalidOperationException($"Entity of type {typeof(T)} does not support soft delete.");
            }
        }

        public async Task<Pagination> GetCount(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, List<FilterDescriptor>? filterDescriptors = null, CancellationToken token = default)
        {
            IQueryable<T> query = _context.Set<T>();

            var uniqueSpec = specification != null ? JsonConvert.SerializeObject(specification).GetHashCode() : 0;
            var filterKey = filterDescriptors != null ? JsonConvert.SerializeObject(filterDescriptors).GetHashCode() : 0;
            var cacheKey = ENTITY_COUNT_KEY + $"_Filter{uniqueSpec}_DynFilter{filterKey}";

            if (_config.UseCache)
            {
                var cacheData = await _cache.GetStringAsync(cacheKey, token);

                if (!string.IsNullOrEmpty(cacheData))
                {
                    return JsonConvert.DeserializeObject<Pagination>(cacheData);
                }
            }

            if (specification == null)
                specification = new DefaultSpecification<T>();

            var expression = specification.ToExpression();
            query = query.Where(expression);

            // Apply dynamic filtering
            var dynamicFilter = FilterBuilder<T>.BuildFilterExpression(filterDescriptors);
            if (dynamicFilter != null)
            {
                query = query.Where(dynamicFilter);
            }

            int totalItems = await query.CountAsync();

            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            Pagination pagination = new Pagination()
            {
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };

            if(totalItems > 0 && _config.UseCache)
            {
                await WriteToCache(cacheKey, pagination, token);
            }

            return pagination;
        }

        public async Task<List<T>> GetList(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, List<SortDescriptor>? sortDescriptors = null, List<FilterDescriptor>? filterDescriptors = null, CancellationToken token = default)
        {
            int skip = (pageNumber - 1) * pageSize;
            IQueryable<T> query = _context.Set<T>();


            if (specification == null)
                specification = new DefaultSpecification<T>();

            var uniqueSpec = JsonConvert.SerializeObject(specification);
            var sortKey = sortDescriptors != null ? JsonConvert.SerializeObject(sortDescriptors).GetHashCode() : 0;
            var filterKey = filterDescriptors != null ? JsonConvert.SerializeObject(filterDescriptors).GetHashCode() : 0;
            var cacheKey = ENTITY_KEY + $"_Page{pageNumber}_Size{pageSize}_Filter{uniqueSpec.GetHashCode()}_Sort{sortKey}_DynFilter{filterKey}";

            if(_config.UseCache)
            {
                var cacheData = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cacheData))
                    return JsonConvert.DeserializeObject<List<T>>(cacheData);
            }

            var expression = specification.ToExpression();
            query = query.Where(expression);

            // Apply dynamic filtering
            var dynamicFilter = FilterBuilder<T>.BuildFilterExpression(filterDescriptors);
            if (dynamicFilter != null)
            {
                query = query.Where(dynamicFilter);
            }

            // Apply sorting
            query = ApplySorting(query, sortDescriptors);

            query = query.Skip(skip).Take(pageSize);

            var entities = await query.ToListAsync(token);

            if (entities != null && entities.Count > 0 && _config.UseCache)
                await WriteToCache(cacheKey, entities, token);

            return entities ?? [];
        }

        public async Task<T> GetOne(ISpecificationBase<T> specification, CancellationToken token = default)
        {
            if(specification == null) throw new ArgumentNullException(nameof(specification));


            var uniqueSpec = JsonConvert.SerializeObject(specification);
            var cacheKey = ENTITY_KEY + $"_Filter{uniqueSpec.GetHashCode()}";

            if (_config.UseCache)
            {
                var cacheData = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cacheData))
                    return JsonConvert.DeserializeObject<T>(cacheData);
            }
            
            var expression = specification.ToExpression();

            IQueryable<T> query = _context.Set<T>();
            query = query.Where(expression);

            var entity = await query.FirstOrDefaultAsync(token);

            if(entity != null && _config.UseCache) 
                await WriteToCache(cacheKey, entity, token);

            return entity;
        }

        public async Task<T> GetOne(int id, CancellationToken token = default)
        {
            if (id <= 0) throw new ArgumentException("Parameter on Get One can't be less that 1");

            var cacheKey = ENTITY_KEY + $"_{id}";
            if (_config.UseCache)
            {
                var cacheData = await _cache.GetStringAsync(cacheKey, token);
                if (!string.IsNullOrEmpty(cacheData))
                    return JsonConvert.DeserializeObject<T>(cacheData);
            }

            var entity = await _context.Set<T>().FindAsync(id, token);
            if (entity == null)
            {
                throw new NotFoundException(typeof(T).Name, id);
            }

            if (entity != null && _config.UseCache)
                await WriteToCache(cacheKey, entity, token);

            return entity;
        }

        public async Task<T> Upsert(object model, int id = 0, CancellationToken token = default)
        {
            T entity;
            if (model is not T)
            {
                throw new ArgumentException($"Parameter model must be of type {typeof(T)}");
            }

            entity = (T)model;
            if (id == 0)
            {
                entity.CreatedDate = DateTime.Now;
                entity.UpdatedDate = DateTime.Now;
                _context.Set<T>().Add(entity);
            }
            else
            {
                var existingEntity = await _context.Set<T>().FindAsync(id);
                if (existingEntity == null)
                {
                    throw new NotFoundException(typeof(T).Name, id);
                }

                entity.UpdatedDate = DateTime.Now;
                _context.Entry(existingEntity).CurrentValues.SetValues(entity);

                if(_config.UseCache)
                {
                    string cacheKey = $"Entity_{typeof(T).Name}_{id}";
                    await _cache.RemoveAsync(cacheKey, token);
                }

                entity = existingEntity;
            }

            await _context.SaveChangesAsync();

            return entity;
        }

        private async Task WriteToCache(string cacheKey, object obj, CancellationToken token = default)
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(69)
            };
            await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(obj), cacheOptions, token);
        }

        private IQueryable<T> ApplySorting(IQueryable<T> query, List<SortDescriptor>? sortDescriptors)
        {
            if (sortDescriptors == null || !sortDescriptors.Any())
            {
                // Default sorting by UpdatedDate descending
                if (typeof(EntityBase).IsAssignableFrom(typeof(T)))
                {
                    return query.OrderByDescending(e => ((EntityBase)(object)e).UpdatedDate);
                }
                return query;
            }

            IOrderedQueryable<T>? orderedQuery = null;

            for (int i = 0; i < sortDescriptors.Count; i++)
            {
                var sort = sortDescriptors[i];
                var propertyInfo = typeof(T).GetProperty(sort.PropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                if (propertyInfo == null)
                {
                    throw new ArgumentException($"Property '{sort.PropertyName}' does not exist on type '{typeof(T).Name}'");
                }

                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, propertyInfo);
                var lambda = Expression.Lambda(property, parameter);

                var methodName = i == 0
                    ? (sort.Order == SortOrder.Ascending ? "OrderBy" : "OrderByDescending")
                    : (sort.Order == SortOrder.Ascending ? "ThenBy" : "ThenByDescending");

                var resultExpression = Expression.Call(
                    typeof(Queryable),
                    methodName,
                    new Type[] { typeof(T), propertyInfo.PropertyType },
                    i == 0 ? query.Expression : orderedQuery!.Expression,
                    Expression.Quote(lambda)
                );

                orderedQuery = (IOrderedQueryable<T>)(i == 0 ? query.Provider.CreateQuery<T>(resultExpression) : orderedQuery!.Provider.CreateQuery<T>(resultExpression));
            }

            return orderedQuery ?? query;
        }

    }
}
