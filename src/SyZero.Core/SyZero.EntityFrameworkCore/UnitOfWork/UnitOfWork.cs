using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SyZero.Domain.Repository;

namespace SyZero.EntityFrameworkCore
{
    /// <summary>
    /// EF Core 事务作用域
    /// </summary>
    public class EfCoreTransactionScope : ITransactionScope
    {
        private readonly DbContext _dbContext;
        private readonly IDbContextTransaction _transaction;
        private readonly bool _ownsTransaction;
        private bool _committed;
        private bool _disposed;

        public EfCoreTransactionScope(DbContext dbContext, IDbContextTransaction transaction, bool ownsTransaction = true)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _ownsTransaction = ownsTransaction;
        }

        public void Commit()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EfCoreTransactionScope));
            if (_committed) return;
            _transaction.Commit();
            _committed = true;
        }

        public async Task CommitAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EfCoreTransactionScope));
            if (_committed) return;
            await _transaction.CommitAsync();
            _committed = true;
        }

        public void Rollback()
        {
            if (_disposed) return;
            if (_committed) return;
            _transaction.Rollback();
            _dbContext.ChangeTracker.Clear();
        }

        public async Task RollbackAsync()
        {
            if (_disposed) return;
            if (_committed) return;
            await _transaction.RollbackAsync();
            _dbContext.ChangeTracker.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_ownsTransaction && !_committed)
            {
                try
                {
                    _transaction.Rollback();
                    _dbContext.ChangeTracker.Clear();
                }
                catch { }
            }
            if (_ownsTransaction)
            {
                _transaction.Dispose();
            }
            _disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            if (_ownsTransaction && !_committed)
            {
                try
                {
                    await _transaction.RollbackAsync();
                    _dbContext.ChangeTracker.Clear();
                }
                catch { }
            }
            if (_ownsTransaction)
            {
                await _transaction.DisposeAsync();
            }
            _disposed = true;
        }
    }

    public class UnitOfWork : UnitOfWork<DbContext>
    {
        public UnitOfWork(DbContext dataContext) : base(dataContext)
        {
        }
    }

    public class UnitOfWork<TDbContext> : IUnitOfWork
        where TDbContext : DbContext
    {
        private readonly TDbContext dataContext;

        public UnitOfWork(TDbContext dataContext)
        {
            this.dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        }

        public void BeginTransaction()
        {
            if (dataContext.Database.CurrentTransaction == null)
            {
                dataContext.Database.BeginTransaction();
            }
        }

        public void CommitTransaction()
        {
            if (dataContext.Database.CurrentTransaction != null)
            {
                dataContext.SaveChanges();
                dataContext.Database.CurrentTransaction.Commit();
            }
        }

        public void RollbackTransaction()
        {
            if (dataContext.Database.CurrentTransaction != null)
            {
                dataContext.Database.CurrentTransaction.Rollback();
                dataContext.ChangeTracker.Clear();
            }
        }

        public void DisposeTransaction()
        {
            if (dataContext.Database.CurrentTransaction != null)
            {
                dataContext.Database.CurrentTransaction.Dispose();
            }
        }

        public async Task<ITransactionScope> BeginTransactionAsync()
        {
            var transaction = await dataContext.Database.BeginTransactionAsync();
            return new EfCoreTransactionScope(dataContext, transaction);
        }

        public void ExecuteInTransaction(Action action)
        {
            using var transaction = dataContext.Database.BeginTransaction();
            try
            {
                action();
                dataContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                dataContext.ChangeTracker.Clear();
                throw;
            }
        }

        public T ExecuteInTransaction<T>(Func<T> func)
        {
            using var transaction = dataContext.Database.BeginTransaction();
            try
            {
                var result = func();
                dataContext.SaveChanges();
                transaction.Commit();
                return result;
            }
            catch
            {
                transaction.Rollback();
                dataContext.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task ExecuteInTransactionAsync(Func<Task> func)
        {
            await using var scope = await BeginTransactionAsync();
            await func();
            await dataContext.SaveChangesAsync();
            await scope.CommitAsync();
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> func)
        {
            await using var scope = await BeginTransactionAsync();
            var result = await func();
            await dataContext.SaveChangesAsync();
            await scope.CommitAsync();
            return result;
        }
    }
}
