using SyZero.Example2.Core.Entities;
using SyZero.SqlSugar.Repositories;
using SyZero.SqlSugar.DbContext;

namespace SyZero.Example2.Core.Repository.Imp
{
    public class ExampleRepository : SqlSugarRepository<Example>, IExampleRepository
    {
        public ExampleRepository(ISyZeroDbContext dbContext)
            : base(dbContext)
        {
        }
    }
}
