namespace OpenLMStudio.Desktop.Controls;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Services.Agent;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Interactive form for executing a tool call with pre-filled parameters.
/// </summary>
public partial class ToolCallForm : UserControl, IDisposable
{
    private readonly ToolDefinition _toolDefinition;
    private readonly AgentToolExecutor _toolExecutor;
    private readonly ILogger<ToolCallForm>? _logger;
    private readonly Dictionary<string, object> _parameterValues;
    private readonly Action<ToolCallForm>? _onCancel;
    private readonly Func<ToolCallForm, Task>? _onExecute;
    private bool _disposed;
    private int _cancelled;
    private bool _formBuilt;

    /// <summary>
    /// Event fired when the form is cancelled.
    /// </summary>
    public event Action Cancelled = delegate { };

    /// <summary>
    /// Reference to the popup hosting this form.
    /// </summary>
    internal Popup? PopupReference { get; set; }

    /// <summary>
    /// Creates a new ToolCallForm for the given tool definition.
    /// </summary>
    public ToolCallForm(ToolDefinition toolDefinition, AgentToolExecutor toolExecutor, ILogger<ToolCallForm>? logger = null)
    {
        InitializeComponent();
        _toolDefinition = toolDefinition ?? throw new ArgumentNullException(nameof(toolDefinition));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _logger = logger;
        _parameterValues = new Dictionary<string, object>();
        ToolNameText?.SetText(toolDefinition.Name);
        ToolIconText?.SetText(GetToolIcon(toolDefinition.Name));
    }

    /// <summary>
    /// Creates a new ToolCallForm with a custom execute callback.
    /// </summary>
    public ToolCallForm(ToolDefinition toolDefinition, AgentToolExecutor toolExecutor, Func<ToolCallForm, Task> onExecute, ILogger<ToolCallForm>? logger = null)
    {
        InitializeComponent();
        _toolDefinition = toolDefinition ?? throw new ArgumentNullException(nameof(toolDefinition));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _logger = logger;
        _parameterValues = new Dictionary<string, object>();
        _onExecute = onExecute;
        ToolNameText?.SetText(toolDefinition.Name);
        ToolIconText?.SetText(GetToolIcon(toolDefinition.Name));
    }

    /// <summary>
    /// Creates a new ToolCallForm with a custom cancel callback.
    /// </summary>
    public ToolCallForm(ToolDefinition toolDefinition, AgentToolExecutor toolExecutor, Action<ToolCallForm> onCancel, ILogger<ToolCallForm>? logger = null)
    {
        InitializeComponent();
        _toolDefinition = toolDefinition ?? throw new ArgumentNullException(nameof(toolDefinition));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _logger = logger;
        _parameterValues = new Dictionary<string, object>();
        _onCancel = onCancel;
        ToolNameText?.SetText(toolDefinition.Name);
        ToolIconText?.SetText(GetToolIcon(toolDefinition.Name));
    }

    private void BuildForm()
    {
        if (_formBuilt) return;
        _formBuilt = true;

        if (_toolDefinition.ParameterSchema == null || _toolDefinition.ParameterSchema.Count == 0)
        {
            _logger?.LogDebug("ToolCallForm: No parameters for tool '{ToolName}'", _toolDefinition.Name);
            return;
        }

        // Ensure all existing parameter values are available for initialization
        foreach (var param in _toolDefinition.ParameterSchema)
        {
            if (!_parameterValues.ContainsKey(param.Key))
                _parameterValues[param.Key] = param.Value;
        }

        foreach (var param in _toolDefinition.ParameterSchema)
        {
            var row = CreateParameterRow(param.Key, param.Value);
            ParameterPanel.Children.Add(row);
        }
    }

