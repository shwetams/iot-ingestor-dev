using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using StackExchange.Redis;
using System.Web.Http.Cors;

namespace DashboardAPI.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class ValuesController : ApiController
    {
        // GET api/values
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        [HttpGet]
        
        // GET api/values/5
        public string Get(string hashName)

        {
            string val = "Unable to fetch value";

            ConnectionMultiplexer conn = ConnectionMultiplexer.Connect("devsgredis.redis.cache.windows.net:6380,password=WCDjxjd0pgKcImyhsJ/IID0lnNtZNQu1aRUbsdv97io=,ssl=True,abortConnect=False");

            if (conn.IsConnected)
            {
                var db = conn.GetDatabase();
                val = db.HashValues(hashName)[0].ToString();
            }
            conn.CloseAsync();
            return val;
        }

        // POST api/values
        

        // PUT api/values/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        public void Delete(int id)
        {
        }
    }
}
