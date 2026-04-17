using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Generic;

namespace SyZero.MongoDB
{
    public class MongoContext : IMongoContext
    {
        public IMongoDatabase _db;

        [System.Obsolete]
        public MongoContext(IOptions<MongoOptions> options)

        {
            var services = new List<MongoServerAddress>();
            foreach (var item in options.Value.Services)
            {
                services.Add(new MongoServerAddress(item.Host, item.Port));
            }
            var settings = new MongoClientSettings
            {
                Servers = services
            };

            if (!string.IsNullOrWhiteSpace(options.Value.UserName) || !string.IsNullOrWhiteSpace(options.Value.Password))
            {
                settings.Credentials = new[]
                {
                    MongoCredential.CreateCredential(options.Value.DataBase, options.Value.UserName ?? string.Empty, options.Value.Password ?? string.Empty)
                };
            }

            var _mongoClient = new MongoClient(settings);
            _db = _mongoClient.GetDatabase(options.Value.DataBase);
        }

        public IMongoCollection<T> Set<T>()
        {
            return _db.GetCollection<T>(typeof(T).Name);
        }





        //   public IMongoCollection<T> Entities => _db.GetCollection<T>(typeof(T).ToString());

        //  public IMongoCollection<PermissionSystemLogs> PermissionSystemLogs => _db.GetCollection<PermissionSystemLogs>("PermissionSystemLogs");


    }
}
