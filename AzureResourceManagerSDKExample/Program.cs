using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure;

namespace AzureResourceManagerSDKExample
{
    internal class Program
    {
        private static readonly string ManagedIdentityQueryTemplate = $@"
            resources
            | where properties.clientId =~ '{{0}}' and type =~ 'microsoft.managedidentity/userassignedidentities'
            | project
                {nameof(ServicePrincipalDetails.Name)} = name,
                {nameof(ServicePrincipalDetails.ResourceGroup)} = resourceGroup,
                {nameof(ServicePrincipalDetails.SubscriptionId)} = subscriptionId,
                {nameof(ServicePrincipalDetails.ServicePrincipalId)} = properties.principalId,
                {nameof(ServicePrincipalDetails.ClientId)} = properties.clientId,
                {nameof(ServicePrincipalDetails.TenantId)} = tenantId
            | take 1";

        private const string TenantId = "771d663d-da6c-44bd-bc0a-e9a46bc4bfb6";
        private const string ManagedIdentityClienId = "962cbeaf-6fbd-476b-87e2-aa2d8ee1bacc";

        static async Task Main(string[] args)
        {
            var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                TenantId = TenantId
            });


            var client = new ArmClient(credential);

            var tenant = client.GetTenants().FirstOrDefault(t => t.Data.TenantId == Guid.Parse(TenantId));


            var query = string.Format(ManagedIdentityQueryTemplate, ManagedIdentityClienId);
            var queryContent = new ResourceQueryContent(query);

            var servicePrincipalResources = await tenant.GetResourcesAsync(queryContent);
            var servicePrincipal = servicePrincipalResources.Value.Data.ToObjectFromJson<ServicePrincipalDetails[]>().FirstOrDefault();


            var managedIdentityId = UserAssignedIdentityResource.CreateResourceIdentifier(servicePrincipal.SubscriptionId.ToString(), servicePrincipal.ResourceGroup, servicePrincipal.Name);
            
            var ficData = new FederatedIdentityCredentialData
            {
                Subject = "testSubject",
                IssuerUri = new Uri("https://test.com")
            };
            ficData.Audiences.Add("audience");

            var ficName = Guid.NewGuid().ToString();

            await client
                .GetUserAssignedIdentityResource(managedIdentityId)
                .GetFederatedIdentityCredentials()
                .CreateOrUpdateAsync(WaitUntil.Completed, ficName, ficData);

            var ficId = FederatedIdentityCredentialResource.CreateResourceIdentifier(servicePrincipal.SubscriptionId.ToString(), servicePrincipal.ResourceGroup, servicePrincipal.Name, ficName);
            await client
                .GetFederatedIdentityCredentialResource(ficId)
                .DeleteAsync(WaitUntil.Completed);
        }
    }

    internal class ServicePrincipalDetails
    {
        public string ResourceGroup { get; set; }
        public string Name { get; set; }
        public Guid ClientId { get; set; }
        public Guid ServicePrincipalId { get; set; }
        public Guid SubscriptionId { get; set; }
        public Guid TenantId { get; set; }
    }

}