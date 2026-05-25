namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents task progress tracking with checklist and reminders.
/// </summary>
public class AgentTaskProgress
{
    /// <summary>Current progress percentage (0-100).</summary>
    public int Percentage { get; set; }

    /// <summary>Checklist of task items.</summary>
    public List<AgentTaskChecklistItem> Checklist { get; set; } = new();

    /// <summary>Current step description.</summary>
    public string? CurrentStep { get; set; }

    /// <summary>Reminders to send to the user.</summary>
    public List<string> Reminders { get; set; } = new();

    /// <summary>Last reminder timestamp.</summary>
    public DateTime LastReminderTime { get; set; }

    /// <summary>Interval between reminders (in minutes).</summary>
    public int ReminderIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Adds a checklist item.
    /// </summary>
    public void AddChecklistItem(string description, bool isCompleted = false)
    {
        Checklist.Add(new AgentTaskChecklistItem
        {
            Description = description,
            IsCompleted = isCompleted,
            AddedAt = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Marks a checklist item as completed.
    /// </summary>
    public void CompleteChecklistItem(string description)
    {
        var item = Checklist.FirstOrDefault(i => i.Description == description);
        if (item != null)
        {
            item.IsCompleted = true;
            item.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Updates the current step.
    /// </summary>
    public void UpdateCurrentStep(string step)
    {
        CurrentStep = step;
    }

    /// <summary>
    /// Adds a reminder.
    /// </summary>
    public void AddReminder(string reminder)
    {
        Reminders.Add(reminder);
        LastReminderTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the overall completion percentage based on checklist items.
    /// </summary>
    public int CalculateChecklistPercentage()
    {
        if (Checklist.Count == 0)
            return Percentage;

        var completed = Checklist.Count(i => i.IsCompleted);
        var checklistPercentage = (int)((double)completed / Checklist.Count * 100);

        // Weight checklist 70% and manual percentage 30%
        return (int)(checklistPercentage * 0.7 + Percentage * 0.3);
    }
}

/// <summary>
/// A single checklist item in the task progress.
/// </summary>
public class AgentTaskChecklistItem
{
    /// <summary>Description of the checklist item.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether the item has been completed.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>When the item was added.</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the item was completed.</summary>
    public DateTime? CompletedAt { get; set; }
}