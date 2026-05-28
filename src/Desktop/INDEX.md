## src/Desktop/AboutWindow.axaml - 289 lines - Dark-themed About dialog
  - XAML window with embedded styles showing version badge (v1.0.0), runtime info (.NET 8, OS, arch), tech stack table, and external links. Fixed-size 448x341 popup.
## src/Desktop/AboutWindow.axaml.cs - 156 lines - AboutWindow code-behind
  - Reads version from assembly, git info, runtime/OS/arch. OnVersionBadgeClicked shows git log popup via Avalonia Popup control.
## src/Desktop/AccessibilityService.cs - 121 lines - AccessibilityService
  - Concrete implementation of IAccessibilityService. Detects high-contrast mode, screen reader support, sets accessibility properties on controls.
## src/Desktop/App.axaml - 8 lines - Minimal Avalonia Application root
  - Sets dark theme via RequestedThemeVariant="Dark", loads FluentTheme style.
## src/Desktop/App.axaml.cs - 277 lines - App code-behind
  - Static Main() entry point. Initialize() builds DI, creates MainWindow via DI. WriteFatalError() writes to debug console.
## src/Desktop/AssemblyInfo.cs - 7 lines - AssemblyInfo.cs
  - Sets AssemblyCopyright. References GeneratedGitInfo.cs for git commit/branch constants.
## src/Desktop/AvaloniaMarkdownRenderer.cs - 211 lines - AvaloniaMarkdownRenderer
  - IMarkdownRenderer implementation using Markdig with AdvancedExtensions, Abbreviations, Mathematics, YamlFrontMatter.
## src/Desktop/Controls/GitLogEntry.cs - 8 lines - GitLogEntry record
  - Simple record with Hash/Author/Message properties.
## src/Desktop/Controls/GitLogTable.cs - 440 lines - GitLogTable
  - Custom Avalonia control displaying git log entries in 3-column table (hash/author/message). Supports hover effects, selection, clipboard copy.
## src/Desktop/Controls/MainModelSelector.axaml - 113 lines - MainModelSelector
  - UserControl for displaying main AI model info: name, status badge, engine/backend, port, GPU memory bar, download progress.
## src/Desktop/Controls/MainModelSelector.axaml.cs - 549 lines - MainModelSelector code-behind
  - Manages MainAI model lifecycle: DiscoverModelsAsync, LoadModelAsync, Stop/Restart, advanced settings dialog.
## src/Desktop/Controls/PinguAvatar.axaml - 36 lines - PinguAvatar
  - 80x80 UserControl with white penguin body, black eyes/mouth. Blink overlay, mood-based body color.
## src/Desktop/Controls/PinguAvatar.axaml.cs - 137 lines - PinguAvatar code-behind
  - Listens to IPinguStore, updates mouth color/size, body color, blink overlay, bob animation via ScaleTransform.
## src/Desktop/Controls/PinguCanvas.cs - 120 lines - PinguCanvas
  - SkiaSharp-backed canvas control for rendering Pingu characters in the Avalonia visual tree.
## src/Desktop/Controls/PinguCharacterView.cs - 350 lines - PinguCharacterView
  - Full Pingu character rendering with animation, IK, and physics. Uses Avalonia DrawingContext with continuous render loop.
## src/Desktop/Controls/PinguHomeTile.axaml - 17 lines - PinguHomeTile
  - 120x120 border with BgSecondary background, CornerRadius=24, shows "Pingu" text centered.
## src/Desktop/Controls/PinguHomeTile.axaml.cs - 43 lines - PinguHomeTile code-behind
  - Implements IDisposable. OnTileClicked triggers _pingu.StartAwakeningSequenceAsync().
## src/Desktop/Controls/PinguPanel.axaml - 58 lines - PinguPanel
  - 320x480 panel with 6 tabs: Skills/Settings/Models/Compile/Logs/About. Uses TabControl.
