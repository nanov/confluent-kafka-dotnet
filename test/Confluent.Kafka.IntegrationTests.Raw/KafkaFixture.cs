using System.Threading.Tasks;
using Testcontainers.Kafka;
using Xunit;

namespace Confluent.Kafka.IntegrationTests.Raw;

public sealed class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer container = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.6.1")
        .Build();

    public string BootstrapServers => container.GetBootstrapAddress();

    public Task InitializeAsync() => container.StartAsync();

    public Task DisposeAsync() => container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class KafkaCollection : ICollectionFixture<KafkaFixture>
{
    public const string Name = "Kafka";
}
