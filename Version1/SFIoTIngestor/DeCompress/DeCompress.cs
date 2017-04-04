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
using System.IO.Compression;
using System.IO;
using System.Text;

namespace DeCompress
{
    /// https://github.com/Microsoft/iot-samples/blob/master/DecompressShred/csharp/DecompShred.cs - Extracted the deomcpress code from William's DeompressShred
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    /// 
    struct DecompressedMessage
    {
        public string decompressedMessage;
        public bool isDecompressed;
    }
    internal sealed class DeCompress : StatelessService
    {
        public DeCompress(StatelessServiceContext context)
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



        static DecompressedMessage DecompressMessage(string compressedMessage)

        {

            DecompressedMessage msg = new DecompressedMessage();
            // this string compression code is lifted & modified from 

            // http://madskristensen.net/post/Compress-and-decompress-strings-in-C
            try
            {
                var gzBuffer = Convert.FromBase64String(compressedMessage);

                using (MemoryStream ms = new MemoryStream())

                {

                    var msgLength = BitConverter.ToInt32(gzBuffer, 0);

                    ms.Write(gzBuffer, 4, gzBuffer.Length - 4);



                    var buffer = new byte[msgLength];



                    ms.Position = 0;

                    using (var zip = new GZipStream(ms, CompressionMode.Decompress))

                    {

                        zip.Read(buffer, 0, buffer.Length);

                    }


                    msg.decompressedMessage = Encoding.UTF8.GetString(buffer);
                    msg.isDecompressed = true;
                    

                }
            }
            catch (Exception ex)
            {
                msg.isDecompressed = false;
                msg.decompressedMessage = "Message : " + ex.Message + " StackTrace : " + ex.StackTrace;

            }

            return msg;

        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

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
                        //MessageDetails msgDet = new MessageDetails();

                        //dynamic msgD = resp.Result.Substring(resp.Result);
                        //msgDet = msgD.msg;

                        //ServiceEventSource.Current.ServiceMessage(this.Context, "Processing Decompressing msg recieved -{0}", resp.ToString());
                        //MessageDetailsWrapper msgW = new MessageDetailsWrapper();
                        //     msgW = JsonConvert.DeserializeObject<MessageDetailsWrapper>(resp.Result.ToString());

                        //  msgDet = JsonConvert.DeserializeObject<MessageDetails>(resp.Result.ToString());

                        var msgStr = resp;
                        if (msgStr.Length <= 0)
                        { }
                        else
                        {
                            // ServiceEventSource.Current.ServiceMessage(this.Context, "Processing a role msg de-serialized object msg= {0}", msgStr);

                            // if (msgDet.state != null)
                            // {
                            //     msgDet.state =  "a_processed";
                            // }
                            // else
                            // {
                            //     msgDet.state = "a_processed";
                            // }
                            //msgDet.msg = resp.Result.ToString();
                            var decompressedMsg = DecompressMessage(msgStr);
                            if (decompressedMsg.isDecompressed)
                            {

                                Dictionary<string, string> msgParam = new Dictionary<string, string>();
                                msgParam.Add("operation", output_operation);
                                msgParam.Add("queue", output_queue);
                                msgParam.Add("msg", decompressedMsg.decompressedMessage);                                
                                HttpContent msgContent = new FormUrlEncodedContent(msgParam);                               
                                
                                if (isOutput)
                                {
                                    // var msgString = JsonConvert.SerializeObject(msgDet);
                                    string out_uri = output_serviceuri;
                                    var resp_out = await httpClient.PostAsync(out_uri, msgContent);

                                }
                                else
                                {
                                    string processed_msg = "msg=" + msgStr + " state=" + "Decompressed";
                                    ServiceEventSource.Current.ServiceMessage(this.Context, "Processed-{0}", processed_msg);
                                }
                                if (conn.IsConnected)
                                {
                                    var db = conn.GetDatabase();
                                    var cnt = await db.HashIncrementAsync("numDecompressedMsgs", 1);

                                }

                            }
                            else
                            {

                                /// Implement this later - send to error queue
                            }
                            //ServiceEventSource.Current.ServiceMessage(this.Context, "Processing-{0}", msgStr);
                            

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
