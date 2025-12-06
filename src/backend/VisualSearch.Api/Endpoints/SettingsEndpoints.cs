using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Settings management endpoints for admin panel.
/// </summary>
public static class SettingsEndpoints
{
    /// <summary>
    /// Maps the settings endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("Settings");

        // Public endpoints (for reading settings by frontend)
        group.MapGet("/public", HandleGetPublicSettingsAsync)
            .Produces<IReadOnlyList<SettingDto>>(200)
            .WithName("GetPublicSettings")
            .WithDescription("Gets all public settings for the frontend.");

        group.MapGet("/public/{key}", HandleGetPublicSettingAsync)
            .Produces<SettingDto>(200)
            .Produces(404)
            .WithName("GetPublicSetting")
            .WithDescription("Gets a specific public setting by key.");

        // Admin endpoints (requires authentication)
        group.MapGet("/", HandleGetAllSettingsAsync)
            .RequireAuthorization("Admin")
            .Produces<IReadOnlyList<SettingDto>>(200)
            .WithName("GetAllSettings")
            .WithDescription("Gets all settings (admin only).");

        group.MapGet("/{key}", HandleGetSettingAsync)
            .RequireAuthorization("Admin")
            .Produces<SettingDto>(200)
            .Produces(404)
            .WithName("GetSetting")
            .WithDescription("Gets a specific setting by key (admin only).");

        group.MapPut("/{key}", HandleUpdateSettingAsync)
            .RequireAuthorization("Admin")
            .Produces<SettingDto>(200)
            .Produces(404)
            .WithName("UpdateSetting")
            .WithDescription("Updates a setting value (admin only).");

        group.MapPost("/", HandleCreateSettingAsync)
            .RequireAuthorization("Admin")
            .Produces<SettingDto>(201)
            .Produces(400)
            .WithName("CreateSetting")
            .WithDescription("Creates a new setting (admin only).");

        group.MapPost("/invalidate-cache", HandleInvalidateCacheAsync)
            .RequireAuthorization("Admin")
            .Produces(200)
            .WithName("InvalidateCache")
            .WithDescription("Invalidates the settings cache and notifies all clients (admin only).");

