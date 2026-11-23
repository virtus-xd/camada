using CamAda.Core.Startup;

namespace CamAda.Core.Tests.Startup;

public class AutostartCommandTests
{
    [Fact]
    public void Path_with_spaces_is_quoted()
    {
        Assert.Equal(@"""C:\Users\a b\CamAda.exe""", AutostartCommand.Build(@"C:\Users\a b\CamAda.exe"));
    }

    [Fact]
    public void Run_value_name_is_CamAda()
    {
        Assert.Equal("CamAda", AutostartCommand.RunValueName);
    }
}
