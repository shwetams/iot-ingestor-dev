using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;

namespace SampleDictionary
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    /// 
    internal class ServiceDetails
    {
        public string roleName { get; set; }
        public string inputEndpoint { get; set; }
        public string outputEndpoint { get; set; }
        public bool isActive { get; set; }
    }
    internal sealed class SampleDictionary : StatefulService
    {
        public SampleDictionary(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {

            return new ServiceReplicaListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.
            List<ServiceDetails> serviceDetails = new List<ServiceDetails>();

            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>("ServiceDetails");

            while (true)
            {
                
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "A_Role");

                    if (result.HasValue)
                    {
                        ServiceDetails svcDet = new ServiceDetails();
                        svcDet.inputEndpoint = "inputAQueue";
                        svcDet.outputEndpoint = "outputAQueue";
                        svcDet.roleName = "A_Role";
                        svcDet.isActive = true;
                        var aRole = JsonConvert.SerializeObject(svcDet);

                        await myDictionary.AddAsync(tx, "A_Role", aRole);
                        ServiceEventSource.Current.ServiceMessage(this.Context, "Create Dictionary Service A Role Value: {0}",
                        "Added A role value");

                    }
                    else
                    {
                        ServiceEventSource.Current.ServiceMessage(this.Context, "Create Dictionary Service A Role Value: {0}",
                        "Can also read A role value");

                    }

                    //ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                     //   result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    //await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
