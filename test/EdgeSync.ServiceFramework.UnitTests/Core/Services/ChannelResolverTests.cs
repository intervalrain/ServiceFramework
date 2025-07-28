using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using System.Reflection;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Services;

/// <summary>
/// Unit tests for ChannelResolver to verify attribute-based channel resolution
/// Tests the priority logic: method > class > default connection
/// </summary>
public class ChannelResolverTests
{
    private readonly IOptions<ServiceFrameworkOptions> _mockOptions;
    private readonly ChannelResolver _channelResolver;

    public ChannelResolverTests()
    {
        _mockOptions = Substitute.For<IOptions<ServiceFrameworkOptions>>();
        _mockOptions.Value.Returns(new ServiceFrameworkOptions());
        
        _channelResolver = new ChannelResolver(_mockOptions);
    }

    [Fact]
    public void GetChannelName_WithMethodLevelAttribute_ReturnsMethodChannelName()
    {
        // Arrange
        var serviceType = typeof(TestServiceWithClassAttribute);
        var method = serviceType.GetMethod(nameof(TestServiceWithClassAttribute.MethodWithChannel))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe("method-channel");
    }

    [Fact]
    public void GetChannelName_WithClassLevelAttributeOnly_ReturnsClassChannelName()
    {
        // Arrange
        var serviceType = typeof(TestServiceWithClassAttribute);
        var method = serviceType.GetMethod(nameof(TestServiceWithClassAttribute.MethodWithoutChannel))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe("class-channel");
    }

    [Fact]
    public void GetChannelName_WithNoAttributes_ReturnsEmpty()
    {
        // Arrange
        var serviceType = typeof(TestServiceWithoutAttribute);
        var method = serviceType.GetMethod(nameof(TestServiceWithoutAttribute.PlainMethod))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void GetChannelName_WithMethodAttributeOverridingClass_ReturnsMethodChannelName()
    {
        // Arrange
        var serviceType = typeof(TestServiceWithClassAttribute);
        var method = serviceType.GetMethod(nameof(TestServiceWithClassAttribute.MethodWithDifferentChannel))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe("different-method-channel");
    }

    [Fact]
    public void GetChannelName_WithMethodAttributeEmptyName_ReturnsEmpty()
    {
        // Arrange
        var serviceType = typeof(TestServiceWithClassAttribute);
        var method = serviceType.GetMethod(nameof(TestServiceWithClassAttribute.MethodWithEmptyChannel))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void GetChannelName_WithClassAttributeEmptyName_ReturnsEmpty()
    {
        // Arrange
        var serviceType = typeof(TestServiceWithEmptyClassAttribute);
        var method = serviceType.GetMethod(nameof(TestServiceWithEmptyClassAttribute.PlainMethod))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void GetChannelName_PriorityTest_MethodOverridesClassAttribute()
    {
        // Arrange
        var serviceType = typeof(TestServiceMultipleChannels);
        var method = serviceType.GetMethod(nameof(TestServiceMultipleChannels.MethodWithHighPriorityChannel))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe("high-priority-method");
    }

    [Fact]
    public void GetChannelName_WithInheritedClassAttribute_ReturnsInheritedChannelName()
    {
        // Arrange
        var serviceType = typeof(DerivedTestService);
        var method = serviceType.GetMethod(nameof(DerivedTestService.InheritedMethod))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe("base-channel");
    }

    [Fact]
    public void GetChannelName_WithOverriddenMethod_ReturnsMethodAttribute()
    {
        // Arrange
        var serviceType = typeof(DerivedTestService);
        var method = serviceType.GetMethod(nameof(DerivedTestService.OverriddenMethod))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe("overridden-method-channel");
    }

    // Test service classes for attribute resolution
    [Channel("class-channel")]
    private class TestServiceWithClassAttribute
    {
        [Channel("method-channel")]
        public void MethodWithChannel() { }

        public void MethodWithoutChannel() { }

        [Channel("different-method-channel")]
        public void MethodWithDifferentChannel() { }

        [Channel("")]
        public void MethodWithEmptyChannel() { }
    }

    private class TestServiceWithoutAttribute
    {
        public void PlainMethod() { }
    }

    [Channel("")]
    private class TestServiceWithEmptyClassAttribute
    {
        public void PlainMethod() { }
    }

    [Channel("multi-class-channel")]
    private class TestServiceMultipleChannels
    {
        [Channel("high-priority-method")]
        public void MethodWithHighPriorityChannel() { }
    }

    [Channel("base-channel")]
    private class BaseTestService
    {
        public virtual void InheritedMethod() { }
        public virtual void OverriddenMethod() { }
    }

    private class DerivedTestService : BaseTestService
    {
        public override void InheritedMethod() { }

        [Channel("overridden-method-channel")]
        public override void OverriddenMethod() { }
    }

    // Additional test for interface implementation without attributes
    private interface ITestServiceInterface
    {
        void InterfaceMethod();
    }

    private class TestServiceImplementation : ITestServiceInterface
    {
        public void InterfaceMethod() { }
    }

    [Fact]
    public void GetChannelName_WithInterfaceImplementation_UsesImplementationClassAttribute()
    {
        // Arrange - this tests that we use the concrete class, not the interface
        var serviceType = typeof(TestServiceImplementation);
        var method = serviceType.GetMethod(nameof(TestServiceImplementation.InterfaceMethod))!;

        // Act
        var result = _channelResolver.GetChannelName(serviceType, method);

        // Assert
        result.ShouldBe(string.Empty); // Implementation class has no attribute
    }
}