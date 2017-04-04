using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Net.Http;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace aroleservice
{
    internal class MessageDetailsWrapper
    {
        string msg { get; set; }
    }

    internal class MessageDetails
    {
        public string msg { get; set; }
        public string state { get; set; }
    }
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class aroleservice : StatelessService
    {
        public aroleservice(StatelessServiceContext context)
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


            //                < Parameter Name = "OutputQueueName" Value = "b_queue" />
            //               
            //                  < Parameter Name = "OutputQueueServiceName" Value = "rqueues" />

            //                   < Parameter Name = "OutputQueueServicePort" Value = "8088" />

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

                //long iterations = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (HttpClient httpClient = new HttpClient())
                    {
                        string uri = input_serviceuri + "?" + "operation=" + input_operation + "&" + "queue=" + input_queue;
                        var resp = await httpClient.GetStringAsync(uri);
                        //var  r = resp.Result;
                       // var mDet = r.Content.ToString();
                        //resp.Result.ToString();
                        MessageDetails msgDet = new MessageDetails();
                        
                        //dynamic msgD = resp.Result.Substring(resp.Result);
                        //msgDet = msgD.msg;

                        ServiceEventSource.Current.ServiceMessage(this.Context, "Processing a role msg recieved -{0}", resp.ToString());
                        //MessageDetailsWrapper msgW = new MessageDetailsWrapper();
                       //     msgW = JsonConvert.DeserializeObject<MessageDetailsWrapper>(resp.Result.ToString());

                       //  msgDet = JsonConvert.DeserializeObject<MessageDetails>(resp.Result.ToString());

                        var msgStr = resp;
                        if (msgStr.Length <= 0)
                        { }
                        else
                        {
                            ServiceEventSource.Current.ServiceMessage(this.Context, "Processing a role msg de-serialized object msg= {0}", msgStr);
                            // if (msgDet.state != null)
                            // {
                            //     msgDet.state =  "a_processed";
                            // }
                            // else
                            // {
                            //     msgDet.state = "a_processed";
                            // }
                            //msgDet.msg = resp.Result.ToString();

                            ServiceEventSource.Current.ServiceMessage(this.Context, "Processing-{0}", msgStr);
                            if (isOutput)
                            {
                                // var msgString = JsonConvert.SerializeObject(msgDet);
                                string out_uri = output_serviceuri + "?" + "operation=" + output_operation + "&" + "queue=" + output_queue + "&" + "msg=" + msgStr;
                                var resp_out = await httpClient.GetStringAsync(out_uri);

                            }
                            else
                            {
                                string processed_msg = "msg=" + msgStr + " state=" + "a_processed";
                                ServiceEventSource.Current.ServiceMessage(this.Context, "Processed-{0}", processed_msg);
                            }
                            if (conn.IsConnected)
                            {
                                var db = conn.GetDatabase();
                                var cnt = await db.HashIncrementAsync("numMsgesA", 1);

                            }

                        }


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
