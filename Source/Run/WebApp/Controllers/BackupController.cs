using System.Net;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Backups;
using WealthLedger.Infrastructure.Backup;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/backups")]
public class BackupController(IBackupService backups) : ControllerBase
{
    [HttpGet("status")]
    public async Task<Response<BackupStatus>> GetStatusAsync(CancellationToken ct)
        => await Run(() => backups.GetStatusAsync(ct));

    [HttpGet]
    public async Task<Response<IReadOnlyList<BackupInfo>>> ListAsync(CancellationToken ct)
        => await Run(() => backups.ListAsync(ct));

    [HttpPut("settings")]
    public async Task<Response<BackupSettings>> UpdateSettingsAsync([FromBody] BackupSettings settings, CancellationToken ct)
        => await Run(() => backups.UpdateSettingsAsync(settings, ct));

    [HttpPost]
    public async Task<Response<BackupInfo>> CreateAsync(CancellationToken ct)
        => await Run(() => backups.CreateAsync(BackupService.ReasonManual, ct));

    [HttpGet("{fileName}/download")]
    public async Task<IActionResult> DownloadAsync(string fileName, CancellationToken ct)
    {
        try
        {
            var path = await backups.GetBackupFilePathAsync(fileName, ct);
            return PhysicalFile(Path.GetFullPath(path), "application/octet-stream", fileName);
        }
        catch (ArgumentException ex)
        {
            return ToResult<IActionResult>(ex);
        }
        catch (FileNotFoundException ex)
        {
            return ToResult<IActionResult>(ex);
        }
    }

    [HttpPost("{fileName}/restore")]
    public async Task<Response<BackupStatus>> RestoreAsync(string fileName, CancellationToken ct)
    {
        try
        {
            await backups.StageRestoreAsync(fileName, ct);
            return await backups.GetStatusAsync(ct);
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException)
        {
            return ToResult<BackupStatus>(ex);
        }
    }

    [HttpDelete("{fileName}")]
    public async Task<Response<BackupStatus>> DeleteAsync(string fileName, CancellationToken ct)
    {
        try
        {
            await backups.DeleteAsync(fileName, ct);
            return await backups.GetStatusAsync(ct);
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException)
        {
            return ToResult<BackupStatus>(ex);
        }
    }

    private async Task<Response<T>> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException)
        {
            return ToResult<T>(ex);
        }
    }

    private static Response<T> ToResult<T>(Exception ex)
    {
        var status = ex switch
        {
            FileNotFoundException => HttpStatusCode.NotFound,
            ArgumentException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.Conflict,
        };
        return new Response<T>([new Error(ex.Message)], status);
    }
}
