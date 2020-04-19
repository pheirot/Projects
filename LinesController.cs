using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Dapper;
using System.Data;
using Dashboard.Domain;


namespace Dashboard.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class LinesController : ControllerBase
    {
        private readonly ILogger<LinesController> _logger;
        private readonly IConfiguration _configuration;

        public LinesController(ILogger<LinesController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        private DateTime GetCurrentTime()
        {
            var isDateTimeNow = _configuration.GetConnectionString("DateTime");
            DateTime dateTime;

            if (isDateTimeNow == "1")
                dateTime = DateTime.Now;
            else if (isDateTimeNow == "0")
                dateTime = new DateTime(2019, 9, 17);
            else
            {
                try { dateTime = DateTime.Parse(isDateTimeNow); }
                catch
                {
                    _logger.LogError("Не удалось распарсить дату, использую текущую");
                    dateTime = DateTime.Now;
                }
            }
            return dateTime;
        }
    
   
        [HttpGet]
        public dynamic GetLinesStatus()
        {
            //отсюда определяем isDowntime для каждой из 4-х линии id=16,20,24,28 
            //
            var connectionString = _configuration.GetConnectionString("MESDB");
            
            DateTime dateTime = GetCurrentTime();
            //DateTime dateTime = DateTime.Now;

            var status = new List<LinesStatus>();

            foreach (int i in new List<int> { 16,20,24,28})
            {
                using var connection = new SqlConnection(connectionString);

                var p = new DynamicParameters();

                p.Add("@ent_id", i);
                p.Add("@CurrentTime", dateTime);

                try
                {
                    status.Add(connection.Query("sp_SA_PAG_GetLinesStatus", p, commandType: CommandType.StoredProcedure).Select(Map).ToList()[0]);
                }
                catch (Exception e)
                {
                    _logger.LogInformation(e.Message);
                }
            }
            _logger.LogInformation(status.ToString());
            return status.ToArray();

            LinesStatus Map(dynamic data)
            {
                return new LinesStatus
                {
                    CurWoId = data.cur_wo_id,
                    IsDowntime = data.IsDowntime,
                    EventStartLocal = data.Event_start_local
                };
            }
        }

        [HttpGet]
        public dynamic GetCurrentShift()
        {

            var connectionString = _configuration.GetConnectionString("MESDB");

            var shift = new Shift();

            using var connection = new SqlConnection(connectionString);

            var p = new DynamicParameters();

            DateTime dateTime = GetCurrentTime();

            p.Add("@lang_id", 1049);
            p.Add("@Line_id", "Line_01");
            p.Add("@CurrentTime", dateTime);

            try
            {
                shift=connection.Query<Shift>("sp_SA_PAG_GetCurrentShift", p, commandType: CommandType.StoredProcedure).SingleOrDefault();
            }
            catch (Exception e)
            {
                _logger.LogInformation(e.Message);
            }
            
            return shift;
        }

        [HttpGet]
        public dynamic GetDownTimeForCurrentShift()
        {

            var connectionString = _configuration.GetConnectionString("MESDB");

            var downTime = new DownTime();

            DateTime dateTime = GetCurrentTime();

            using var connection = new SqlConnection(connectionString);

            var p = new DynamicParameters();

            p.Add("@lang_id", 1049);
            p.Add("@Line_id", "Line_01");
            p.Add("@CurrentTime", dateTime);

            try
            {
                downTime = connection.Query<DownTime>("sp_SA_PAG_GetDowntimesForCurrentShift", p, commandType: CommandType.StoredProcedure).ToList().Where(x => x.EntId == 20).FirstOrDefault();
            }
            catch (Exception e)
            {
                _logger.LogInformation(e.Message);
            }

            return downTime;
        }

        [HttpGet]
        public dynamic GetItemProd([FromQuery] string shiftstart , string shiftend)
        {

            var connectionString = _configuration.GetConnectionString("MESDB");

            var downTime = new List<ItemProd>();

            using var connection = new SqlConnection(connectionString);

            var p = new DynamicParameters();

            DateTime shiftstart1 = DateTime.Now.AddHours(-4);
            DateTime shiftend1 = DateTime.Now.AddHours(4); 

            try
            {
                shiftstart1 = DateTime.Parse(shiftstart);
                shiftend1 = DateTime.Parse(shiftend);
            }
            catch
            {
                _logger.LogError("не удалось распарсить shiftstart/shiftend в методе GetItemProd (первый раз может не сработать)");
            }

            _logger.LogError(DateTime.Now + " тик");

            p.Add("@ent_id", 20);
            p.Add("@ShiftStart", shiftstart1);
            p.Add("@ShiftEnd", shiftend1);

            try
            {
                downTime = connection.Query<ItemProd>("sp_SA_PAG_GetItemProd", p, commandType: CommandType.StoredProcedure).ToList();
            }
            catch (Exception e)
            {
                _logger.LogInformation(e.Message);
            }

            return downTime;
        }
    }
}