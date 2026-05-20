using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;
using NUnit.Framework;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="ContextWindowBudgeter"/>.
/// </summary>
public class ContextWindowBudgeterTests
{
    [Test]
    public void Constructor_InitializesBudget()
    {
        var budgeter = new ContextWindowBudgeter(new TestLogger<ContextWindowBudgeter>());
        Assert.That(budgeter, Is.Not.Null);
    }

    [Test]
    public async Task GetOrCreateBudgetAsync_ReturnsState()
    {
        var budgeter = new ContextWindowBudgeter(new TestLogger<ContextWindowBudgeter>());
        var state = await budgeter.GetOrCreateBudgetAsync(Guid.NewGuid(), 8192);
        Assert.That(state, Is.Not.Null);
    }

    [Test]
    public async Task DeductFromBudgetAsync_DeductsCorrectly()
    {
        var budgeter = new ContextWindowBudgeter(new TestLogger<ContextWindowBudgeter>());
        var chatId = Guid.NewGuid();
        await budgeter.GetOrCreateBudgetAsync(chatId, 1000);
        var remaining = await budgeter.DeductFromBudgetAsync(chatId, ContextInjectionType.CompressedHistory, 100);
        Assert.That(remaining, Is.EqualTo(900L));
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var budgeter = new ContextWindowBudgeter(new TestLogger<ContextWindowBudgeter>());
        budgeter.Dispose();
        Assert.DoesNotThrow(() => budgeter.Dispose());
    }
}