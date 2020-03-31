using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace VideoProcessor
{
    public static class ProcessVideoOrchestrators
    {
        [FunctionName("O_ProcessVideo")]
        public static async Task<object> ProcessVideo(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
            ILogger log)
        {
            var videoLocation = ctx.GetInput<string>();

            if (!ctx.IsReplaying)
            {
                log.LogInformation("About to call transcode video activity");
            }

            string transcodedLocation = null;
            string thumbnailLocation = null;
            string withIntroLocation = null;
            string approvalResult = "Unknown";

            try
            {
                var transcodeResults =
                    await ctx.CallSubOrchestratorAsync<VideoFileInfo[]>("O_TranscodeVideo", videoLocation);
                transcodedLocation =
                  transcodeResults
                  .OrderByDescending(r => r.BitRate)
                  .Select(r => r.Location)
                  .FirstOrDefault();

                if (!ctx.IsReplaying)
                {
                    log.LogInformation("About to call extract thumbnail activity");
                }

                thumbnailLocation = await ctx.CallActivityWithRetryAsync<string>("A_ExtractThumbnail",
                    new RetryOptions(TimeSpan.FromSeconds(3), 4)
                    {
                        Handle = ex => ex is InvalidOperationException
                    },
                    transcodedLocation);

                if (!ctx.IsReplaying)
                {
                    log.LogInformation("About to call prepend intro activity");
                }

                withIntroLocation = await ctx.CallActivityAsync<string>("A_PrependIntro", transcodedLocation);

                await ctx.CallActivityAsync("A_SendApprovalRequestEmail", new ApprovalInfo()
                {
                    OrchestrationId = ctx.InstanceId,
                    VideoLocation = withIntroLocation
                });

                approvalResult = await ctx.WaitForExternalEvent<string>("ApprovalResult");

                if (approvalResult == "Approved")
                {
                    await ctx.CallActivityAsync("A_PublishVideo", withIntroLocation);
                }
                else
                {
                    await ctx.CallActivityAsync("A_RejectVideo", withIntroLocation);
                }

                return new
                {
                    Transcoded = transcodedLocation,
                    ThumbNail = thumbnailLocation,
                    WithIntro = withIntroLocation,
                    ApprovalResult = approvalResult
                };

            }
            catch (Exception e)
            {
                if (!ctx.IsReplaying)
                {
                    log.LogInformation($"Caught an error from an activity: {e.Message}");
                }

                await ctx.CallActivityAsync<string>("A_Cleanup",
                        new[] { transcodedLocation, thumbnailLocation, withIntroLocation });

                return new
                {
                    Error = "Failed to process uploaded video",
                    Message = e.Message
                };
            }
        }

        [FunctionName("O_TranscodeVideo")]
        public static async Task<VideoFileInfo[]> TranscodeVideo(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
            ILogger log)
        {
            var videoLocation = ctx.GetInput<string>();
            var bitRates = await ctx.CallActivityAsync<int[]>("A_GetTranscodeBitrates", null);
            var transcodeTasks = new List<Task<VideoFileInfo>>();

            foreach (var bitRate in bitRates)
            {
                var info = new VideoFileInfo() { Location = videoLocation, BitRate = bitRate };
                var task = ctx.CallActivityAsync<VideoFileInfo>("A_TranscodeVideo", info);
                transcodeTasks.Add(task);
            }

            // Pararel execution of all tasks. Returns an array
            var transcodeResults = await Task.WhenAll(transcodeTasks);
            return transcodeResults;
        }
    }
}
