using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace VideoProcessor
{
    public static class ProcessVideoStarter
    {
        [FunctionName("ProcessVideoStarter")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string video = req.Query["video"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            video = video ?? data?.video;

            if (video == null)
            {
                return new BadRequestObjectResult("Please pass in a video!");
            }

            // Starts the orchestration
            var orchestrationId = await starter.StartNewAsync("O_ProcessVideo", Guid.NewGuid().ToString(), video);

            // Returns the RESTful response
            return starter.CreateCheckStatusResponse(req, orchestrationId);
        }

        // Orchestration to be called by the email
        [FunctionName("SubmitVideoApproval")]
        public static async Task<IActionResult> SubmitVideoApproval(
            [HttpTrigger(AuthorizationLevel.Anonymous, 
            "get", Route = "SubmitVideoApproval/{id}")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            [Table("Approvals", "Approval", "{id}", 
            Connection = "AzureWebJobsStorage")] Approval approval,
            ILogger log)
        {
            string result = req.Query["result"];
            if (result == null)
            {
                return new BadRequestObjectResult("Need an approval result");
            }
            log.LogInformation($"Sending approval result to {approval.OrchestrationId} of {result}");

            // send the ApprovalResult external event to this orchestration
            await client.RaiseEventAsync(approval.OrchestrationId, "ApprovalResult", result);

            return new OkResult();
        }

        // Ethernal orchestration sample, this task will repeat itself until termanited by the
        // durable functions API, using the termination link returned by the response
        [FunctionName("StartPeriodicTask")]
        public static async Task<IActionResult> startPeriodicTask(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var instanceId = await client.StartNewAsync("O_PeriodicTask", Guid.NewGuid().ToString(), 0);
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
