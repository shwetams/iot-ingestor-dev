using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using StackExchange.Redis;
using System.Net.Http;
using System.Fabric.Description;

namespace ProcessMessage
{

    internal class HttpCommunicationListener : ICommunicationListener
    {

        public Task CloseAsync(CancellationToken cancellationToken)

        {

          //  this.processRequestsCancellation.Cancel();

//            this.httpListener.Close();

            return Task.FromResult(true);

        }

        public void Abort()
        {
            /*
            this.processRequestsCancellation.Cancel();

            this.httpListener.Abort();
            */
        }

        public Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            EndpointResourceDescription endpoint =
                 serviceContext.CodePackageActivationContext.GetEndpoint("WebEndpoint");

            string uriPrefix = $"{endpoint.Protocol}://+:{endpoint.Port}/myapp/";

            this.httpListener = new HttpListener();
            this.httpListener.Prefixes.Add(uriPrefix);
            this.httpListener.Start();

            string uriPublished = uriPrefix.Replace("+", FabricRuntime.GetNodeContext().IPAddressOrFQDN);

            return Task.FromResult(this.publishUri);

        }
    }

    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    /// 
    internal sealed class ProcessMessage : StatelessService
    {
        public ProcessMessage(StatelessServiceContext context)
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
            string[] locations = { "Redmond", "Seattle", "Portland", "San Fransisco" };
            Random rnd = new Random();
            Random rndDeviceID = new Random();
            Random rndLocation = new Random();
            ConnectionMultiplexer conn = ConnectionMultiplexer.Connect("devsgredis.redis.cache.windows.net:6380,password=,ssl=True,abortConnect=False");

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
                    string msg = "{\"deviceID\":\"" + deviceID + "\",\"temp\":" + temp + ",\"location\":\"" + location + "\"}";
                    //MessageDetails msgDet = new MessageDetails();
                    //msgDet.msg = msg;
                    //msgDet.state = "";
                    //var msgDetStr = JsonConvert.SerializeObject(msgDet);

                    string uri = serviceuri + "?" + "operation=" + operation + "&" + "queue=" + queue + "&" + "msg=" + msg;

                    var resp = httpClient.GetStringAsync(uri);


                    ServiceEventSource.Current.ServiceMessage(this.Context, "Sending to a queue-{0}", msg);

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                    if (conn.IsConnected)
                    {
                        var db = conn.GetDatabase();
                        var cnt = await db.HashIncrementAsync("numMsges", 1);
                        await conn.CloseAsync();
                    }

                }

            }
        }
    }
}
