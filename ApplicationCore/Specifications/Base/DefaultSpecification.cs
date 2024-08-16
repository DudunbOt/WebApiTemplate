using ApplicationCore.Entities;
using LinqKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Specifications
{
    public class DefaultSpecification<T> : ISpecificationBase<T> where T : EntityBase
    {
        public Expression<Func<T, bool>> ToExpression()
        {
            var predicate = PredicateBuilder.New<T>(true);

            predicate = predicate.And(p => p.DeletedDate == null);

            return predicate;
        }
    }
}
