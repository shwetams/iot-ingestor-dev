using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;
using System.Net.Http;
using StackExchange.Redis;

namespace croleservice
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

    internal sealed class croleservice : StatelessService
    {
        public croleservice(StatelessServiceContext context)
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
            await Task.Delay(TimeSpan.FromSeconds(180), cancellationToken);

            try
            {

                ConnectionMultiplexer conn = ConnectionMultiplexer.Connect("devsgredis.redis.cache.windows.net:6380,password=WCDjxjd0pgKcImyhsJ/IID0lnNtZNQu1aRUbsdv97io=,ssl=True,abortConnect=False");
                var configurationPackage = Context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
                var input_queue = configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["InputQueueName"].Value;
                var input_svc = configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["InputQueueServiceName"].Value;
                var input_svc_port = configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["InputQueueServicePort"].Value;
                var isOutput = false;
                bool.TryParse(configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["IsOutputQueue"].Value, out isOutput);
                var output_queue = string.Empty;
                var output_svc = string.Empty;
                var output_svc_port = string.Empty;
                var input_operation = configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["InputQueueOperation"].Value;
                var output_operation = string.Empty;
                var msg = string.Empty;
                var output_serviceuri = string.Empty;
                var nodeIP = FabricRuntime.GetNodeContext().IPAddressOrFQDN;
                var input_serviceuri = "http://" + nodeIP + ":" + input_svc_port + "/" + input_svc;

                if (isOutput)
                {
                    output_queue = configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["OutputQueueName"].Value;
                    output_svc = configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["OutputQueueServiceName"].Value;
                    output_svc_port = configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["OutputQueueServicePort"].Value;
                    output_serviceuri = "http://" + nodeIP + ":" + input_svc_port + "/" + output_svc;
                    output_operation = configurationPackage.Settings.Sections["InputOutputConfigs"].Parameters["OutputQueueOperation"].Value;
                }

                //            long iterations = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (HttpClient httpClient = new HttpClient())
                    {
                        string uri = input_serviceuri + "?" + "operation=" + input_operation + "&" + "queue=" + input_queue;
                        var resp = await httpClient.GetStringAsync(uri);
                        //resp.Result.ToString();
                      //  MessageDetails msgDet = new MessageDetails();
                        ServiceEventSource.Current.ServiceMessage(this.Context, "Processing c role msg recieved -{0}", resp);
                        //  msgDet = JsonConvert.DeserializeObject<MessageDetails>(resp.Result.ToString());
                        var msgStr = resp;
                        if (msgStr.Length > 0)
                        {
                            ServiceEventSource.Current.ServiceMessage(this.Context, "Processing c role msg deserialized -{0}", msgStr);
                            //if (msgDet.state != null)
                            //{
                            //    msgDet.state = msgDet.state + " + c_processed";
                            //}
                            //else
                            //{
                            //    msgDet.state = "c_processed";
                            //}


                            ServiceEventSource.Current.ServiceMessage(this.Context, "Processing-{0}", msgStr);
                            if (isOutput)
                            {
                                var msgString = msgStr;
                                string out_uri = output_serviceuri + "?" + "operation=" + output_operation + "&" + "queue=" + output_queue + "&" + "msg=" + msgString;
                                var resp_out = await httpClient.GetStringAsync(out_uri);

                            }
                            else
                            {
                                string processed_msg = "msg=" + msgStr + " state=" + "c processed";
                                ServiceEventSource.Current.ServiceMessage(this.Context, "C Role Processed-{0}", processed_msg);
                            }
                            if (conn.IsConnected)
                            {
                                var db = conn.GetDatabase();
                                var cnt = await db.HashIncrementAsync("numMsgesC", 1);
                                await conn.CloseAsync();

                            }
                        }
                        else
                        { }
                        

                    }


                    await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "Error a role service-{0}", ex.StackTrace);

            }
        }
    }
}
