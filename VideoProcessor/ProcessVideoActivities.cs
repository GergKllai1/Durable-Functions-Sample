using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
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