    private Control CreateParameterRow(string paramName, string paramType)
    {
        if (string.IsNullOrWhiteSpace(paramName))
            return new TextBlock { Text = "Invalid parameter", Foreground = GetResource<SolidColorBrush>("AccentRed"), Margin = new Thickness(0, 4, 0, 0) };

        var outer = new Grid();
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var label = new TextBlock
        {
            Text = paramName,
            Foreground = GetResource<SolidColorBrush>("TextSecondary"),
            FontSize = 11,
            Margin = new Thickness(0, 4, 8, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        Grid.SetColumn(label, 0);
        outer.Children.Add(label);

        Control input;
        switch (paramName.ToLowerInvariant())
        {
            case "path":
            case "workingdirectory":
                input = CreatePathInput(paramName);
                break;
            case "content":
            case "diff":
            case "command":
            case "context":
            case "prompt":
            case "input":
                input = CreateMultilineInput(paramName);
                break;
            case "startline":
            case "endline":
            case "timeout":
            case "maxtokens":
                input = CreateNumericInput(paramName);
                break;
            case "requiresapproval":
            case "recursive":
            case "needsmoreexploration":
                input = CreateBoolInput(paramName);
                break;
            case "arguments":
            case "argumentsjson":
                input = CreateJsonInput(paramName);
                break;
            case "action":
                input = CreateActionInput(paramName);
                break;
            case "servername":
            case "toolname":
            case "query":
            case "skillname":
                input = CreateTextBoxInput(paramName);
                break;
            default:
                if (paramName.StartsWith("prompt_", StringComparison.Ordinal))
                    input = CreateMultilineInput(paramName);
                else if (paramName.Contains("url", StringComparison.OrdinalIgnoreCase) ||
                         paramName.Contains("uri", StringComparison.OrdinalIgnoreCase))
                    input = CreateTextBoxInput(paramName);
                else if (paramName.Contains("coordinate", StringComparison.OrdinalIgnoreCase))
                    input = CreateTextBoxInput(paramName);
                else if (paramName.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                         paramName.Contains("content", StringComparison.OrdinalIgnoreCase))
                    input = CreateMultilineInput(paramName);
                else
                    input = CreateTextBoxInput(paramName);
                break;
        }

        Grid.SetColumn(input, 1);
        outer.Children.Add(input);

        return outer;
    }

    private Control CreatePathInput(string paramName)
    {
        var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var tb = new TextBox
        {
            Text = string.Empty,
            Foreground = GetResource<SolidColorBrush>("TextPrimary"),
            Background = GetResource<SolidColorBrush>("BgTertiary"),
            BorderThickness = new Thickness(1),
            BorderBrush = GetResource<SolidColorBrush>("TextMuted"),
            Margin = new Thickness(0, 0, 4, 0)
        };
        tb.AddHandler(TextBox.TextChangedEvent, (s, e) => _parameterValues[paramName] = ((TextBox)s!).Text ?? string.Empty);

        var btn = new Button
        {
            Content = "📂",
            Background = GetResource<SolidColorBrush>("BgTertiary"),
            Foreground = GetResource<SolidColorBrush>("TextPrimary"),
            Padding = new Thickness(8, 4),
            BorderThickness = new Thickness(1),
            BorderBrush = GetResource<SolidColorBrush>("TextMuted")
        };
        btn.Click += async (s, e) =>
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var files = await topLevel!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Folder"
                }).ConfigureAwait(false);
                if (files?.Count > 0)
                    tb.Text = files[0].Path.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to select folder for parameter '{ParamName}'", paramName);
            }
        };

        row.Children.Add(tb);
        row.Children.Add(btn);
        return row;
    }

    private Control CreateMultilineInput(string paramName)
    {
        var tb = new TextBox
        {
            Text = string.Empty,
            Foreground = GetResource<SolidColorBrush>("TextPrimary"),
            Background = GetResource<SolidColorBrush>("BgTertiary"),
            BorderThickness = new Thickness(1),
            BorderBrush = GetResource<SolidColorBrush>("TextMuted"),
            Margin = new Thickness(0, 4, 0, 0),
            AcceptsReturn = true,
            MaxHeight = 200,
            MinHeight = 60,
            Height = 100,
            TextWrapping = TextWrapping.Wrap
        };
        tb.AddHandler(TextBox.TextChangedEvent, (s, e) => _parameterValues[paramName] = ((TextBox)s!).Text ?? string.Empty);
        return tb;
    }

    private Control CreateNumericInput(string paramName)
    {
        var numeric = new NumericUpDown
        {
            Value = 0,
            Width = 120,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 11
        };
        numeric.AddHandler(NumericUpDown.ValueChangedEvent, (s, e) => _parameterValues[paramName] = ((NumericUpDown)s!).Value!);
        return numeric;
    }

    private Control CreateBoolInput(string paramName)
    {
        var toggle = new ToggleButton
        {
            Content = "Off",
            Background = GetResource<SolidColorBrush>("BgTertiary"),
            Foreground = GetResource<SolidColorBrush>("TextPrimary"),
            Padding = new Thickness(12, 4),
            Margin = new Thickness(0, 4, 0, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = GetResource<SolidColorBrush>("TextMuted")
        };

        // Initialize toggle state from existing parameter value
        if (_parameterValues.TryGetValue(paramName, out var existingValue) && existingValue is bool existingBool)
        {
            toggle.IsChecked = existingBool;
            toggle.Content = existingBool ? "On" : "Off";
            toggle.Background = existingBool
                ? GetResource<SolidColorBrush>("AccentBlue")
                : GetResource<SolidColorBrush>("BgTertiary");
            toggle.Foreground = existingBool
                ? Brushes.White
                : GetResource<SolidColorBrush>("TextPrimary");
        }

        toggle.AddHandler(ToggleButton.IsCheckedChangedEvent, (s, e) =>
        {
            var isChecked = ((ToggleButton)s!).IsChecked == true;
            _parameterValues[paramName] = isChecked;
            toggle.Content = isChecked ? "On" : "Off";
            toggle.Background = isChecked
                ? GetResource<SolidColorBrush>("AccentBlue")
                : GetResource<SolidColorBrush>("BgTertiary");
            toggle.Foreground = isChecked
                ? Brushes.White
                : GetResource<SolidColorBrush>("TextPrimary");
        });
        return toggle;
    }

    private Control CreateJsonInput(string paramName)
    {
        var tb = new TextBox
        {
            Text = "{}",
            Foreground = GetResource<SolidColorBrush>("TextPrimary"),
            Background = GetResource<SolidColorBrush>("BgTertiary"),
            BorderThickness = new Thickness(1),
            BorderBrush = GetResource<SolidColorBrush>("TextMuted"),
            Margin = new Thickness(0, 4, 0, 0),
            AcceptsReturn = true,
            MaxHeight = 150,
            MinHeight = 60,
            Height = 80,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap
        };
        tb.AddHandler(TextBox.TextChangedEvent, (s, e) => _parameterValues[paramName] = ((TextBox)s!).Text ?? "{}");
        return tb;
    }

    private Control CreateActionInput(string paramName)
    {
        var cb = new ComboBox
        {
            Width = 160,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 11
        };
        foreach (var item in new[] { "launch", "click", "type", "scroll_down", "scroll_up", "close" })
            cb.Items.Add(item);
        cb.AddHandler(ComboBox.SelectionChangedEvent, (s, e) =>
        {
            var sel = ((ComboBox)s!).SelectedItem as string;
            _parameterValues[paramName] = sel ?? string.Empty;
        });
        return cb;
    }

    private Control CreateTextBoxInput(string paramName)
    {
        var tb = new TextBox
        {
            Text = string.Empty,
            Foreground = GetResource<SolidColorBrush>("TextPrimary"),
            Background = GetResource<SolidColorBrush>("BgTertiary"),
            BorderThickness = new Thickness(1),
            BorderBrush = GetResource<SolidColorBrush>("TextMuted"),
            Margin = new Thickness(0, 4, 0, 0),
            Width = 200
        };
        tb.AddHandler(TextBox.TextChangedEvent, (s, e) => _parameterValues[paramName] = ((TextBox)s!).Text ?? string.Empty);
        return tb;
    }

    private T GetResource<T>(string name) where T : IBrush
    {
        var result = (T?)(this.FindResource(name) ?? Brushes.Gray);
        if (result == null)
            _logger?.LogDebug("ToolCallForm: Resource '{Name}' not found, using default", name);
        return result!;
    }

    private static string GetToolIcon(string toolName)
    {
        return toolName switch
        {
            "write_to_file" => "📝",
            "replace_in_file" => "✏️",
            "read_file" => "📄",
            "search_files" => "🔍",
            "list_files" => "📋",
            "execute_command" => "⚡",
            "browser_action" => "🌐",
            "use_mcp_tool" => "🔗",
            "access_mcp_resource" => "📡",
            "load_mcp_documentation" => "📚",
            "plan_mode_respond" => "💭",
            "act_mode_respond" => "🚀",
            "attempt_completion" => "✅",
            "new_task" => "🆕",
            "use_skill" => "🛠️",
            "use_subagents" => "🤖",
            "apply_patch" => "🩹",
            "generate_explanation" => "🔎",
            "web_fetch" => "🌍",
            "web_search" => "🔎",
            "ask_followup_question" => "❓",
            _ => "🔧"
        };
    }

    private async void OnExecuteClicked(object? sender, RoutedEventArgs e)
    {
        if (_onExecute != null)
        {
            await _onExecute(this);
            return;
        }

        ExecuteButton.IsEnabled = false;
        ExecuteButton.Content = "⏳";

        try
        {
            var result = await _toolExecutor.ExecuteAsync(_toolDefinition.Name, _parameterValues);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResultText.Text = result.Success
                    ? result.Output ?? "Success"
                    : $"Error: {result.Error}";
                ResultBorder.IsVisible = true;
                ResultBorder.Background = result.Success
                    ? GetResource<SolidColorBrush>("AccentGreen")
                    : GetResource<SolidColorBrush>("AccentRed");
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ToolCallForm: Error executing tool '{ToolName}'", _toolDefinition.Name);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResultText.Text = $"Error: {ex.Message}";
                ResultBorder.IsVisible = true;
                ResultBorder.Background = GetResource<SolidColorBrush>("AccentRed");
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ExecuteButton.IsEnabled = true;
                ExecuteButton.Content = "Execute";
            });
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (Interlocked.Exchange(ref _cancelled, 1) == 0)
        {
            if (_onCancel != null)
                _onCancel(this);
            else
                this.IsVisible = false;
        }
    }

    /// <summary>
    /// Called when the form is first loaded into the visual tree.
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        BuildForm();
    }

    /// <summary>
    /// Gets the collected parameter values.
    /// </summary>
    public IReadOnlyDictionary<string, object> GetParameters() => new ReadOnlyDictionary<string, object>(_parameterValues);

    /// <summary>
    /// Sets the value of a specific parameter.
    /// </summary>
    /// <param name="paramName">The parameter name to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when paramName is null or empty.</exception>
    public void SetParameterValue(string paramName, object value)
    {
        if (string.IsNullOrEmpty(paramName))
            throw new ArgumentNullException(nameof(paramName));
        _parameterValues[paramName] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Disposes the form and closes its popup.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (PopupReference != null)
        {
            try
            {
                PopupReference.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, false);
                PopupReference.Child = null;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "ToolCallForm: Error closing popup during dispose");
            }
            PopupReference = null;
        }

        if (Interlocked.Exchange(ref _cancelled, 1) == 0)
        {
            Cancelled?.Invoke();
        }
        GC.SuppressFinalize(this);
    }
}
