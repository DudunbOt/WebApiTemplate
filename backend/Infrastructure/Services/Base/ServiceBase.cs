using ApplicationCore.Attributes;
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
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core.Objects.DataClasses;
using System.Drawing.Printing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public partial class ServiceBase<T> : IServiceBase<T> where T : EntityBase
    {
        protected AppDbContext _context;
        protected readonly IDistributedCache _cache;
        protected AppConfig _config;
        protected readonly ICurrentUser _currentUser;
        private string ENTITY_COUNT_KEY = $"EntityCount_{typeof(T).Name}";
        private string ENTITY_KEY = $"Entity_{typeof(T).Name}";
        protected Dictionary<string, List<string>> errors = new Dictionary<string, List<string>>();

        protected bool ServiceState { get => errors.Count == 0; }

        protected void AddError(string propertyName, string errorMessage)
        {
            if (!errors.ContainsKey(propertyName))
            {
                errors[propertyName] = new List<string>();
            }
            errors[propertyName].Add(errorMessage);
        }
        protected string Username => _currentUser.Username;

        public ServiceBase(AppDbContext context, IDistributedCache cache, IOptions<AppConfig> config, ICurrentUser currentUser)
        {
            _context = context;
            _cache = cache;
            _config = config.Value;
            _currentUser = currentUser;
        }

        public virtual async Task Delete(int id, CancellationToken token = default)
        {
            var entity = await _context.Set<T>().FindAsync([id], token);

            if (entity == null)
                throw new NotFoundException(typeof(T).Name, id);

            if (entity.DeletedDate.HasValue)
                return;

            entity.DeletedDate = DateTime.Now;
            entity.DeletedBy = Username;

            await _context.SaveChangesAsync(token);

            if (_config.UseCache)
            {
                await _cache.RemoveAsync($"{ENTITY_KEY}_{id}", token);
            }
        }

        public virtual async Task<Pagination> GetCount(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, List<FilterDescriptor>? filterDescriptors = null, CancellationToken token = default)
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

            if (totalItems > 0 && _config.UseCache)
            {
                await WriteToCache(cacheKey, pagination, token);
            }

            return pagination;
        }

        public virtual async Task<List<T>> GetList(
            ISpecificationBase<T>? specification = null,
            int pageNumber = 1,
            int pageSize = 10,
            List<SortDescriptor>? sortDescriptors = null,
            List<FilterDescriptor>? filterDescriptors = null,
            List<string>? includes = null,
            CancellationToken token = default)
        {
            int skip = (pageNumber - 1) * pageSize;
            IQueryable<T> query = _context.Set<T>();

            // Apply includes - default reference includes + any specified includes
            query = ApplyIncludes(query, includes);

            if (specification == null)
                specification = new DefaultSpecification<T>();

            var uniqueSpec = JsonConvert.SerializeObject(specification);
            var sortKey = sortDescriptors != null ? JsonConvert.SerializeObject(sortDescriptors).GetHashCode() : 0;
            var filterKey = filterDescriptors != null ? JsonConvert.SerializeObject(filterDescriptors).GetHashCode() : 0;
            var includeKey = includes != null ? string.Join("_", includes).GetHashCode() : 0;
            var cacheKey = ENTITY_KEY + $"_Page{pageNumber}_Size{pageSize}_Filter{uniqueSpec.GetHashCode()}_Sort{sortKey}_DynFilter{filterKey}_Inc{includeKey}";

            if (_config.UseCache)
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

        public virtual async Task<T> GetOne(ISpecificationBase<T> specification, CancellationToken token = default, List<string>? includes = null)
        {
            if (specification == null) throw new ArgumentNullException(nameof(specification));

            var uniqueSpec = JsonConvert.SerializeObject(specification);
            var includeKey = includes != null ? string.Join("_", includes).GetHashCode() : 0;
            var cacheKey = ENTITY_KEY + $"_Filter{uniqueSpec.GetHashCode()}_Inc{includeKey}";

            if (_config.UseCache)
            {
                var cacheData = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cacheData))
                    return JsonConvert.DeserializeObject<T>(cacheData);
            }

            var expression = specification.ToExpression();

            IQueryable<T> query = _context.Set<T>();

            // Apply includes - default reference includes + any specified includes
            query = ApplyIncludes(query, includes);

            query = query.Where(expression);

            var entity = await query.FirstOrDefaultAsync(token);

            if (entity != null && _config.UseCache)
                await WriteToCache(cacheKey, entity, token);

            return entity;
        }

        public virtual async Task<T> GetOne(int id, CancellationToken token = default, List<string>? includes = null)
        {
            if (id <= 0) throw new ArgumentException("Parameter on Get One can't be less that 1");

            var includeKey = includes != null ? string.Join("_", includes).GetHashCode() : 0;
            var cacheKey = ENTITY_KEY + $"_{id}_Inc{includeKey}";

            if (_config.UseCache)
            {
                var cacheData = await _cache.GetStringAsync(cacheKey, token);
                if (!string.IsNullOrEmpty(cacheData))
                    return JsonConvert.DeserializeObject<T>(cacheData);
            }

            IQueryable<T> query = _context.Set<T>();

            // Apply includes - default reference includes + any specified includes
            query = ApplyIncludes(query, includes);

            // Use Where + FirstOrDefaultAsync instead of FindAsync to support includes
            var entity = await query.FirstOrDefaultAsync(e => e.Id == id, token);

            if (entity == null)
            {
                throw new NotFoundException(typeof(T).Name, id);
            }

            if (_config.UseCache)
                await WriteToCache(cacheKey, entity, token);

            return entity;
        }

        public virtual async Task<T> Upsert(T entity, int id = 0, CancellationToken token = default, bool commit = true)
        {
            if (id == 0)
            {
                if (!await ValidateOnInsert(entity))
                {
                    throw new ValidationException(errors);
                }

                entity.CreatedDate = DateTime.Now;
                entity.UpdatedDate = DateTime.Now;
                entity.CreatedBy = Username;
                entity.UpdatedBy = Username;
                _context.Set<T>().Add(entity);
            }
            else
            {
                var existingEntity = await _context.Set<T>().FindAsync(id);
                if (existingEntity == null)
                {
                    throw new NotFoundException(typeof(T).Name, id);
                }

                if (!await ValidateOnUpdate(entity))
                {
                    throw new ValidationException(errors);
                }

                _context.Entry(existingEntity).CurrentValues.SetValues(entity);

                #region Ignore Created and Deleted fields on update
                _context.Entry(existingEntity)
                    .Property(x => x.CreatedDate)
                    .IsModified = false;
                _context.Entry(existingEntity)
                    .Property(x => x.CreatedBy)
                    .IsModified = false;
                _context.Entry(existingEntity)
                    .Property(x => x.DeletedBy)
                    .IsModified = false;
                _context.Entry(existingEntity)
                    .Property(x => x.DeletedDate)
                    .IsModified = false;
                #endregion

                #region Ignore properties marked with PreserveOnUpdate attribute
                var preservedProperties = typeof(T).GetProperties()
                    .Where(p => p.GetCustomAttribute<PreserveOnUpdateAttribute>() != null)
                    .Select(p => p.Name)
                    .ToList();

                foreach (var propName in preservedProperties)
                {
                    _context.Entry(existingEntity).Property(propName).IsModified = false;
                }
                #endregion

                existingEntity.UpdatedDate = DateTime.Now;
                existingEntity.UpdatedBy = Username;

                if (_config.UseCache)
                {
                    string cacheKey = $"Entity_{typeof(T).Name}_{id}";
                    await _cache.RemoveAsync(cacheKey, token);
                }

                entity = existingEntity;
            }

            if (commit)
                await _context.SaveChangesAsync(token);

            return entity;
        }

        protected virtual async Task WriteToCache(string cacheKey, object obj, CancellationToken token = default)
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(69)
            };
            await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(obj), cacheOptions, token);
        }

        #region Sort, Filter, Includes
        protected virtual IQueryable<T> ApplySorting(IQueryable<T> query, List<SortDescriptor>? sortDescriptors)
        {
            if (sortDescriptors == null || !sortDescriptors.Any())
            {
                // Default sorting by CreatedDate descending
                if (typeof(EntityBase).IsAssignableFrom(typeof(T)))
                {
                    return query.OrderByDescending(e => ((EntityBase)(object)e).CreatedDate);
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

        /// <summary>
        /// Applies includes for eager loading.
        /// By default, includes all reference navigation properties (not collections).
        /// Additional includes can be specified by name (supports both references and collections).
        /// </summary>
        protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query, List<string>? includes = null)
        {
            var entityType = _context.Model.FindEntityType(typeof(T));
            if (entityType == null) return query;

            var allNavigations = entityType.GetNavigations()
                .ToDictionary(n => n.Name, n => n, StringComparer.OrdinalIgnoreCase);

            // Always include reference navigations (not collections) by default
            foreach (var navigation in allNavigations.Values)
            {
                if (!navigation.IsCollection)
                {
                    query = query.Include(navigation.Name);
                }
            }

            // Apply additional includes specified by name (can be collections or references)
            if (includes != null && includes.Count > 0)
            {
                foreach (var includeName in includes)
                {
                    if (allNavigations.TryGetValue(includeName, out var navigation))
                    {
                        // Only include if it's a collection (references are already included above)
                        if (navigation.IsCollection)
                        {
                            query = query.Include(navigation.Name);
                        }
                    }
                }
            }

            return query;
        }
        #endregion

        #region Validation
        protected virtual async Task<bool> ValidateBase(T entity)
        {
            if (entity == null)
            {
                throw new ValidationException("Entity is null");
            }

            return ServiceState;
        }
        protected virtual async Task<bool> ValidateOnInsert(T entity)
        {
            await ValidateBase(entity);

            return ServiceState;
        }
        protected virtual async Task<bool> ValidateOnUpdate(T entity)
        {
            await ValidateBase(entity);

            return ServiceState;
        }
        #endregion

        #region TRANSACTION
        /// <summary>
        /// Checks if there is an active database transaction.
        /// </summary>
        protected bool HasActiveTransaction => _context.Database.CurrentTransaction != null;

        /// <summary>
        /// Begins a transaction only if one is not already active.
        /// Returns null if a transaction already exists (caller should not commit/rollback).
        /// </summary>
        protected async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken token = default)
        {
            if (HasActiveTransaction)
                return null;

            return await _context.Database.BeginTransactionAsync(token);
        }

        /// <summary>
        /// Commits the transaction only if we own it (transaction is not null).
        /// </summary>
        protected async Task CommitTransactionAsync(IDbContextTransaction? transaction, CancellationToken token = default)
        {
            if (transaction != null)
            {
                await transaction.CommitAsync(token);
            }
        }

        /// <summary>
        /// Rolls back the transaction only if we own it (transaction is not null).
        /// </summary>
        protected async Task RollbackTransactionAsync(IDbContextTransaction? transaction, CancellationToken token = default)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(token);
            }
        }
        #endregion

    }
}