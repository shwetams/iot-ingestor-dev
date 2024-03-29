﻿using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Net;
using System.Fabric.Description;
using System.Text;
using System.IO;
using System.Web;


namespace ProcessingQueues
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    /// 
    internal class ReadMyMessageListener : ICommunicationListener
    {
        private readonly HttpListener httpListener;
        private readonly Func<HttpListenerContext, CancellationToken, Task> processRequest;
        private readonly CancellationTokenSource processRequestsCancellation = new CancellationTokenSource();
        private readonly string publishUri;

        public ReadMyMessageListener(string uriPrefix, string uriPublished, Func<HttpListenerContext, CancellationToken, Task> processRequest)
        {

            this.publishUri = uriPublished;

            this.processRequest = processRequest;

            this.httpListener = new HttpListener();

            this.httpListener.Prefixes.Add(uriPrefix);

        }

        public void Abort()

        {

            this.processRequestsCancellation.Cancel();

            this.httpListener.Abort();

        }


        public Task CloseAsync(CancellationToken cancellationToken)

        {

            this.processRequestsCancellation.Cancel();

            this.httpListener.Close();

            return Task.FromResult(true);

        }

        public Task<string> OpenAsync(CancellationToken cancellationToken)

        {






            this.httpListener.Start();





            var openTask = this.ProcessRequestsAsync(this.processRequestsCancellation.Token);



            return Task.FromResult(this.publishUri);

        }


        private async Task ProcessRequestsAsync(CancellationToken processRequests)

        {

            while (!processRequests.IsCancellationRequested)

            {

                var request = await this.httpListener.GetContextAsync();



                // The ContinueWith forces rethrowing the exception if the task fails.

                Task requestTask =

                    this.processRequest(request, this.processRequestsCancellation.Token)

                        .ContinueWith(

                            async t => await t /* Rethrow unhandled exception */,

                            TaskContinuationOptions.OnlyOnFaulted);

            }

        }

    }

    internal sealed class ProcessingQueues : StatefulService
    {
        public const string servicename = "r_queue";


        public ProcessingQueues(StatefulServiceContext context)
            : base(context)
        { }

        private ICommunicationListener CreateInternalListener(ServiceContext context)

        {


            EndpointResourceDescription serviceEndpoint = context.CodePackageActivationContext.GetEndpoint("ServiceEndPoint");

            int port = serviceEndpoint.Port;

            //          var internalEndpoint = context.CodePackageActivationContext.GetEndpoint("ProcessingServiceEndpoint");

            var uriPrefix = $"{"http"}://+:{port}/";



            var nodeIP = FabricRuntime.GetNodeContext().IPAddressOrFQDN;



            var uriPublished = uriPrefix.Replace("+", nodeIP);
            uriPublished = uriPublished + "rqueues";
            ServiceEventSource.Current.ServiceMessage(this.Context, "Uri Prefix : " + uriPrefix + " , Uri Published : " + uriPublished);

            return new ReadMyMessageListener(uriPrefix, uriPublished, this.ProcessInternalRequest);

        }


        private async Task ProcessInternalRequest(HttpListenerContext context, CancellationToken cancelRequest)

        {


            string output = string.Empty;

            
            

            //ServiceEventSource.Current.ServiceMessage(this.Context, "Processing queue - Receieved message for queue -- {0} ", queue);

            try

            {
                var operation = string.Empty;
                var queue = string.Empty;
                var msg = string.Empty;
                var myQueue = await this.StateManager.GetOrAddAsync<IReliableQueue<string>>(queue);

                if (context.Request.HttpMethod.ToLower() == "post")
                {
                    var postContent = string.Empty;
                    using (var reader = new StreamReader(context.Request.InputStream,
                                                            context.Request.ContentEncoding))
                    {
                        postContent = reader.ReadToEnd();
                    }

                    // Parse Post Content
                    //operation=""&queue=""&msg=""
                    var operationStartIndex = postContent.IndexOf("operation=") + ("operation=").Length;
                    var operationEndIndex = postContent.IndexOf("&queue=");
                    var queueStartIndex = postContent.IndexOf("&queue=") + ("&queue=").Length;
                    var queueEndIndex = postContent.IndexOf("&msg=");
                    var msgStartIndex = postContent.IndexOf("&msg=") + ("&msg=").Length;
                    operation = postContent.Substring(operationStartIndex, operationEndIndex - operationStartIndex);
                    queue = postContent.Substring(queueStartIndex, queueEndIndex - queueStartIndex);
                    msg = postContent.Substring(msgStartIndex);
                    
                }
                if(context.Request.HttpMethod.ToLower() == "get")
                {
                    operation = context.Request.QueryString["operation"];
                    queue = context.Request.QueryString["queue"];
                    msg = context.Request.QueryString["msg"];
                }
                


                if (operation.ToLower().Trim() == "add")

                {
                    
                    using (var tx = this.StateManager.CreateTransaction())
                    {


                        await myQueue.EnqueueAsync(tx, msg);
                        ServiceEventSource.Current.ServiceMessage(this.Context, "Inserting into queue: -- {0}", queue);

                        await tx.CommitAsync();
                    }


                    //var subject = context.Request.QueryString["subject"];

                    // await this.SetTweetSubject(subject);

                    output = "{\"status\":true}";

                }

                else

                {

                    if (operation == "get")

                    {
                        using (var tx = this.StateManager.CreateTransaction())
                        {
                             msg = myQueue.TryDequeueAsync(tx).Result.Value;
                            // MessageDetails msgDet = new MessageDetails();
                            // msgDet.msg = msg.ToString();
                            // msgDet.state = "";
                            if (msg != null)
                            {
                                output = msg.ToString();
                            }
                            else
                            {
                                output = "";
                            }


                            await tx.CommitAsync();
                        }
                    }

                }

            }

            catch (Exception ex)

            {

                output = "{\"err\":\"" + ex.StackTrace + "\"}";

            }



            using (var response = context.Response)

            {

                var outBytes = Encoding.UTF8.GetBytes(output);

                response.OutputStream.Write(outBytes, 0, outBytes.Length);

            }

        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {

            return new ServiceReplicaListener[] { new ServiceReplicaListener(this.CreateInternalListener) };
        }




        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>        

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.
            /*
            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            } */
        }
    }
}
