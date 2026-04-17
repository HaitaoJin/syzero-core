using System;
using System.Threading.Tasks;
using SyZero.Domain.Repository;
using SyZero.SqlSugar.DbContext;

namespace SyZero.SqlSugar
{
    /// <summary>
    /// SqlSugar 事务作用域
    /// </summary>
    public class SqlSugarTransactionScope : ITransactionScope
    {
        private readonly ISyZeroDbContext _dataContext;
        private bool _committed;
        private bool _disposed;

        public SqlSugarTransactionScope(ISyZeroDbContext dataContext)
        {
            _dataContext = dataContext;
        }

        public void Commit()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SqlSugarTransactionScope));
            if (_committed) return;
            _dataContext.CommitTran();
            _committed = true;
        }

        public async Task CommitAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SqlSugarTransactionScope));
            if (_committed) return;
            await _dataContext.Ado.CommitTranAsync();
            _committed = true;
        }

        public void Rollback()
        {
            if (_disposed) return;
            if (_committed) return;
            _dataContext.RollbackTran();
        }

        public async Task RollbackAsync()
        {
            if (_disposed) return;
            if (_committed) return;
            await _dataContext.Ado.RollbackTranAsync();
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (!_committed)
            {
                try { _dataContext.RollbackTran(); } catch { }
            }
            _disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            if (!_committed)
            {
                try { await _dataContext.Ado.RollbackTranAsync(); } catch { }
            }
            _disposed = true;
        }
    }

    public class UnitOfWork : IUnitOfWork
    {
        private ISyZeroDbContext dataContext;

        public UnitOfWork(ISyZeroDbContext _dataContext)
        {
            this.dataContext = _dataContext;
        }

        public void BeginTransaction()
        {
            this.dataContext.BeginTran();
        }

        public void CommitTransaction()
        {
            this.dataContext.CommitTran();
        }


        public void RollbackTransaction()
        {
            this.dataContext.RollbackTran();
        }

        public void DisposeTransaction()
        {
            // DbContext is scoped and disposed by DI. Avoid disposing it here,
            // otherwise repositories in the same request may see an already disposed context.
        }

        public async Task<ITransactionScope> BeginTransactionAsync()
        {
            await this.dataContext.Ado.BeginTranAsync();
            return new SqlSugarTransactionScope(this.dataContext);
        }

        public void ExecuteInTransaction(Action action)
        {
            try
            {
                BeginTransaction();
                action();
                CommitTransaction();
            }
            catch
            {
                RollbackTransaction();
                throw;
            }
        }

        public T ExecuteInTransaction<T>(Func<T> func)
        {
            try
            {
                BeginTransaction();
                var result = func();
                CommitTransaction();
                return result;
            }
            catch
            {
                RollbackTransaction();
                throw;
            }
        }

        public async Task ExecuteInTransactionAsync(Func<Task> func)
        {
            await using var scope = await BeginTransactionAsync();
            await func();
            await scope.CommitAsync();
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> func)
        {
            await using var scope = await BeginTransactionAsync();
            var result = await func();
            await scope.CommitAsync();
            return result;
        }
    }
}
