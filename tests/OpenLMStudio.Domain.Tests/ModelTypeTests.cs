using System;
using NUnit.Framework;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Domain.Tests;

/// <summary>
/// Tests for the ModelType enum to ensure it covers all expected multi-modal types.
/// </summary>
[TestFixture]
public class ModelTypeTests
{
    [Test]
    public void TextGeneration_IsDefined()
    {
        Assert.That(ModelType.TextGeneration, Is.EqualTo(0));
    }

    [Test]
    public void ImageGeneration_IsDefined()
    {
        Assert.That(ModelType.ImageGeneration, Is.EqualTo(1));
    }

    [Test]
    public void Diffusion_IsDefined()
    {
        Assert.That(ModelType.Diffusion, Is.EqualTo(2));
    }

    [Test]
    public void Vae_IsDefined()
    {
        Assert.That(ModelType.Vae, Is.EqualTo(3));
    }

    [Test]
    public void Lora_IsDefined()
    {
        Assert.That(ModelType.Lora, Is.EqualTo(4));
    }

    [Test]
    public void Embedding_IsDefined()
    {
        Assert.That(ModelType.Embedding, Is.EqualTo(5));
    }

    [Test]
    public void AllModelTypesAreDefined()
    {
        // Ensure we can iterate all values without unexpected gaps.
        var defined = Enum.GetValues<ModelType>();
        CollectionAssert.AreEqual(new[] { ModelType.TextGeneration, ModelType.ImageGeneration, ModelType.Diffusion, ModelType.Vae, ModelType.Lora, ModelType.Embedding }, defined);
    }
}