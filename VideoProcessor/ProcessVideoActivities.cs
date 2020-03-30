using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace VideoProcessor
{
    public static class ProcessVideoActivities
    {
        [FunctionName("A_TransocdeVideo")]
        public static async Task<string> TransocdeVideo(
            [ActivityTrigger] string inputVideo,
            ILogger log)
        {

            log.LogInformation($"Transcoding {inputVideo}");
            // simulate doing the activity
            await Task.Delay(5000);

            return "transcoded.mp4";
        }

        [FunctionName("A_ExtractThumbnail")]
        public static async Task<string> ExtractThumbnail(
           [ActivityTrigger] string inputVideo,
           ILogger log)
        {

            log.LogInformation($"Extracting Thumbnail {inputVideo}");
            // simulate doing the activity
            await Task.Delay(5000);

            return "thumbnail.png";
        }

        [FunctionName("A_PrependIntro")]
        public static async Task<string> PrependIntro(
           [ActivityTrigger] string inputVideo,
           ILogger log)
        {

            log.LogInformation($"Appending intro to video {inputVideo}");
            var config = new ConfigurationBuilder().Build();
            //var introLocation = config["IntroLocation"];
            // simulate doing the activity
            await Task.Delay(5000);

            return "withIntro.mp4";
        }
    }
}
