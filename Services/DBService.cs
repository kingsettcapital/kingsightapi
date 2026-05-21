using kingsightapi.Entities;

namespace kingsightapi.Services
{
    public interface IDBService : IDisposable
    {
        string GetConnectionString();
    }
    public class DBService : IDBService
    {
        private readonly string _connectionString;
        private bool disposedValue;
        public DBService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("FabricDataWarehouse") 
                ?? throw new InvalidOperationException("Connection string 'FabricDataWarehouse' not found.");
        }
        public string GetConnectionString()
        {
            return _connectionString;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}   