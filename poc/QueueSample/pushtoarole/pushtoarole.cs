using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json;

namespace pushtoarole
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    /// 
    internal class MessageDetails
    {
        public string msg { get; set; }
        public string state { get; set; }
    }
    internal sealed class pushtoarole : StatelessService
    {
        public pushtoarole(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            long iterations = 0;
            string queue = "a_queue";
            string operation = "add";
            int port = 8088;
            string[] locations = { "Redmond", "Seattle","Portland","San Fransisco" };
            Random rnd = new Random();
            Random rndDeviceID = new Random();
            Random rndLocation = new Random();

            string serviceuri = "http://" + FabricRuntime.GetNodeContext().IPAddressOrFQDN + ":" + port.ToString() + "/rqueues";
             
            using (HttpClient httpClient = new HttpClient())
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var temp = rnd.NextDouble() * 20 + 10;
                    var deviceID = "deviceID" + rndDeviceID.Next();
                    var location = locations[rndLocation.Next(0, 4)];
                    var state = "to_process";
                    string msg =  "{\"deviceID\":\"" + deviceID + "\",\"temp\":" + temp + ",\"location\":\"" + location + "\"}";
                    //MessageDetails msgDet = new MessageDetails();
                    //msgDet.msg = msg;
                    //msgDet.state = "";
                    //var msgDetStr = JsonConvert.SerializeObject(msgDet);

                    string uri = serviceuri + "?" + "operation=" + operation + "&" + "queue=" + queue + "&" + "msg=" + msg;
                    
                    var resp = httpClient.GetStringAsync(uri);

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Sending to a queue-{0}", msg );

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
                //HttpClient httpClient = new HttpClient();
                
        }
    }
}
