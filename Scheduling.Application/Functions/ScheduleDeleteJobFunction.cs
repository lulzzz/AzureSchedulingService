using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Scheduling.Application.Scheduling;
using Scheduling.SharedPackage.Messages;

namespace Scheduling.Application.Functions
{
    public class ScheduleDeleteJobFunction
    {
        private readonly ISchedulingActions schedulingActions;

        public ScheduleDeleteJobFunction(ISchedulingActions schedulingActions)
        {
            this.schedulingActions = schedulingActions;
        }

        // TODO: Handle dead letters
        public async Task DeleteJob([ServiceBusTrigger("scheduling-delete")] Message message, ILogger logger, CancellationToken ct)
        {
            string body = null;
            try
            {
                body = Encoding.UTF8.GetString(message.Body);
                var job = JsonConvert.DeserializeObject<DeleteJobMessage>(body);
                await schedulingActions.DeleteJob(job, ct);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Unable to delete job. Message: {body}");
            }
        }
    }
}
