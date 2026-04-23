using Microsoft.Data.SqlClient;
using Project_Minutes.Configuration;
using Project_Minutes.Data;
using Project_Minutes.Models;
using Project_Minutes.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    return AppConfiguration.Load(env.ContentRootPath);
});
builder.Services.AddSingleton<SqlDatabase>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<MeetingRepository>();
builder.Services.AddSingleton<MinuteRepository>();
builder.Services.AddSingleton<TaskRepository>();
builder.Services.AddSingleton<ParticipantRepository>();
builder.Services.AddSingleton<SignatureRepository>();
builder.Services.AddSingleton<TaskSignatureRepository>();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

await ApplySchemaAsync(app.Services).ConfigureAwait(false);

var users = app.Services.GetRequiredService<UserRepository>();
var meetings = app.Services.GetRequiredService<MeetingRepository>();
var minutes = app.Services.GetRequiredService<MinuteRepository>();
var tasks = app.Services.GetRequiredService<TaskRepository>();
var participants = app.Services.GetRequiredService<ParticipantRepository>();
var signatures = app.Services.GetRequiredService<SignatureRepository>();
var taskSignatures = app.Services.GetRequiredService<TaskSignatureRepository>();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/health/ready", async (CancellationToken ct) =>
{
    await ApplySchemaAsync(app.Services).ConfigureAwait(false);
    return Results.Ok(new { ready = true });
});

