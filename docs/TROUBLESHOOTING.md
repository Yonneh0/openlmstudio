# Troubleshooting Guide

## Server Issues

### Server Won't Start
**Symptoms:** Server status shows "Stopped" after clicking Start Server.
- Check that port 8080 is not in use: `netstat -ano | findstr 8080` (Windows) or `lsof -i :8080` (macOS/Linux)
- Try a different port in Settings → Server
- Verify HTTPS certificate permissions (may require admin on Windows)
- Check logs in the developer console (F12) for specific error messages

### Server Connection Refused
**Symptoms:** API calls to localhost:8080 fail with "connection refused."
- Verify the server is running (status should show "Running")
- Check the port displayed in the status bar matches your API calls
- Ensure no firewall is blocking localhost connections

## Model Loading Issues

### Model Fails to Load
**Symptoms:** "Failed to load model" error when loading a model.
- Verify the model file is not corrupted:
  ```bash
  # Windows
  CertUtil -hashfile model.gguf SHA256
  # macOS/Linux
  sha256sum model.gguf
  ```
- Ensure the file is in the correct format (GGUF for text, Safetensors for image)
- Check available VRAM: `nvidia-smi` (NVIDIA) or `system_profiler SPDisplaysDataType` (macOS)
- Models larger than available VRAM will fall back to CPU (slower)

### OOM (Out of Memory) Errors
**Symptoms:** Application crashes or model loading fails with "out of memory."
- Reduce model quantization (use Q4 instead of Q8)
- Close other GPU-intensive applications
- Reduce GPU offloading in Settings → Model
- Enable CPU fallback for models larger than VRAM

### Model Loads But Produces Garbage Output
**Symptoms:** Model responds with gibberish or nonsensical text.
- Try a different quantization level
- Ensure you're using the correct model for the task (text model for chat, image model for generation)
- Verify the model file was downloaded completely (check SHA256)

## Image Generation Issues

### Images Not Generating
**Symptoms:** Generation button clicks but no output appears.
- Verify an image generation model is loaded (check Image Gen tab)
- Check the ONNX Runtime installation
- Look for errors in the developer console
- Try a simpler prompt and lower step count

### Generation Takes Too Long
**Symptoms:** Image generation takes minutes per image.
- Reduce the number of steps
- Use a smaller resolution (512x512 instead of 1024x1024)
- Use a faster sampler (Euler instead of DPM++)
- Consider using Flux Fast instead of Flux Dev

### Low Quality Images
**Symptoms:** Generated images look blurry or artifacts are visible.
- Increase the number of steps (30+ for best quality)
- Increase CFG scale slightly (7.5-8.5)
- Use a higher resolution
- Try a different model (Flux Dev for highest quality)

## Context Management Issues

### Context Budget Exceeded
**Symptoms:** Messages disappear or responses become degraded.
- Reduce context window size in Settings
- Pin important messages (📌) to prevent compression
- Use the Suppress button (👁️) to hide irrelevant messages
- Switch to a model with a larger context window

### Pin/Suppress Buttons Not Working
**Symptoms:** Clicking pin or suppress buttons has no effect.
- Ensure a chat is selected (not just created)
- Refresh the chat by clicking on it again
- Check for errors in the developer console

## Performance Issues

### Application Is Slow
**Symptoms:** UI lags, scrolling is stuttery, buttons respond slowly.
- Close unused chats
- Reduce the number of loaded models
- Check for disk I/O bottlenecks
- On Windows, ensure power settings are set to "High Performance"

### High Memory Usage
**Symptoms:** System slows down when OpenLMStudio is running.
- Unload unused models (Models tab → Unload Model)
- Close chats you're not actively using
- Enable memory-efficient context compression
- Check Settings → Model for offloading options

## Plugin Issues

### Plugin Installation Fails
**Symptoms:** "Failed to install plugin" error.
- Check internet connection
- Verify the registry URL is correct
- Ensure the plugin is compatible with your version
- Check disk space

### Plugin Crashes Application
**Symptoms:** App crashes when a plugin is enabled.
- Disable the plugin in Settings → Plugins
- Re-enable plugins one at a time to identify the culprit
- Update the plugin to the latest version

## Network Issues

### HuggingFace Download Fails
**Symptoms:** Model download from HuggingFace fails or hangs.
- Check your internet connection
- Verify HuggingFace access token is set correctly
- Try downloading manually and placing in the models directory
- For large files, check available disk space

### MCP Server Connection Fails
**Symptoms:** "Failed to connect to MCP server" error.
- Verify the MCP server is running
- Check the stdio command is correct
- Ensure the server binary is executable (Linux/macOS)
- Check firewall settings

## General Fixes

### Reset Application Settings
1. Close OpenLMStudio
2. Delete the settings file:
   - **Windows**: `%APPDATA%\OpenLMStudio\settings.json`
   - **macOS**: `~/Library/Application Support/OpenLMStudio/settings.json`
   - **Linux**: `~/.config/OpenLMStudio/settings.json`
3. Restart the application

### Clear Cache
1. Close OpenLMStudio
2. Delete the cache directory:
   - **Windows**: `%APPDATA%\OpenLMStudio\cache\`
   - **macOS**: `~/Library/Caches/OpenLMStudio/`
   - **Linux**: `~/.cache/OpenLMStudio/`
3. Restart the application

### View Logs
Logs are stored in the application's data directory:
- **Windows**: `%APPDATA%\OpenLMStudio\logs\`
- **macOS**: `~/Library/Logs/OpenLMStudio/`
- **Linux**: `~/.config/OpenLMStudio/logs/`

### Developer Console
Press F12 to open the developer console for debugging output and error messages.

## Still Having Issues?

If you're experiencing problems not covered here:
1. Check the [GitHub Issues](https://github.com/Yonneh0/openlmstudio/issues) for similar reports
2. Gather relevant log files
3. Note your OS, version, and steps to reproduce
4. Open a new issue with the above information