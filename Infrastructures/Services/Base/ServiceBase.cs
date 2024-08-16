using ApplicationCore.Entities;
using ApplicationCore.Interfaces;
using ApplicationCore.Specifications;
using AutoMapper;
using Infrastructures.Configurations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructures.Services
{
    public class ServiceBase<T> : IServiceBase<T> where T : EntityBase
    {
        protected AppDbContext _context;
        protected IMapper? _mapper;

        public ServiceBase(AppDbContext context, IMapper? mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task Delete(int id, CancellationToken token = default)
        {
            var entity = await _context.Set<T>().FindAsync(id);
            if (entity == null)
            {
                throw new InvalidOperationException($"Entity of type {typeof(T)} with Id {id} not found");
            }

            var property = entity.GetType().GetProperty("DeletedDate");
            if (property != null && property.PropertyType == typeof(DateTime?))
            {
                property.SetValue(entity, DateTime.Now);
                _context.Entry(entity).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Entity of type {typeof(T)} does not support soft delete.");
            }
        }

        public async Task<Pagination> GetCount(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, CancellationToken token = default)
        {
            IQueryable<T> query = _context.Set<T>();

            if (specification == null)
                specification = new DefaultSpecification<T>();

            var expression = specification.ToExpression();
            query = query.Where(expression);


            int totalItems = await query.CountAsync();

            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            Pagination pagination = new Pagination()
            {
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };

            return pagination;
        }

        public async Task<List<T>> GetList(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, CancellationToken token = default)
        {
            int skip = (pageNumber - 1) * pageSize;
            IQueryable<T> query = _context.Set<T>();

            if (specification == null)
                specification = new DefaultSpecification<T>();

            var expression = specification.ToExpression();
            query = query.Where(expression);
            query = query.OrderByDescending(item => item.UpdatedDate);

            query = query.Skip(skip).Take(pageSize);

            return await query.ToListAsync(token);
        }

        public async Task<T> GetOne(ISpecificationBase<T> specification, CancellationToken token = default)
        {
            if(specification == null) throw new ArgumentNullException(nameof(specification));

            var expression = specification.ToExpression();
            IQueryable<T> query = _context.Set<T>();
            query = query.Where(expression);

            return await query.FirstOrDefaultAsync(token);
        }

        public async Task<T> GetOne(int id, CancellationToken token = default)
        {
            var entity = await _context.Set<T>().FindAsync(id);
            if (entity == null)
            {
                throw new InvalidOperationException($"Entity of type {typeof(T)} with Id {id} not found");
            }

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
                    throw new InvalidOperationException($"Entity of type {typeof(T)} with Id {id} not found");
                }

                entity.UpdatedDate = DateTime.Now;
                _context.Entry(existingEntity).CurrentValues.SetValues(entity);
            }

            await _context.SaveChangesAsync();

            return entity;
        }
    }
}
