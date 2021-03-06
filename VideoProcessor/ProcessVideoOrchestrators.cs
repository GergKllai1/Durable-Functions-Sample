﻿using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            // get the input passed from the StartNewAsync() function
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
                // Activity to return the bitrates
                var transcodeResults =
                    await ctx.CallSubOrchestratorAsync<VideoFileInfo[]>("O_TranscodeVideo", videoLocation);
                transcodedLocation =
                  transcodeResults
                  .OrderByDescending(r => r.BitRate)
                  .Select(r => r.Location)
                  .FirstOrDefault();

                // Durable functions api keeps track what activites has been performed and what has not been
                if (!ctx.IsReplaying)
                {
                    log.LogInformation("About to call extract thumbnail activity");
                }

                // Call an activity with retry options
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
               
                // Call an activity without retrying
                await ctx.CallActivityAsync("A_SendApprovalRequestEmail", new ApprovalInfo()
                {
                    OrchestrationId = ctx.InstanceId,
                    VideoLocation = withIntroLocation
                });

                // An exemple of a running task without timeout 
                // approvalResult = await ctx.WaitForExternalEvent<string>("ApprovalResult");

                using (var cts = new CancellationTokenSource())
                {
                    // ctx saved dateTime has to be used to keep the orchestrator deterministic
                    var timeoutAt = ctx.CurrentUtcDateTime.AddSeconds(30);
                    // Puts the orchestration to sleep for x amount of time. Takes CancellationToken as 
                    // a secondary argument, to be able to stop it. CancellationToken.None can be passed if not.
                    var timeoutTask = ctx.CreateTimer(timeoutAt, cts.Token);
                    var approvalTaks = ctx.WaitForExternalEvent<string>("ApprovalResult");

                    // Passes in the result of any of the tasks that has been executed first
                    var winner = await Task.WhenAny(approvalTaks, timeoutTask);

                    if (winner == approvalTaks)
                    {
                        approvalResult = approvalTaks.Result;
                        cts.Cancel(); // cancelling the timeout task
                    }
                    else
                    {
                        approvalResult = "Timed Out!";
                    }
                }
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
                // Creates a list of tasks
                transcodeTasks.Add(task);
            }

            // Pararel execution of all tasks. Returns an array. Wait for all tasks to be executed
            var transcodeResults = await Task.WhenAll(transcodeTasks);
            return transcodeResults;
        }

        [FunctionName("O_PeriodicTask")]
        public static async Task<int> PeriodicTask(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
            ILogger log)
        {
            int timesRun = ctx.GetInput<int>();
            timesRun++;
            if (!ctx.IsReplaying)
                log.LogInformation($"Starting the PeriodicTask activity {ctx.InstanceId}, {timesRun}");
            await ctx.CallActivityAsync("A_PeriodicActivity", timesRun);
            var nextRun = ctx.CurrentUtcDateTime.AddSeconds(30);
            await ctx.CreateTimer(nextRun, CancellationToken.None);
            ctx.ContinueAsNew(timesRun);
            return timesRun;
        }
    }
}
