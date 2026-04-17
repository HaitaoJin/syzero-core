using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Domain.Entities;
using SyZero.Domain.Repository;
using SyZero.SqlSugar.DbContext;

namespace SyZero.SqlSugar.Repositories
{
    public class SqlSugarRepository<TEntity> : IRepository<TEntity>
        where TEntity : class, IEntity, new()
    {
        protected ISyZeroDbContext _dbContext;
        protected SimpleClient<TEntity> _dbSet;

        public SqlSugarRepository(ISyZeroDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _dbSet = _dbContext.GetSimpleClient<TEntity>();
        }

        #region Count
        public int Count(Expression<Func<TEntity, bool>> where)
        {
            return _dbSet.Count(where);
        }

        public async Task<int> CountAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbSet.CountAsync(where);
        }
        #endregion

        #region Insert
        public TEntity Add(TEntity entity)
        {
            _dbSet.Insert(entity);
            return entity;
        }

        public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _dbSet.InsertAsync(entity);
            return entity;
        }

        public int AddList(IQueryable<TEntity> entities)
        {
            var entityList = ToList(entities);
            if (entityList.Count == 0)
            {
                return 0;
            }

            return NormalizeAffectedCount(_dbSet.InsertRange(entityList), entityList.Count);
        }

        public async Task<int> AddListAsync(IQueryable<TEntity> entities, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entityList = ToList(entities);
            if (entityList.Count == 0)
            {
                return 0;
            }

            return NormalizeAffectedCount(await _dbSet.InsertRangeAsync(entityList), entityList.Count);
        }
        #endregion

        #region Delete
        public long Delete(long id)
        {
            return NormalizeAffectedRows(_dbSet.DeleteById(id));
        }

        public long Delete(Expression<Func<TEntity, bool>> where)
        {
            return NormalizeAffectedRows(_dbSet.Delete(where));
        }

        public async Task<long> DeleteAsync(long id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return NormalizeAffectedRows(await _dbSet.DeleteByIdAsync(id));
        }

        public async Task<long> DeleteAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return NormalizeAffectedRows(await _dbSet.DeleteAsync(where));
        }
        #endregion

        #region Select
        public IQueryable<TEntity> GetList()
        {
            return _dbSet.GetList().AsQueryable();
        }

        public IQueryable<TEntity> GetList(Expression<Func<TEntity, bool>> where)
        {
            return _dbSet.GetList(where).AsQueryable();
        }

        public Task<IQueryable<TEntity>> GetListAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetList());
        }

        public Task<IQueryable<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetList(where));
        }

        public TEntity GetModel(long id)
        {
            return _dbSet.GetById(id);
        }

        public async Task<TEntity> GetModelAsync(long id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbSet.GetByIdAsync(id);
        }

        public TEntity GetModel(Expression<Func<TEntity, bool>> where)
        {
            return _dbSet.GetSingle(where);
        }

        public async Task<TEntity> GetModelAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbSet.GetSingleAsync(where);
        }

        public IQueryable<TEntity> GetPaged(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, bool isDesc = false)
        {
            return _dbSet.GetPageList(
                p => true,
                new PageModel
                {
                    PageIndex = pageIndex,
                    PageSize = pageSize
                },
                sortBy,
                isDesc ? OrderByType.Desc : OrderByType.Asc).AsQueryable();
        }

        public Task<IQueryable<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, bool isDesc = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetPaged(pageIndex, pageSize, sortBy, isDesc));
        }

        public IQueryable<TEntity> GetPaged(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, Expression<Func<TEntity, bool>> where, bool isDesc = false)
        {
            return _dbSet.GetPageList(
                where,
                new PageModel
                {
                    PageIndex = pageIndex,
                    PageSize = pageSize
                },
                sortBy,
                isDesc ? OrderByType.Desc : OrderByType.Asc).AsQueryable();
        }

        public Task<IQueryable<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, Expression<Func<TEntity, bool>> where, bool isDesc = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetPaged(pageIndex, pageSize, sortBy, where, isDesc));
        }
        #endregion

        #region Update
        public long Update(TEntity entity)
        {
            return NormalizeAffectedRows(_dbSet.Update(entity));
        }

        public long Update(IQueryable<TEntity> entitys)
        {
            var entityList = ToList(entitys);
            if (entityList.Count == 0)
            {
                return 0;
            }

            return NormalizeAffectedRows(_dbSet.UpdateRange(entityList), entityList.Count);
        }

        public async Task<long> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return NormalizeAffectedRows(await _dbSet.UpdateAsync(entity));
        }

        public async Task<long> UpdateAsync(IQueryable<TEntity> entitys, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entityList = ToList(entitys);
            if (entityList.Count == 0)
            {
                return 0;
            }

            return NormalizeAffectedRows(await _dbSet.UpdateRangeAsync(entityList), entityList.Count);
        }
        #endregion

        private static List<TEntity> ToList(IQueryable<TEntity> entities)
        {
            return entities?.ToList() ?? new List<TEntity>();
        }

        private static int NormalizeAffectedCount(object result, int successCount)
        {
            return checked((int)NormalizeAffectedRows(result, successCount));
        }

        private static long NormalizeAffectedRows(object result, long successCount = 1)
        {
            return result switch
            {
                null => 0,
                bool success => success ? successCount : 0,
                int count => count,
                long count => count,
                _ => Convert.ToInt64(result)
            };
        }
    }
}
