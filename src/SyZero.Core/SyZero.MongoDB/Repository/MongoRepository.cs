using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Domain.Entities;
using SyZero.Domain.Repository;

namespace SyZero.MongoDB
{
    public class MongoRepository<TEntity> : IRepository<TEntity> where TEntity : class, IEntity
    {
        private readonly IMongoContext _Context;
        private IMongoCollection<TEntity> _collection;
        public MongoRepository(IMongoContext context)
        {
            _Context = context;
            _collection = _Context.Set<TEntity>();
        }


        #region Insert
        public TEntity Add(TEntity entity)
        {
            _collection.InsertOne(entity);
            return entity;
        }

        public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return entity;
        }

        public int AddList(IQueryable<TEntity> entities)
        {
            _collection.InsertMany(entities);
            return entities.Count();
        }

        public async Task<int> AddListAsync(IQueryable<TEntity> entities, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _collection.InsertManyAsync(entities, cancellationToken: cancellationToken);
            return entities.Count();
        }
        #endregion

        #region Count
        public long Count(Expression<Func<TEntity, bool>> where)
        {
            return _collection.CountDocuments(where);
        }

        public async Task<long> CountAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _collection.CountDocumentsAsync(where, cancellationToken: cancellationToken);
        }
        #endregion

        #region Delete
        public long Delete(long id)
        {
            return _collection.DeleteOne(a => a.Id == id).DeletedCount;
        }

        public long Delete(Expression<Func<TEntity, bool>> where)
        {
            return _collection.DeleteOne(where).DeletedCount;
        }

        public async Task<long> DeleteAsync(long id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await _collection.DeleteOneAsync(a => a.Id == id, cancellationToken);
            return result.DeletedCount;
        }

        public async Task<long> DeleteAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await _collection.DeleteOneAsync(where, cancellationToken);
            return result.DeletedCount;
        }
        #endregion

        #region Select
        public IQueryable<TEntity> GetList()
        {
            return _collection.AsQueryable();
        }

        public IQueryable<TEntity> GetList(Expression<Func<TEntity, bool>> where)
        {
            return _collection.FindSync(where).ToEnumerable().AsQueryable();
        }

        public Task<IQueryable<TEntity>> GetListAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() => GetList(), cancellationToken);
        }

        public async Task<IQueryable<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            var reulst = await _collection.FindAsync(where, cancellationToken: cancellationToken);
            return reulst.ToEnumerable().AsQueryable();
        }

        public TEntity GetModel(long id)
        {
            return GetModel(a => a.Id == id);
        }

        public TEntity GetModel(Expression<Func<TEntity, bool>> where)
        {
            var result = _collection.FindSync(where);
            return result.FirstOrDefault();
        }



        public async Task<TEntity> GetModelAsync(long id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await GetModelAsync(a => a.Id == id, cancellationToken);
        }

        public async Task<TEntity> GetModelAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await _collection.FindAsync(where, cancellationToken: cancellationToken);
            return result.FirstOrDefault(cancellationToken);
        }

        public IQueryable<TEntity> GetPaged(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, bool isDesc = false)
        {
            SortDefinition<TEntity> sorts = new ObjectSortDefinition<TEntity>(new { });
            if (isDesc)
                sorts = sorts.Descending(sortBy);
            else
                sorts = sorts.Ascending(sortBy);
            return _collection.Find(p => true).Sort(sorts).Skip((pageIndex - 1) * pageSize).Limit(pageSize).ToEnumerable().AsQueryable();
        }

        public IQueryable<TEntity> GetPaged(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, Expression<Func<TEntity, bool>> where, bool isDesc = false)
        {
            SortDefinition<TEntity> sorts = new ObjectSortDefinition<TEntity>(new { });
            if (isDesc)
                sorts = sorts.Descending(sortBy);
            else
                sorts = sorts.Ascending(sortBy);
            return _collection.Find(where).Sort(sorts).Skip((pageIndex - 1) * pageSize).Limit(pageSize).ToEnumerable().AsQueryable();
        }

        public Task<IQueryable<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, bool isDesc = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() => GetPaged(pageIndex, pageSize, sortBy, isDesc), cancellationToken);
        }

        public Task<IQueryable<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, Expression<Func<TEntity, bool>> where, bool isDesc = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() => GetPaged(pageIndex, pageSize, sortBy, where, isDesc), cancellationToken);
        }


        #endregion

        #region Updata
        public long Update(TEntity entity)
        {
            var doc = entity.ToBsonDocument();
            var result = _collection.UpdateOne(Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id),
                new BsonDocumentUpdateDefinition<TEntity>(doc));
            return result.ModifiedCount;
        }

        public long Update(IQueryable<TEntity> entitys)
        {
            long reulst = 0;
            foreach (var item in entitys)
                reulst += Update(item);
            return reulst;
        }

        public async Task<long> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default(CancellationToken))
        {
            var doc = entity.ToBsonDocument();
            var result = await _collection.UpdateOneAsync(Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id),
                new BsonDocumentUpdateDefinition<TEntity>(doc), cancellationToken: cancellationToken);
            return result.ModifiedCount;
        }

        public async Task<long> UpdateAsync(IQueryable<TEntity> entitys, CancellationToken cancellationToken = default(CancellationToken))
        {
            long reulst = 0;
            foreach (var item in entitys)
                reulst += await UpdateAsync(item, cancellationToken);
            return reulst;
        }

        TEntity IBaseRepository<TEntity, long>.Add(TEntity entity)
        {
            return Add(entity);
        }

        Task<TEntity> IBaseRepository<TEntity, long>.AddAsync(TEntity entity, CancellationToken cancellationToken)
        {
            return AddAsync(entity, cancellationToken);
        }

        int IBaseRepository<TEntity, long>.AddList(IQueryable<TEntity> entities)
        {
            return AddList(entities);
        }

        Task<int> IBaseRepository<TEntity, long>.AddListAsync(IQueryable<TEntity> entities, CancellationToken cancellationToken)
        {
            return AddListAsync(entities, cancellationToken);
        }

        int IBaseRepository<TEntity, long>.Count(Expression<Func<TEntity, bool>> where)
        {
            return checked((int)Count(where));
        }

        async Task<int> IBaseRepository<TEntity, long>.CountAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken)
        {
            return checked((int)await CountAsync(where, cancellationToken));
        }

        IQueryable<TEntity> IBaseRepository<TEntity, long>.GetList()
        {
            return GetList();
        }

        IQueryable<TEntity> IBaseRepository<TEntity, long>.GetList(Expression<Func<TEntity, bool>> where)
        {
            return GetList(where);
        }

        Task<IQueryable<TEntity>> IBaseRepository<TEntity, long>.GetListAsync(CancellationToken cancellationToken)
        {
            return GetListAsync(cancellationToken);
        }

        Task<IQueryable<TEntity>> IBaseRepository<TEntity, long>.GetListAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken)
        {
            return GetListAsync(where, cancellationToken);
        }

        TEntity IBaseRepository<TEntity, long>.GetModel(long id)
        {
            return GetModel(id);
        }

        TEntity IBaseRepository<TEntity, long>.GetModel(Expression<Func<TEntity, bool>> where)
        {
            return GetModel(where);
        }

        Task<TEntity> IBaseRepository<TEntity, long>.GetModelAsync(long id, CancellationToken cancellationToken)
        {
            return GetModelAsync(id, cancellationToken);
        }

        Task<TEntity> IBaseRepository<TEntity, long>.GetModelAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken)
        {
            return GetModelAsync(where, cancellationToken);
        }

        IQueryable<TEntity> IBaseRepository<TEntity, long>.GetPaged(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, bool isDesc)
        {
            return GetPaged(pageIndex, pageSize, sortBy, isDesc);
        }

        IQueryable<TEntity> IBaseRepository<TEntity, long>.GetPaged(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, Expression<Func<TEntity, bool>> where, bool isDesc)
        {
            return GetPaged(pageIndex, pageSize, sortBy, where, isDesc);
        }

        Task<IQueryable<TEntity>> IBaseRepository<TEntity, long>.GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, bool isDesc, CancellationToken cancellationToken)
        {
            return GetPagedAsync(pageIndex, pageSize, sortBy, isDesc, cancellationToken);
        }

        Task<IQueryable<TEntity>> IBaseRepository<TEntity, long>.GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, object>> sortBy, Expression<Func<TEntity, bool>> where, bool isDesc, CancellationToken cancellationToken)
        {
            return GetPagedAsync(pageIndex, pageSize, sortBy, where, isDesc, cancellationToken);
        }


        #endregion

    }
}
