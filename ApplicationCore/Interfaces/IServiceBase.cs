using ApplicationCore.Entities;
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
        Task<Pagination> GetCount(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, CancellationToken token = default);
        Task<List<T>> GetList(ISpecificationBase<T>? specification = null, int pageNumber = 1, int pageSize = 10, CancellationToken token = default);
        Task<T> GetOne(int id, CancellationToken token = default);
        Task<T> GetOne(ISpecificationBase<T> specification, CancellationToken token = default);
        Task<T> Upsert(object model, int id = 0, CancellationToken token = default);
        Task Delete(int id, CancellationToken token = default);
    }
}
