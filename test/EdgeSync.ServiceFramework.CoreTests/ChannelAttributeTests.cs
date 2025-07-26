using System.Reflection;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using Xunit;

namespace EdgeSync.ServiceFramework.CoreTests;

public class ChannelAttributeTests
{
    [Fact]
    public void ChannelAttribute_Should_Support_Class_Level()
    {
        // Arrange & Act
        var attribute = typeof(TestClassWithChannel).GetCustomAttribute<ChannelAttribute>();
        
        // Assert
        Assert.NotNull(attribute);
        Assert.Equal("class-channel", attribute.Name);
    }
    
    [Fact]
    public void ChannelAttribute_Should_Support_Method_Level()
    {
        // Arrange & Act
        var method = typeof(TestClassWithChannel).GetMethod(nameof(TestClassWithChannel.MethodWithChannel))!;
        var attribute = method.GetCustomAttribute<ChannelAttribute>();
        
        // Assert
        Assert.NotNull(attribute);
        Assert.Equal("method-channel", attribute.Name);
    }
    
    [Fact]
    public void ChannelAttribute_Priority_Method_Over_Class()
    {
        // Arrange
        var serviceType = typeof(TestClassWithChannel);
        var method = serviceType.GetMethod(nameof(TestClassWithChannel.MethodWithChannel))!;
        
        // Act
        var channelName = GetChannelName(serviceType, method);
        
        // Assert
        Assert.Equal("method-channel", channelName);
    }
    
    [Fact]
    public void ChannelAttribute_Fallback_To_Class_When_No_Method_Attribute()
    {
        // Arrange
        var serviceType = typeof(TestClassWithChannel);
        var method = serviceType.GetMethod(nameof(TestClassWithChannel.MethodWithoutChannel))!;
        
        // Act
        var channelName = GetChannelName(serviceType, method);
        
        // Assert
        Assert.Equal("class-channel", channelName);
    }
    
    [Fact]
    public void ChannelAttribute_Returns_Empty_When_No_Attributes()
    {
        // Arrange
        var serviceType = typeof(TestClassWithoutChannel);
        var method = serviceType.GetMethod(nameof(TestClassWithoutChannel.RegularMethod))!;
        
        // Act
        var channelName = GetChannelName(serviceType, method);
        
        // Assert
        Assert.Equal(string.Empty, channelName);
    }
    
    private static string GetChannelName(Type serviceType, MethodInfo method)
    {
        // Priority: method > class > default connection
        
        // 1. Check method-level Channel attribute
        var methodChannel = method.GetCustomAttribute<ChannelAttribute>();
        if (methodChannel != null)
        {
            return methodChannel.Name;
        }
        
        // 2. Check class-level Channel attribute
        var classChannel = serviceType.GetCustomAttribute<ChannelAttribute>();
        if (classChannel != null)
        {
            return classChannel.Name;
        }
        
        // 3. Return empty (would use default connection in real implementation)
        return string.Empty;
    }
    
    [Channel("class-channel")]
    private class TestClassWithChannel
    {
        [Channel("method-channel")]
        public void MethodWithChannel() { }
        
        public void MethodWithoutChannel() { }
    }
    
    private class TestClassWithoutChannel
    {
        public void RegularMethod() { }
    }
}