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
    public partial class UserInfoSpecification : ISpecificationBase<UserInfo>
    {
        public string? UserName { get; set; }   
        public string? UserNameContains { get; set; }
        public string? Password { get; set; }

        public Expression<Func<UserInfo, bool>> ToExpression()
        {
            var predicate = PredicateBuilder.New<UserInfo>(true);
            predicate = predicate.And(p => p.DeletedDate == null);

            if(!string.IsNullOrEmpty(UserName))
            {
                predicate = predicate.And(p => p.UserName == UserName);
            }
            else if(!string.IsNullOrEmpty(UserNameContains))
            {
                predicate = predicate.And(p => p.UserName.Contains(UserNameContains));
            }

            if(!string.IsNullOrEmpty(Password))
            {
                predicate = predicate.And(p => p.Password == Password);
            }

            return predicate;
        }
    }
}
