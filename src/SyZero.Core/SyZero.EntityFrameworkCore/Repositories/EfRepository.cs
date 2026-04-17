using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SyZero.Domain.Entities;
using SyZero.Domain.Repository;

namespace SyZero.EntityFrameworkCore.Repositories
{
    public class EfRepository<TEntity> : EfRepository<DbContext, TEntity>
        where TEntity : class, IEntity
    {
        public EfRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }

    public class EfRepository<TDbContext, TEntity> : IRepository<TEntity>
        where TEntity : class, IEntity
        where TDbContext : DbContext
    {
        protected readonly TDbContext _dbContext;
        protected readonly DbSet<TEntity> _dbSet;

        public EfRepository(TDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _dbSet = _dbContext.Set<TEntity>();
        }

        #region Count
        public int Count(Expression<Func<TEntity, bool>> where)
        {
            return checked((int)_dbSet.LongCount(where));
        }

        public async Task<int> CountAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return checked((int)await _dbSet.LongCountAsync(where, cancellationToken));
        }
        #endregion

        #region Insert
        public TEntity Add(TEntity entity)
        {
            var result = _dbSet.Add(entity);
            SaveChangesIfNeeded();
            return result.Entity;
        }

        public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var newEntity = (await _dbSet.AddAsync(entity, cancellationToken)).Entity;
            await SaveChangesIfNeededAsync(cancellationToken);
            return newEntity;
        }

        public int AddList(IQueryable<TEntity> entities)
        {
            var entityList = ToList(entities);
            if (entityList.Count == 0)
            {
                return 0;
            }

            _dbSet.AddRange(entityList);
            SaveChangesIfNeeded();
            return entityList.Count;
        }

        public async Task<int> AddListAsync(IQueryable<TEntity> entities, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entityList = ToList(entities);
            if (entityList.Count == 0)
            {
                return 0;
            }

            await _dbSet.AddRangeAsync(entityList, cancellationToken);
            await SaveChangesIfNeededAsync(cancellationToken);
            return entityList.Count;
        }
        #endregion

        #region Delete
        public long Delete(long id)
        {
            var entity = GetModel(id);
            if (entity == null)
            {
                return 0;
            }

            _dbSet.Remove(entity);
            SaveChangesIfNeeded();
            return 1;
        }

        public long Delete(Expression<Func<TEntity, bool>> where)
        {
            var entities = _dbSet.Where(where).ToList();
            if (entities.Count == 0)
            {
                return 0;
            }

            _dbSet.RemoveRange(entities);
            SaveChangesIfNeeded();
            return entities.Count;
        }

        public async Task<long> DeleteAsync(long id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entity = await GetModelAsync(id, cancellationToken);
            if (entity == null)
            {
                return 0;
            }

            _dbSet.Remove(entity);
            await SaveChangesIfNeededAsync(cancellationToken);
            return 1;
        }

        public async Task<long> DeleteAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entities = await _dbSet.Where(where).ToListAsync(cancellationToken);
            if (entities.Count == 0)
            {
                return 0;
            }

            _dbSet.RemoveRange(entities);
            await SaveChangesIfNeededAsync(cancellationToken);
            return entities.Count;
        }
        #endregion

        #region Select
        public IQueryable<TEntity> GetList()
        {
            return _dbSet.AsQueryable();
        }

        public IQueryable<TEntity> GetList(Expression<Func<TEntity, bool>> where)
        {
            return _dbSet.Where(where);
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
            return _dbSet.Find(id);
        }

        public async Task<TEntity> GetModelAsync(long id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        }

        public TEntity GetModel(Expression<Func<TEntity, bool>> where)
        {
            return _dbSet.FirstOrDefault(where);
        }

        public async Task<TEntity> GetModelAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbSet.FirstOrDefaultAsync(where, cancellationToken);
        }

        public IQueryable<TEntity> GetPaged(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, bool isDesc = false)
        {
            if (isDesc)
            {
                return _dbSet.OrderByDescending(sortBy).Skip((pageIndex - 1) * pageSize).Take(pageSize);
            }

            return _dbSet.OrderBy(sortBy).Skip((pageIndex - 1) * pageSize).Take(pageSize);
        }

        public Task<IQueryable<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, bool isDesc = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetPaged(pageIndex, pageSize, sortBy, isDesc));
        }

        public IQueryable<TEntity> GetPaged(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, Expression<Func<TEntity, bool>> where, bool isDesc = false)
        {
            if (isDesc)
            {
                return _dbSet.Where(where).OrderByDescending(sortBy).Skip((pageIndex - 1) * pageSize).Take(pageSize);
            }

            return _dbSet.Where(where).OrderBy(sortBy).Skip((pageIndex - 1) * pageSize).Take(pageSize);
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
            _dbSet.Update(entity);
            SaveChangesIfNeeded();
            return 1;
        }

        public long Update(IQueryable<TEntity> entitys)
        {
            var entityList = ToList(entitys);
            if (entityList.Count == 0)
            {
                return 0;
            }

            _dbSet.UpdateRange(entityList);
            SaveChangesIfNeeded();
            return entityList.Count;
        }

        public async Task<long> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _dbSet.Update(entity);
            await SaveChangesIfNeededAsync(cancellationToken);
            return 1;
        }

        public async Task<long> UpdateAsync(IQueryable<TEntity> entitys, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entityList = ToList(entitys);
            if (entityList.Count == 0)
            {
                return 0;
            }

            _dbSet.UpdateRange(entityList);
            await SaveChangesIfNeededAsync(cancellationToken);
            return entityList.Count;
        }
        #endregion

        private void SaveChangesIfNeeded()
        {
            if (_dbContext.Database.CurrentTransaction == null)
            {
                _dbContext.SaveChanges();
            }
        }

        private async Task SaveChangesIfNeededAsync(CancellationToken cancellationToken)
        {
            if (_dbContext.Database.CurrentTransaction == null)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        private static List<TEntity> ToList(IQueryable<TEntity> entities)
        {
            return entities?.ToList() ?? new List<TEntity>();
        }
    }
}
