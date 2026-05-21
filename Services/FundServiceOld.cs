//using Microsoft.Extensions.Logging;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using kingsightapi.Entities;
//using Microsoft.Data.SqlClient;
//using System.Data;


//namespace kingsightapi.Services
//{
//    public interface IFundService
//    {
//        Task<List<FundDto>> GetAllAsync();
//        //Task<FundDto?> GetByKeyAsync(int fundKey);
//    }
//    public class FundService : IFundService
//    {
//        private readonly IDBService _db;
//        private readonly ILogger<FundService> _logger;

//        public FundService(IDBService db, ILogger<FundService> logger) 
//        {
//            _db = db;
//            _logger = logger;
//        }

//        public async Task<List<FundDto>> GetAllAsync()
//        {
//            var results = new List<FundDto>();
//            const string sql = @" select fund_key,fund_id, fund_code,fund_name,fund_type_name,fund_strategy_name,
//                                    js_fund_name,is_sidecar,fund_start_date
//                                    from wh_enterprise_gold.dbo.dim_fund
//                                    Where getdate() between valid_from and ISNULL(valid_to,getdate()) 
//                                    Order by 1 ";
//            try
//            {
//                await using var conn = new SqlConnection(_db.GetConnectionString());
//                await conn.OpenAsync();

//                await using var cmd = conn.CreateCommand();
//                cmd.CommandText = sql;
//                cmd.CommandType = CommandType.Text;

//                await using var reader = await cmd.ExecuteReaderAsync();
//                while (await reader.ReadAsync())
//                {
//                    var dto = new FundDto();
//                    dto.FundKey = Convert.ToInt32(reader["fund_key"]);
//                    dto.FundId = Convert.ToInt32(reader["fund_id"]);
//                    dto.FundCode = reader["fund_code"] == DBNull.Value ? null : reader["fund_code"].ToString();
//                    dto.FundName = reader["fund_name"] == DBNull.Value ? null : reader["fund_name"].ToString();
//                    dto.FundType = reader["fund_type_name"] == DBNull.Value ? null : reader["fund_type_name"].ToString();
//                    dto.FundStrat = reader["fund_strategy_name"] == DBNull.Value ? null : reader["fund_strategy_name"].ToString();
//                    dto.FundStart = reader["fund_start_date"] == DBNull.Value ? null : (DateTime?)reader["fund_start_date"];
//                    results.Add(dto);
//                }

//                return results; 
//            }
//            catch (OperationCanceledException)
//            {
//                _logger.LogInformation("GetAllAsync cancelled");
//                throw;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error in FundService.GetAllAsync");
//                throw;
//            }
//        }

//        //public async Task<FundDto?> GetByKeyAsync(int fundKey)
//        //{
//        //    try
//        //    {
//        //        return await _db.Funds
//        //            .AsNoTracking()
//        //            .Where(f => f.FundKey == fundKey)
//        //            .Select(f => new FundDto
//        //            {
//        //                FundKey = f.FundKey,
//        //                FundId = f.FundId,
//        //                FundCode = f.FundCode,
//        //                FundName = f.FundName,
//        //                FundType = f.FundType,
//        //                FundStrat = f.FundStrat,
//        //                FundStart = f.FundStart,
//        //                ValidFrom = f.ValidFrom,
//        //                ValidTo = f.ValidTo,
//        //                IsCurrent = f.IsCurrent,
//        //                CreatedDate = f.CreatedDate
//        //            })
//        //            .FirstOrDefaultAsync(cancellationToken);
//        //    }
//        //    catch (OperationCanceledException)
//        //    {
//        //        _logger.LogInformation("GetByKeyAsync cancelled for {FundKey}", fundKey);
//        //        throw;
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        _logger.LogError(ex, "Error in FundService.GetByKeyAsync for {FundKey}", fundKey);
//        //        throw;
//        //    }
//        //}
//    }
//}