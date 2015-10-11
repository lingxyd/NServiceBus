namespace NServiceBus.Transports.FileBased
{
    using System.IO;

    class QueueCreator : ICreateQueues
    {
        public void CreateQueueIfNecessary(string address, string account)
        {
            var fullPath = Path.Combine("c:\\bus", address);
            Directory.CreateDirectory(fullPath);
            Directory.CreateDirectory(Path.Combine(fullPath, ".committed"));
        }
    }
}