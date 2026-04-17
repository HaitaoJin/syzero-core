using SyZero.Example1.Core.Entities;
using SyZero.SqlSugar.Repositories;
using SyZero.SqlSugar.DbContext;

namespace SyZero.Example1.Core.Repository.Imp
{
    public class ExampleRepository : SqlSugarRepository<Example>, IExampleRepository
    {
        public ExampleRepository(ISyZeroDbContext dbContext)
            : base(dbContext)
        {
        }
    }
}
