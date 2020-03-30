using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
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

            var transcodedLocation = await ctx.CallActivityAsync<string>("A_TransocdeVideo", videoLocation);

            if (!ctx.IsReplaying)
            {
                log.LogInformation("About to call extract thumbnail activity");
            }

            var thumbnailLocation = await ctx.CallActivityAsync<string>("A_ExtractThumbnail", transcodedLocation);

            if (!ctx.IsReplaying)
            {
                log.LogInformation("About to call prepend intro activity");
            }

            var withIntroLocation = await ctx.CallActivityAsync<string>("A_PrependIntro", transcodedLocation);

            return new
            {
                Transcoded = transcodedLocation,
                ThumbNail = thumbnailLocation,
                WithIntro = withIntroLocation
            };

        }
    }
}