app.MapPost("/api/auth/login", async (LoginBody body, CancellationToken ct) =>
{
    try
    {
        var u = await users.LoginAdministratorAsync(body.Username, body.Password, ct).ConfigureAwait(false);
        return u is null ? Results.Unauthorized() : Results.Ok(u);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/api/auth/admin-count", async (CancellationToken ct) =>
    Results.Ok(await users.CountActiveAdministratorsAsync(ct).ConfigureAwait(false)));

app.MapPost("/api/auth/register-first", async (RegisterBody body, CancellationToken ct) =>
{
    try
    {
        var u = await users.RegisterFirstAdministratorAsync(body.DisplayName, body.Email, body.Username, body.Password, ct)
            .ConfigureAwait(false);
        return Results.Ok(u);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/auth/register", async (RegisterBody body, CancellationToken ct) =>
{
    try
    {
        var u = await users.RegisterAdministratorAsync(body.DisplayName, body.Email, body.Username, body.Password, ct)
            .ConfigureAwait(false);
        return Results.Ok(u);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/users", async (CancellationToken ct) =>
    Results.Ok(await users.GetAllAsync(ct).ConfigureAwait(false)));

app.MapPost("/api/users", async (NewUserBody body, CancellationToken ct) =>
{
    var id = await users.AddAsync(body.Name, body.Email, ct).ConfigureAwait(false);
    return Results.Ok(new { userId = id });
});

app.MapGet("/api/meetings", async (CancellationToken ct) =>
    Results.Ok(await meetings.GetAllAsync(ct).ConfigureAwait(false)));

app.MapPost("/api/meetings", async (MeetingCreateBody body, CancellationToken ct) =>
{
    var id = await meetings.AddAsync(body.Title, body.MeetingDate, body.MeetingTime, ct).ConfigureAwait(false);
    return Results.Ok(new { meetingId = id });
});

app.MapPut("/api/meetings/{meetingId:int}", async (int meetingId, MeetingUpdateBody body, CancellationToken ct) =>
{
    try
    {
        await meetings.UpdateAsync(meetingId, body.Title, body.MeetingDate, body.MeetingTime, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapDelete("/api/meetings/{meetingId:int}", async (int meetingId, CancellationToken ct) =>
{
    try
    {
        await meetings.DeleteAsync(meetingId, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/api/minutes", async (CancellationToken ct) =>
    Results.Ok(await minutes.GetAllAsync(ct).ConfigureAwait(false)));

app.MapGet("/api/minutes/list", async (int? meetingId, CancellationToken ct) =>
    Results.Ok(await minutes.GetListItemsAsync(meetingId, ct).ConfigureAwait(false)));

app.MapPost("/api/minutes", async (MinuteCreateBody body, CancellationToken ct) =>
{
    var id = await minutes.AddAsync(body.MeetingId, body.Content, ct).ConfigureAwait(false);
    return Results.Ok(new { minuteId = id });
});

app.MapPut("/api/minutes/{minuteId:int}", async (int minuteId, MinuteUpdateBody body, CancellationToken ct) =>
{
    try
    {
        await minutes.UpdateAsync(minuteId, body.Content, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapDelete("/api/minutes/{minuteId:int}", async (int minuteId, CancellationToken ct) =>
{
    try
    {
        await minutes.DeleteAsync(minuteId, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/api/minutes/{minuteId:int}/signatures", async (int minuteId, CancellationToken ct) =>
    Results.Ok(await signatures.GetAllPngByUserForMinuteAsync(minuteId, ct).ConfigureAwait(false)));

app.MapPut("/api/minutes/{minuteId:int}/signatures/{userId:int}",
    async (int minuteId, int userId, PngBody body, CancellationToken ct) =>
    {
        await signatures.UpsertMinuteUserAsync(minuteId, userId, body.Png, ct).ConfigureAwait(false);
        return Results.NoContent();
    });

app.MapDelete("/api/minutes/{minuteId:int}/signatures/{userId:int}", async (int minuteId, int userId, CancellationToken ct) =>
{
    await signatures.DeleteMinuteUserAsync(minuteId, userId, ct).ConfigureAwait(false);
    return Results.NoContent();
});

app.MapGet("/api/meetings/{meetingId:int}/participants", async (int meetingId, CancellationToken ct) =>
    Results.Ok(await participants.GetByMeetingAsync(meetingId, ct).ConfigureAwait(false)));

app.MapPost("/api/meetings/{meetingId:int}/participants", async (int meetingId, ParticipantAddBody body, CancellationToken ct) =>
{
    await participants.AddIfNotExistsAsync(meetingId, body.UserId, body.Position, ct).ConfigureAwait(false);
    return Results.NoContent();
});

app.MapDelete("/api/meetings/{meetingId:int}/participants/{userId:int}", async (int meetingId, int userId, CancellationToken ct) =>
{
    await participants.RemoveAsync(meetingId, userId, ct).ConfigureAwait(false);
    return Results.NoContent();
});

app.MapGet("/api/minutes/{minuteId:int}/tasks", async (int minuteId, CancellationToken ct) =>
    Results.Ok(await tasks.GetByMinuteIdAsync(minuteId, ct).ConfigureAwait(false)));

app.MapPost("/api/tasks", async (TaskCreateBody body, CancellationToken ct) =>
{
    var id = await tasks.AddAsync(body.MinuteId, body.Title, body.ResponsibleUserId, body.DueDate, ct).ConfigureAwait(false);
    return Results.Ok(new { taskId = id });
});

app.MapDelete("/api/tasks/{taskId:int}", async (int taskId, CancellationToken ct) =>
{
    try
    {
        await tasks.DeleteAsync(taskId, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapPut("/api/tasks/{taskId:int}/signature",
    async (int taskId, TaskSignBody body, CancellationToken ct) =>
    {
        await taskSignatures.UpsertAsync(taskId, body.UserId, body.Png, ct).ConfigureAwait(false);
        return Results.NoContent();
    });

app.MapGet("/api/tasks/{taskId:int}/signature", async (int taskId, CancellationToken ct) =>
{
    var png = await taskSignatures.GetPngAsync(taskId, ct).ConfigureAwait(false);
    return png is null ? Results.NotFound() : Results.Bytes(png, "image/png");
});

app.Run();

static async Task ApplySchemaAsync(IServiceProvider services)
{
    var cfg = services.GetRequiredService<AppConfiguration>();
    await using var c = new SqlConnection(cfg.MeetingMinutesConnectionString);
    await c.OpenAsync().ConfigureAwait(false);
    await DatabaseSchemaInitializer.EnsureExtendedSchemaAsync(c).ConfigureAwait(false);
}

internal sealed record LoginBody(string Username, string Password);

internal sealed record RegisterBody(string DisplayName, string? Email, string Username, string Password);

internal sealed record NewUserBody(string Name, string? Email);

internal sealed record MeetingCreateBody(string? Title, DateTime MeetingDate, TimeSpan MeetingTime);

internal sealed record MeetingUpdateBody(string? Title, DateTime MeetingDate, TimeSpan MeetingTime);

internal sealed record MinuteCreateBody(int MeetingId, string Content);

internal sealed record MinuteUpdateBody(string Content);

internal sealed record ParticipantAddBody(int UserId, string? Position);

internal sealed record TaskCreateBody(int MinuteId, string Title, int? ResponsibleUserId, DateTime? DueDate);

internal sealed record PngBody(byte[] Png);

internal sealed record TaskSignBody(int UserId, byte[] Png);