## src/Desktop/Controls/PinguPanel.axaml.cs - 61 lines - PinguPanel code-behind
  - Implements IDisposable. SetPinguStore() subscribes to OnStateChanged event.
## src/Desktop/Controls/SystemModelSelector.axaml - 90 lines - SystemModelSelector
  - Similar to MainModelSelector but for SystemAI: name/status badge, model type indicator, engine/backend, port.
## src/Desktop/Controls/SystemModelSelector.axaml.cs - 515 lines - SystemModelSelector code-behind
  - Mirrors MainModelSelector for SystemAI: DiscoverModelsAsync, LoadModelAsync, Stop/Restart.
## src/Desktop/Controls/ToolCallForm.axaml - 61 lines - ToolCallForm
  - 420px wide UserControl with tool name/icon header, Execute/Cancel buttons, scrollable parameter form, green result area.
## src/Desktop/Controls/ToolCallForm.axaml.cs - 420 lines - ToolCallForm code-behind
  - Full-featured: BuildForm() generates input controls from ToolDefinition.ParameterSchema. Smart parameter detection.
## src/Desktop/Controls/VMConsole.axaml - 51 lines - VMConsole
  - Terminal-like control: 800x400, dark background, toolbar with Clear/Copy buttons, Canvas for terminal rendering.
## src/Desktop/Controls/VMConsole.axaml.cs - 302 lines - VMConsole code-behind
  - Full terminal emulator: 100x40 grid, Consolas font. ParseAnsi() parses ANSI escape sequences.
## src/Desktop/Controls/VMPanel.axaml - 43 lines - VMPanel
  - 400x500 sidebar panel for QEMU VM management: VM list, Start/Stop/Pause/Resume buttons, console output.
## src/Desktop/Controls/VMPanel.axaml.cs - 65 lines - VMPanel code-behind
  - Simple: InitializeViewModel() populates VM list, 4 async VM lifecycle methods.
## src/Desktop/Controls/VMWizardStep1.axaml - 43 lines - VMWizardStep1
  - Step 1 of 5 VM creation wizard: VM name TextBox, architecture ComboBox, accelerator ComboBox, hardware info.
## src/Desktop/Controls/VMWizardStep1.axaml.cs - 66 lines - VMWizardStep1 code-behind
  - DI constructor with IQEMUProcessManager. InitializeControls() populates architecture and accelerator enums.
## src/Desktop/Controls/VMWizardStep2.axaml - 50 lines - VMWizardStep2
  - Step 2 of 5 VM creation wizard: CPU cores slider (1-32), RAM slider (256-32768 MB), Disk size slider (1-500 GB).
## src/Desktop/Controls/VMWizardStep2.axaml.cs - 53 lines - VMWizardStep2 code-behind
  - Step 2: CPU/RAM/Disk sliders with ValueChanged handlers. GetForm() returns VMCreationForm.
## src/Desktop/Controls/VMWizardStep3.axaml - 31 lines - VMWizardStep3
  - Step 3 of 5 VM creation wizard: Disk images management. Add Disk button, ListBox for DiskImageConfig.
## src/Desktop/Controls/VMWizardStep3.axaml.cs - 67 lines - VMWizardStep3 code-behind
  - Step 3: Disk images management. OnAddDiskClicked uses StorageProvider.OpenFilePickerAsync.
## src/Desktop/Controls/VMWizardStep4.axaml - 31 lines - VMWizardStep4
  - Step 4 of 5 VM creation wizard: Network devices management. Add Network button, ListBox for NetworkDeviceConfig.
## src/Desktop/Controls/VMWizardStep4.axaml.cs - 52 lines - VMWizardStep4 code-behind
  - Simple: OnAddNetClicked creates new NetworkDeviceConfig with generated MAC address.
## src/Desktop/Controls/VMWizardStep5.axaml - 21 lines - VMWizardStep5
  - Step 5 of 5 VM creation wizard: Review and create summary. Read-only TextBox with Consolas font.
