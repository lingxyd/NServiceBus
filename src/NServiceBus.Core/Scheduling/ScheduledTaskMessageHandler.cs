namespace NServiceBus.Scheduling
{
    using System.Threading.Tasks;

    class ScheduledTaskMessageHandler : IHandleMessages<Messages.ScheduledTask>
    {
        DefaultScheduler scheduler;
        IBus bus;

        public ScheduledTaskMessageHandler(DefaultScheduler scheduler, IBus bus)
        {
            this.scheduler = scheduler;
            this.bus = bus;
        }

        public Task Handle(Messages.ScheduledTask message)
        {
            scheduler.Start(message.TaskId, bus);
            return TaskEx.Completed;
        }
    }
}