namespace Ecommerce.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_reference_resolves()
    {
        var error = new Ecommerce.Abstractions.Error("Test.Code", "Test description");
        Assert.Equal("Test.Code", error.Code);
    }
}
