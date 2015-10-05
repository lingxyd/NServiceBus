namespace NServiceBus.Transports.FileBased
{
    using System.IO;

    class QueueCreator:ICreateQueues
    {
        public void CreateQueueIfNecessary(string address, string account)
        {
            Directory.CreateDirectory(address);
            Directory.CreateDirectory(Path.Combine(address,".committed"));
        }
    }
}