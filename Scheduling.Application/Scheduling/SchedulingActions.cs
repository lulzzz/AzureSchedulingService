﻿using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using Quartz.Spi;
using Scheduling.Application.Jobs.Services;
using Scheduling.Application.Logging;
using Scheduling.SharedPackage.Messages;

namespace Scheduling.Application.Scheduling
{
    public class SchedulingActions : ISchedulingActions, IDisposable
    {
        private IScheduler scheduler;
        private readonly ILogger<SchedulingActions> logger;
        private readonly StdSchedulerFactory standardFactory;
        private readonly IScheduledJobBuilder scheduledJobBuilder;

        public SchedulingActions(ILogger<SchedulingActions> logger, IConfiguration configuration, IScheduledJobBuilder scheduledJobBuilder)
        {
            try
            {
                this.logger = logger;
                this.scheduledJobBuilder = scheduledJobBuilder;

                var quartzSettingsDict = configuration.GetSection("Quartz")
                    .GetChildren()
                    .ToDictionary(x => x.Key, x => x.Value);

                var quartzSettings = new NameValueCollection();
                foreach (var (key, value) in quartzSettingsDict) quartzSettings.Add(key, value);
                standardFactory = new StdSchedulerFactory(quartzSettings);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error getting quartz settings");
                throw;
            }
        }

        public async Task StartScheduler(IJobFactory jobFactory, CancellationToken ct)
        {
            try
            {
                // TODO: Get Quartz logging integrated into ILogger
                LogProvider.SetCurrentLogProvider(new QuartzLoggingProvider(logger));
                scheduler = await standardFactory.GetScheduler(ct);
                scheduler.JobFactory = jobFactory;
                await scheduler.Start(ct);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error starting scheduler");
                throw;
            }
        }

        public async Task DeleteJob(DeleteJobMessage deleteJobMessage, CancellationToken ct)
        {
            await RemoveJobIfAlreadyExists(deleteJobMessage.JobUid, deleteJobMessage.SubscriptionName, ct);
        }

        public async Task AddOrUpdateJob(ScheduleJobMessage scheduleJobMessage, CancellationToken ct)
        {
            try
            {
                scheduledJobBuilder.AssertInputIsValid(scheduleJobMessage);

                await RemoveJobIfAlreadyExists(scheduleJobMessage.JobUid, scheduleJobMessage.SubscriptionName, ct);

                var job = scheduledJobBuilder.BuildJob(scheduleJobMessage);
                var trigger = scheduledJobBuilder.BuildTrigger(scheduleJobMessage.JobUid, scheduleJobMessage.SubscriptionName, scheduleJobMessage.Schedule);

                await scheduler.ScheduleJob(job, trigger, ct);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Error adding job. ScheduleJobMessage: {JsonConvert.SerializeObject(scheduleJobMessage)}");
            }
        }

        private async Task RemoveJobIfAlreadyExists(string jobUid, string subscriptionName, CancellationToken ct)
        {
            try
            {
                var jobKey = new JobKey(jobUid, subscriptionName);
                var jobExists = await scheduler.CheckExists(jobKey, ct);
                if (jobExists)
                {
                    await scheduler.DeleteJob(jobKey, ct);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Error deleting scheduled job, JobUid: {jobUid}");
            }
        }

        public void Dispose()
        {
            Task.Run(async () =>
            {
                await scheduler.Shutdown();
            }).Wait(TimeSpan.FromSeconds(10));
        }
    }
}