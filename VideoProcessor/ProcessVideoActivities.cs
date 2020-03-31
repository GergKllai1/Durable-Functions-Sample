using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid.Helpers.Mail;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoProcessor
{
    public static class ProcessVideoActivities
    {
        [FunctionName("A_GetTranscodeBitrates")]
        public static int[] GetTranscodeBitrates(
            [ActivityTrigger] object input,
            ILogger log)
        {
            return Environment.GetEnvironmentVariable("TranscodeBitRates")
                .Split(",").Select(int.Parse).ToArray();
        }

        [FunctionName("A_TranscodeVideo")]
        public static async Task<VideoFileInfo> TransocdeVideo(
            [ActivityTrigger] VideoFileInfo inputVideo,
            ILogger log)
        {

            log.LogInformation($"Transcoding {inputVideo.Location} to {inputVideo.BitRate}");
            // simulate doing the activity
            await Task.Delay(5000);

            string transcodedLocation =  
                $"{Path.GetFileNameWithoutExtension(inputVideo.Location)}-{inputVideo.BitRate}kbps.mp4";

            return new VideoFileInfo
            {
                Location = transcodedLocation,
                BitRate = inputVideo.BitRate
            };
        }

        [FunctionName("A_ExtractThumbnail")]
        public static async Task<string> ExtractThumbnail(
           [ActivityTrigger] string inputVideo,
           ILogger log)
        {

            log.LogInformation($"Extracting Thumbnail {inputVideo}");
            // simulate doing the activity
            await Task.Delay(5000);

            if (inputVideo.Contains("error"))
            {
                throw new InvalidOperationException("Could not extract thumbnail");
            }

            return "thumbnail.png";
        }

        [FunctionName("A_PrependIntro")]
        public static async Task<string> PrependIntro(
           [ActivityTrigger] string inputVideo,
           ILogger log)
        {

            log.LogInformation($"Appending intro to video {inputVideo}");
            // simulate doing the activity
            await Task.Delay(5000);

            return "withIntro.mp4";
        }

        [FunctionName("A_SendApprovalRequestEmail")]
        public static void SendApprovalRequestEmail(
            [ActivityTrigger] ApprovalInfo approvalInfo,
            [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage message,
            [Table("Approvals", Connection = "AzureWebJobsStorage")] out Approval approval,
            ILogger log)
        {
            string approvalCode = Guid.NewGuid().ToString("N");
            approval = new Approval
            {
                PartitionKey = "Approval",
                RowKey = approvalCode,
                OrchestrationId = approvalInfo.OrchestrationId
            };

            string host = Environment.GetEnvironmentVariable("Host");

            string functionAddress = $"{host}/api/SubmitVideoApproval/{approvalCode}";
            string approvedLink = functionAddress + "?result=Approved";
            string rejectedLink = functionAddress + "?result=Rejected";
            string emailBody =
                $"Please review {approvalInfo.VideoLocation}<br>"
                + $"<a href=\"{approvedLink}\">Approve</a><br>"
                + $"<a href=\"{rejectedLink}\">Reject</a>";

            message = new SendGridMessage();
            message.AddTo(Environment.GetEnvironmentVariable("ApproverEmail"));
            message.AddContent("text/html", emailBody);
            message.SetFrom(new EmailAddress(Environment.GetEnvironmentVariable("SenderEmail")));
            message.SetSubject("Approve this message");

        }

        [FunctionName("A_PublishVideo")]
        public static async Task PublishVideo(
            [ActivityTrigger] string inputVideo,
            ILogger log)
        {
            log.LogInformation($"Publishing {inputVideo}");

            await Task.Delay(1000);
        }

        [FunctionName("A_RejectVideo")]
        public static async Task RejectVideo(
           [ActivityTrigger] string inputVideo,
           ILogger log)
        {
            log.LogInformation($"Recejting {inputVideo}");

            await Task.Delay(1000);
        }

        [FunctionName("A_Cleanup")]
        public static async Task<string> Cleanup(
            [ActivityTrigger] string[] filesToCleanUp,
            ILogger log)
        {
            foreach (var file in filesToCleanUp.Where(f => f != null))
            {
                log.LogInformation($"Deleting {file}");
                // simulate doing the activity
                await Task.Delay(1000);
            }
            return "Cleaned up successfully";
        }
    }
}
