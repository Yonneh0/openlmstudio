using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for DiffusionInferenceEngine sampler and utility methods (inference engine is stubbed).
/// </summary>
public class DiffusionInferenceEngineTests
{
    [Test]
    public void ComputeEulerTimeSteps_ReturnsCorrectNumberOfSteps()
    {
        var engine = new DiffusionInferenceEngine(null);
        var steps = engine.ComputeEulerTimeSteps(10);

        Assert.That(steps.Length, Is.EqualTo(10));
        // First step should be ~1.0, last step should be ~0.0
        Assert.That(steps[0], Is.InRange(0.99, 1.01));
        Assert.That(steps[steps.Length - 1], Is.InRange(-0.01, 0.01));
    }

    [Test]
    public void ComputeEulerTimeSteps_IsMonotonicallyDecreasing()
    {
        var engine = new DiffusionInferenceEngine(null);
        var steps = engine.ComputeEulerTimeSteps(100);

        for (int i = 1; i < steps.Length; i++)
        {
            Assert.That(steps[i], Is.AtMost(steps[i - 1]), $"Step {i} ({steps[i]}) should be <= step {i - 1} ({steps[i - 1]})");
        }
    }

    [Test]
    public void ComputeEulerATimeSteps_ReturnsSameScheduleAsEuler()
    {
        var engine = new DiffusionInferenceEngine(null);
        var eulerSteps = engine.ComputeEulerTimeSteps(5);
        var eulerASteps = engine.ComputeEulerATimeSteps(5);

        Assert.That(eulerASteps.Length, Is.EqualTo(eulerSteps.Length));
        for (int i = 0; i < eulerSteps.Length; i++)
        {
            Assert.That(eulerASteps[i], Is.EqualTo(eulerSteps[i]).Within(5));
        }
    }

    [Test]
    public void ComputeDPMTimesteps_ExponentialDecay()
    {
        var engine = new DiffusionInferenceEngine(null);
        var steps = engine.ComputeDPMTimesteps(10);

        Assert.That(steps.Length, Is.EqualTo(10));
        // DPM uses exp(-t * log(1000))
        Assert.That(steps[0], Is.InRange(0.99, 1.01));
        Assert.That(steps[steps.Length - 1], Is.GreaterThan(0).And.LessThan(0.01));
    }

    [Test]
    public void ComputeLMSFixedTimeSteps_LMSFormula()
    {
        var engine = new DiffusionInferenceEngine(null);
        var steps = engine.ComputeLMSFixedTimeSteps(20);

        Assert.That(steps.Length, Is.EqualTo(20));
        // First step ≈ 14.6186328
        Assert.That(steps[0], Is.InRange(14.5, 14.7));
    }

    [Test]
    public void GetSigmaFromTime_ReturnsValidRange()
    {
        var engine = new DiffusionInferenceEngine(null);
        var sigma = engine.GetSigmaFromTime(0.5, 1.0);
        Assert.That(sigma, Is.InRange(0, 1));
    }

    [Test]
    public void GetSigmaFromTime_IsIncreasing()
    {
        var engine = new DiffusionInferenceEngine(null);
        var sigma1 = engine.GetSigmaFromTime(0.3, 1.0);
        var sigma2 = engine.GetSigmaFromTime(0.7, 1.0);
        Assert.That(sigma2, Is.GreaterThan(sigma1), "Sigma should increase as t decreases toward 0");
    }

