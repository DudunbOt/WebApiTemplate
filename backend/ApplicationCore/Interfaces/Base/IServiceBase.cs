using ApplicationCore.Entities;
using ApplicationCore.Entities.Base;
using ApplicationCore.Specifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Interfaces
{
    public interface IServiceBase<T> where T : class
    {
        Task<Pagination> GetCount(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, List<FilterDescriptor>? filterDescriptors = null, CancellationToken token = default);
        Task<List<T>> GetList(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, List<SortDescriptor>? sortDescriptors = null, List<FilterDescriptor>? filterDescriptors = null, List<string>? includes = null, CancellationToken token = default);
        Task<T> GetOne(int id, CancellationToken token = default, List<string>? includes = null);
        Task<T> GetOne(ISpecificationBase<T> specification, CancellationToken token = default, List<string>? includes = null);
        Task<T> Upsert(T entity, int id = 0, CancellationToken token = default, bool commit = true);
        Task Delete(int id, CancellationToken token = default);
    }
}
