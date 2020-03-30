using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
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

            string transcodedLocation = null;
            string thumbnailLocation = null;
            string withIntroLocation = null;

            try
            {
                if (!ctx.IsReplaying)
                {
                    log.LogInformation("About to call transcode video activity");
                }

                transcodedLocation = await ctx.CallActivityAsync<string>("A_TranscodeVideo", videoLocation);

                if (!ctx.IsReplaying)
                {
                    log.LogInformation("About to call extract thumbnail activity");
                }

                //thumbnailLocation = await ctx.CallActivityAsync<string>("A_ExtractThumbnail", transcodedLocation);

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

                return new
                {
                    Transcoded = transcodedLocation,
                    ThumbNail = thumbnailLocation,
                    WithIntro = withIntroLocation
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
    }
}