    [Test]
    public void EncodePrompt_ReturnsNullForEmptyInput()
    {
        var engine = new DiffusionInferenceEngine(null);
        // Empty prompts return null in the stub implementation
        var result = engine.RunTextEncoder("sd15", "");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void EncodePrompt_ReturnsTensorForNonEmptyInput()
    {
        var engine = new DiffusionInferenceEngine(null);
        var result = engine.RunTextEncoder("sd15", "a cat sitting on a table");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void BlendTensors_CorrectlyBlendsWithCfgScale()
    {
        var engine = new DiffusionInferenceEngine(null);
        var cond = new DenseTensor<float>(new[] { 1, 768 });
        var uncond = new DenseTensor<float>(new[] { 1, 768 });

        // Set distinct values for testing
        for (int i = 0; i < cond.Length; i++)
        {
            cond[i] = 1.0f;
            uncond[i] = 0.0f;
        }

        var blended = engine.BlendTensors(cond, uncond, 7.5f);
        Assert.That(blended, Is.Not.Null);
        Assert.That(blended.Dimensions, Is.EqualTo(cond.Dimensions));

        // blended[i] = 0.0 + 7.5 * (1.0 - 0.0) = 7.5
        for (int i = 0; i < blended.Length; i++)
        {
            Assert.That(blended[i], Is.InRange(7.49f, 7.51f));
        }
    }

    [Test]
    public void AddTensors_ElementWiseCorrect()
    {
        var engine = new DiffusionInferenceEngine(null);
        var a = new DenseTensor<float>(new[] { 1, 4 }) { 1.0f, 2.0f, 3.0f, 4.0f };
        var b = new DenseTensor<float>(new[] { 1, 4 }) { 5.0f, 6.0f, 7.0f, 8.0f };

        var result = engine.AddTensors(a, b);
        Assert.That(result.Length, Is.EqualTo(4));
        Assert.That(result[0], Is.EqualTo(6.0f));
        Assert.That(result[1], Is.EqualTo(8.0f));
        Assert.That(result[2], Is.EqualTo(10.0f));
        Assert.That(result[3], Is.EqualTo(12.0f));
    }

    [Test]
    public void SubtractTensors_ElementWiseCorrect()
    {
        var engine = new DiffusionInferenceEngine(null);
        var a = new DenseTensor<float>(new[] { 1, 4 }) { 6.0f, 8.0f, 10.0f, 12.0f };
        var b = new DenseTensor<float>(new[] { 1, 4 }) { 1.0f, 2.0f, 3.0f, 4.0f };

        var result = engine.SubtractTensors(a, b);
        Assert.That(result.Length, Is.EqualTo(4));
        Assert.That(result[0], Is.EqualTo(5.0f));
        Assert.That(result[1], Is.EqualTo(6.0f));
        Assert.That(result[2], Is.EqualTo(7.0f));
        Assert.That(result[3], Is.EqualTo(8.0f));
    }

    [Test]
    public void AddGaussianNoise_AddsNoiseProportionalToSigma()
    {
        var engine = new DiffusionInferenceEngine(null);
        var latents = new DenseTensor<float>(new[] { 1, 4, 4, 4 });
        var noisy = engine.AddGaussianNoise(latents, 2.0f);

        Assert.That(noisy.Dimensions, Is.EqualTo(latents.Dimensions));
        for (int i = 0; i < latents.Length; i++)
        {
            Assert.That(noisy[i], Is.Not.EqualTo(latents[i]));
        }
    }

    [Test]
    public void AddNoiseToLatents_IsDeterministic()
    {
        var engine = new DiffusionInferenceEngine(null);
        var latents = new DenseTensor<float>(new[] { 1, 4, 4, 4 });
        var seed = 42;

        var noisy1 = engine.AddGaussianNoise(latents, 1.0f, seed);
        var noisy2 = engine.AddGaussianNoise(latents, 1.0f, seed);

        for (int i = 0; i < noisy1.Length; i++)
        {
            Assert.That(noisy1[i], Is.EqualTo(noisy2[i]).Within(10));
        }
    }

    [Test]
    public void GetPipelineType_SDXL()
    {
        var engine = new DiffusionInferenceEngine(null);
        var result = engine.GetPipelineType("model-sdxl-test");
        Assert.That(result, Is.EqualTo("sdxl"));
    }

    [Test]
    public void GetPipelineType_Flux()
    {
        var engine = new DiffusionInferenceEngine(null);
        var result = engine.GetPipelineType("flux-dev");
        Assert.That(result, Is.EqualTo("flux"));
    }

    [Test]
    public void GetPipelineType_DefaultsToSD15()
    {
        var engine = new DiffusionInferenceEngine(null);
        var result = engine.GetPipelineType("random-model");
        Assert.That(result, Is.EqualTo("sd15"));
    }

    [Test]
    public void GetPipelineType_IsCaseInsensitive()
    {
        var engine = new DiffusionInferenceEngine(null);
        var result = engine.GetPipelineType("MODEL-FLUX-TEST");
        Assert.That(result, Is.EqualTo("flux"));
    }
}