using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

public class DiffusionModelFamilyServiceTests
{
    private DiffusionModelFamilyService? _service;

    [SetUp]
    public void SetUp()
    {
        _service = new DiffusionModelFamilyService(NullLogger<DiffusionModelFamilyService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _service?.Dispose();
        _service = null;
    }

    [Test]
    public void Families_ShouldReturnDefaultFamilies()
    {
        Assert.That(_service!.Families, Is.Not.Empty);
        Assert.That(_service.Families.Count, Is.EqualTo(4));
    }

    [Test]
    public void GetFamily_ShouldReturnCorrectFamily()
    {
        var result = _service!.GetFamily("sd15");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("SD 1.5"));
    }

    [Test]
    public void GetFamily_ShouldReturnSdxlFamily()
    {
        var result = _service!.GetFamily("sdxl");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("SDXL"));
    }

    [Test]
    public void GetFamilyByModelId_ShouldMatchSdxl()
    {
        var result = _service!.GetFamilyByModelId("sdxl-base-1.0");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("SDXL"));
    }

    [Test]
    public void GetFamilyByModelId_ShouldMatchFlux()
    {
        var result = _service!.GetFamilyByModelId("flux-dev");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Flux"));
    }

    [Test]
    public void GetFamily_UnknownPipelineType_ReturnsNull()
    {
        var result = _service!.GetFamily("nonexistent");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void RegisterFamily_DuplicateName_DoesNotAdd()
    {
        var initialCount = _service!.Families.Count;
        var config = new DiffusionModelFamilyConfig(
            "SD 1.5",
            "sd15-fake",
            4, 512, 512, 20, 50, 5.0, 12.0, null);
        _service.RegisterFamily(config);
        Assert.That(_service.Families.Count, Is.EqualTo(initialCount));
    }

    [Test]
    public void RegisterFamily_NewName_AddsSuccessfully()
    {
        var initialCount = _service!.Families.Count;
        var config = new DiffusionModelFamilyConfig(
            "Custom",
            "custom",
            4, 512, 512, 20, 50, 5.0, 12.0, null);
        _service.RegisterFamily(config);
        Assert.That(_service.Families.Count, Is.EqualTo(initialCount + 1));
    }

    [Test]
    public void GetFamily_SupportsCaseInsensitive()
    {
        var result = _service!.GetFamily("SDXL");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("SDXL"));
    }

    [Test]
    public void SupportedSamplers_ShouldContainAtLeastFourValues()
    {
        var family = _service!.GetFamily("sd15");
        Assert.That(family, Is.Not.Null);
        Assert.That(family!.SupportedSamplers, Is.Not.Null);
        Assert.That(family.SupportedSamplers.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var service = new DiffusionModelFamilyService(NullLogger<DiffusionModelFamilyService>.Instance);
        Assert.DoesNotThrow(() => service.Dispose());
    }

    [Test]
    public void Flux_HasDifferentLatentChannelsThanSD()
    {
        var flux = _service!.GetFamily("flux");
        var sd15 = _service.GetFamily("sd15");
        Assert.That(flux, Is.Not.Null);
        Assert.That(sd15, Is.Not.Null);
        Assert.That(flux!.LatentChannels, Is.EqualTo(16));
        Assert.That(sd15!.LatentChannels, Is.EqualTo(4));
    }
}