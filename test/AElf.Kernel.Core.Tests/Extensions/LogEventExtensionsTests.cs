namespace AElf.Kernel;

[Trait("Category", AElfBlockchainModule)]
public class LogEventExtensionsTests : AElfKernelTestBase
{
    [Fact]
    public void LogEvent_GetBloomAndCompare()
    {
        var address = SampleAddress.AddressList[0];
        var logEvent = new LogEvent
        {
            Address = address,
            Indexed =
            {
                ByteString.CopyFromUtf8("event1")
            }
        };
        var bloom = logEvent.GetBloom();
        bloom.Data.ShouldNotBeNull();

        var logEvent1 = new LogEvent
        {
            Address = address,
            Indexed =
            {
                ByteString.CopyFromUtf8("event1"),
                ByteString.CopyFromUtf8("event2")
            }
        };
        var bloom1 = logEvent1.GetBloom();
        bloom1.Data.ShouldNotBeNull();

        bloom.IsIn(bloom1).ShouldBeTrue();
    }
}