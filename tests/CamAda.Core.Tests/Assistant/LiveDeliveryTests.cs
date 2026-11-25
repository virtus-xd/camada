using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class LiveDeliveryTests
{
    private readonly List<string> _seen = [];

    private void Attach(LiveDelivery delivery) =>
        delivery.Attach(e => _seen.Add(e.GetType().Name), error => _seen.Add("kapandı:" + error));

    [Fact]
    public void Events_before_attach_are_delivered_in_order_then_live()
    {
        var delivery = new LiveDelivery();
        delivery.Post(new LiveEvent.SetupComplete());
        delivery.Post(new LiveEvent.InputText("merhaba"));

        Attach(delivery);
        delivery.Post(new LiveEvent.TurnComplete());

        Assert.Equal(["SetupComplete", "InputText", "TurnComplete"], _seen);
    }

    [Fact]
    public void Close_before_attach_is_not_lost()
    {
        var delivery = new LiveDelivery();
        delivery.Post(new LiveEvent.InputText("merhaba"));
        delivery.Close(AssistantError.Network); // bağlantı, dinleyici bağlanmadan koptu

        Attach(delivery);

        Assert.Equal(["InputText", "kapandı:Network"], _seen);
    }

    [Fact]
    public void Close_is_raised_once_and_ends_delivery()
    {
        var delivery = new LiveDelivery();
        Attach(delivery);
        delivery.Close(AssistantError.Network);
        delivery.Close(AssistantError.None);
        delivery.Post(new LiveEvent.TurnComplete());

        Assert.Equal(["kapandı:Network"], _seen);
    }
}
