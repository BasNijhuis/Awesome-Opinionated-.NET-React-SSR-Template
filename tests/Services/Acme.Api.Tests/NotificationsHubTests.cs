using Acme.ApiClient;
using Acme.Kernel.Application.Common.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace Acme.Api.Tests;

public class NotificationsHubTests
{
    // Local mirror of the wire payload (Acme.Kernel.Contracts.NotificationMessage { channel, message })
    // so the test asserts the contract by shape without taking a direct dependency on the type.
    private sealed record NotificationPayload(string Channel, string Message);

    [Fact]
    public async Task Creating_a_greeting_publishes_a_notification_to_the_channel()
    {
        // Arrange — subscribe to the "greetings" channel on the notifications hub
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        var client = new AcmeApiClient(factory.CreateClient());

        var received = new TaskCompletionSource<NotificationPayload>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress!, "/hubs/notifications"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                }
            )
            .Build();

        connection.On<NotificationPayload>(
            NotificationHubTopics.NotificationReceived,
            payload =>
            {
                if (payload is not null)
                {
                    received.TrySetResult(payload);
                }
            }
        );

        await connection.StartAsync(cancellationToken);
        await connection.InvokeAsync("JoinChannel", "greetings", cancellationToken);

        // Act — create a greeting; its handler publishes to the "greetings" channel
        await client.CreateGreetingAsync(
            new CreateGreetingRequest { Message = "ping" },
            cancellationToken
        );

        // Assert — the published notification arrives on the subscribed channel
        var notification = await received.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            cancellationToken
        );
        notification.Channel.Should().Be("greetings");
        notification.Message.Should().Contain("ping");

        await connection.DisposeAsync();
    }
}
