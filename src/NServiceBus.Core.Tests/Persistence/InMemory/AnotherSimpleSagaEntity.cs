namespace NServiceBus.SagaPersisters.InMemory.Tests
{
    using System;

    public class AnotherSimpleSagaEntity : IContainSagaData
    {
        public Guid Id { get; set; }
        public string Originator { get; set; }
        public string OriginalMessageId { get; set; }

        public string ProductSource { get; set; }
        public DateTime ProductExpirationDate { get; set; }
        public decimal ProductCost { get; set; }
    }
}