        // SSE endpoint for settings change notifications
        app.MapGet("/api/settings/sse", HandleSseConnectionAsync)
            .Produces(200, contentType: "text/event-stream")
            .WithName("SettingsSSE")
            .WithTags("Settings")
            .WithDescription("Server-Sent Events endpoint for real-time settings change notifications.");
    }

    private static async Task<IResult> HandleGetPublicSettingsAsync(
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAllAsync(cancellationToken);

        // Filter to only public categories
        var publicSettings = settings
            .Where(s => s.Category is "ui" or "search")
            .Select(s => new SettingDto
            {
                Key = s.Key,
                Value = s.Value,
                Type = s.Type.ToString().ToLowerInvariant(),
                Category = s.Category,
                Description = s.Description
            })
            .ToList();

        return Results.Ok(publicSettings);
    }

    private static async Task<IResult> HandleGetPublicSettingAsync(
        string key,
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAllAsync(cancellationToken);
        var setting = settings.FirstOrDefault(s => s.Key == key);

        if (setting is null || setting.Category is not "ui" and not "search")
        {
            return Results.NotFound(new { error = "Setting not found" });
        }

        return Results.Ok(new SettingDto
        {
            Key = setting.Key,
            Value = setting.Value,
            Type = setting.Type.ToString().ToLowerInvariant(),
            Category = setting.Category,
            Description = setting.Description
        });
    }

    private static async Task<IResult> HandleGetAllSettingsAsync(
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAllAsync(cancellationToken);

        var result = settings.Select(s => new SettingDto
        {
            Key = s.Key,
            Value = s.Value,
            Type = s.Type.ToString().ToLowerInvariant(),
            Category = s.Category,
            Description = s.Description,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSettingAsync(
        string key,
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAllAsync(cancellationToken);
        var setting = settings.FirstOrDefault(s => s.Key == key);

        if (setting is null)
        {
            return Results.NotFound(new { error = "Setting not found" });
        }

        return Results.Ok(new SettingDto
        {
            Key = setting.Key,
            Value = setting.Value,
            Type = setting.Type.ToString().ToLowerInvariant(),
            Category = setting.Category,
            Description = setting.Description,
            UpdatedAt = setting.UpdatedAt
        });
    }

    private static async Task<IResult> HandleUpdateSettingAsync(
        string key,
        UpdateSettingRequest request,
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
        {
            return Results.BadRequest(new { error = "Value is required" });
        }

        var setting = await settingsService.UpdateAsync(key, request.Value, cancellationToken);

        if (setting is null)
        {
            return Results.NotFound(new { error = "Setting not found" });
        }

        return Results.Ok(new SettingDto
        {
            Key = setting.Key,
            Value = setting.Value,
            Type = setting.Type.ToString().ToLowerInvariant(),
            Category = setting.Category,
            Description = setting.Description,
            UpdatedAt = setting.UpdatedAt
        });
    }

    private static async Task<IResult> HandleCreateSettingAsync(
        CreateSettingRequest request,
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
        {
            return Results.BadRequest(new { error = "Key and value are required" });
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return Results.BadRequest(new { error = "Category is required" });
        }

        var settingType = request.Type?.ToLowerInvariant() switch
        {
            "integer" => SettingType.Integer,
            "boolean" => SettingType.Boolean,
            "decimal" => SettingType.Decimal,
            _ => SettingType.String
        };

        var setting = new Setting
        {
            Key = request.Key,
            Value = request.Value,
            Type = settingType,
            Category = request.Category,
            Description = request.Description
        };

        var created = await settingsService.CreateAsync(setting, cancellationToken);

        return Results.Created($"/api/settings/{created.Key}", new SettingDto
        {
            Key = created.Key,
            Value = created.Value,
            Type = created.Type.ToString().ToLowerInvariant(),
            Category = created.Category,
            Description = created.Description,
            UpdatedAt = created.UpdatedAt
        });
    }

    private static async Task<IResult> HandleInvalidateCacheAsync(
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        await settingsService.BroadcastCacheInvalidationAsync(cancellationToken);
        return Results.Ok(new { message = "Cache invalidated and clients notified" });
    }

    private static async Task HandleSseConnectionAsync(
        HttpContext context,
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        var clientId = Guid.NewGuid().ToString();
        var writer = new StreamWriter(context.Response.Body);

        settingsService.RegisterSseClient(clientId, writer);

        try
        {
            // Send initial connection message
            await writer.WriteAsync("event: connected\ndata: {\"clientId\":\"" + clientId + "\"}\n\n");
            await writer.FlushAsync();

            // Keep connection alive with heartbeats
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                // Send heartbeat
                await writer.WriteAsync(": heartbeat\n\n");
                await writer.FlushAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        catch (Exception)
        {
            // Connection error
        }
        finally
        {
            settingsService.UnregisterSseClient(clientId);
        }
    }
}

/// <summary>
/// Setting DTO for API responses.
/// </summary>
public sealed record SettingDto
{
    /// <summary>Gets or sets the setting key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets or sets the setting value.</summary>
    public required string Value { get; init; }

    /// <summary>Gets or sets the data type.</summary>
    public required string Type { get; init; }

    /// <summary>Gets or sets the category.</summary>
    public required string Category { get; init; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets or sets when the setting was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Request to update a setting.
/// </summary>
public sealed record UpdateSettingRequest
{
    /// <summary>Gets or sets the new value.</summary>
    public required string Value { get; init; }
}

/// <summary>
/// Request to create a new setting.
/// </summary>
public sealed record CreateSettingRequest
{
    /// <summary>Gets or sets the setting key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets or sets the setting value.</summary>
    public required string Value { get; init; }

    /// <summary>Gets or sets the data type.</summary>
    public string? Type { get; init; }

    /// <summary>Gets or sets the category.</summary>
    public required string Category { get; init; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; init; }
}