## src/Desktop/Controls/VMWizardStep5.axaml.cs - 44 lines - VMWizardStep5 code-behind
  - Minimal: UpdateReview() builds VM config summary from VMC using StringBuilder.
## src/Desktop/DependencyInjection.cs - 62 lines - DependencyInjection
  - Static class with AddInfrastructureServices() and AddDesktopServices(). Registers 25+ singleton services.
## src/Desktop/GeneratedGitInfo.cs - 8 lines - GeneratedGitInfo
  - Internal static GitInfo class with const Commit/Branch/Dirty/FullName/FullBranch/Log.
## src/Desktop/IAccessibilityService.cs - 27 lines - IAccessibilityService
  - Simple interface: IsHighContrastMode, IsScreenReaderMode, RefreshAccessibilityStateAsync.
## src/Desktop/MainWindow.axaml - 956 lines - MainWindow
  - Full application shell: 3-column layout (280px left sidebar, 4* center pane, 320px right sidebar). Dark theme with 30+ embedded styles.
## src/Desktop/MainWindow.axaml.cs - 1610 lines - MainWindow code-behind
  - Core window logic: DI constructor, tab management, chat title editing, message sending, Pingu avatar panel, AgentMode toggle, Safety toggles.
## src/Desktop/MainWindow.ChatMessages.cs - 645 lines - MainWindow.ChatMessages
  - Chat list management, message loading, message rendering, tool call display, context controls.
## src/Desktop/MainWindow.Context.cs - 250 lines - MainWindow.Context
  - Context budget management, context injection, context segment management, context compression, context segments panel.
## src/Desktop/MainWindow.Helpers.cs - 770 lines - MainWindow.Helpers
  - Message sending, server controls, model list, device status, DI resolution, window state, keyboard shortcuts, dialogs.
## src/Desktop/MainWindow.ImageGeneration.cs - 280 lines - MainWindow.ImageGeneration
  - Image generation: GenerateImageAsync, ImageGenModelSelector, resolution management, random seed.
## src/Desktop/MainWindow.Streaming.cs - 328 lines - MainWindow.Streaming
  - Streaming responses: GetAssistantResponseAsync, StreamResponseViaServerAsync (SSE), StreamResponseViaLocalServiceAsync.
## src/Desktop/MainWindow.TabManager.cs - 200 lines - MainWindow.TabManager
  - Tab navigation: ShowTab, SwitchToTab, UpdateActiveTab, pointer press handlers for all tabs.
## src/Desktop/OpenLMStudio.Desktop.csproj - 114 lines - OpenLMStudio.Desktop project file
  - SDK: Microsoft.NET.Sdk. WinExe, net8.0, nullable/implicit usings. Multi-platform with Avalonia 12.0.3.
## src/Desktop/PanelService.cs - 93 lines - PanelService
  - Static MainWindow reference pattern. IPanelService implementation with 14 panel names mapped to control names.
## src/Desktop/PluginManagementWindow.axaml - 120 lines - PluginManagementWindow
  - 900x700 plugin management dialog with dark theme, toolbar, plugin cards, status bar.
## src/Desktop/PluginManagementWindow.cs - 451 lines - PluginManagementWindow
  - Full plugin management: RefreshPlugins, RenderPluginCardsAsync, OnSearchTextChanged, OnTogglePluginClicked, OnPolicyChanged, OnUninstallPlugin.
## src/Desktop/TabService.cs - 109 lines - TabService
  - Static MainWindow reference pattern. ITabService implementation with 6 tabs and ToggleButton-based switching.
## src/Desktop/VMWizardWindow.axaml - 63 lines - VMWizardWindow
  - 600x550 Window for VM creation wizard. 3-row Grid with header, scrollable WizardContent, footer buttons.
## src/Desktop/VMWizardWindow.axaml.cs - 188 lines - VMWizardWindow code-behind
  - 5-step VM creation wizard. DI constructor with IQEMUProcessManager, HardwareDetector.