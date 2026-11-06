using System.Diagnostics;
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

    /// <summary>
    /// Opens the backup folder in the OS file manager. This only makes sense when the app runs on the
    /// user's own machine, so requests that do not come from the loopback interface are refused.
    /// </summary>
    [HttpPost("open-folder")]
    public async Task<Response<BackupStatus>> OpenFolderAsync(CancellationToken ct)
    {
        var remote = HttpContext.Connection.RemoteIpAddress;
        if (remote is null || !IPAddress.IsLoopback(remote))
            return new Response<BackupStatus>([new Error("The folder can only be opened from the machine running WealthLedger.")], HttpStatusCode.Forbidden);

        try
        {
            var status = await backups.GetStatusAsync(ct);
            Directory.CreateDirectory(status.BackupDirectory);

            var info = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("explorer.exe")
                : OperatingSystem.IsMacOS() ? new ProcessStartInfo("open") : new ProcessStartInfo("xdg-open");
            info.ArgumentList.Add(status.BackupDirectory);
            info.UseShellExecute = false;
            Process.Start(info)?.Dispose();
            return status;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new Response<BackupStatus>([new Error($"Could not open the folder: {ex.Message}")], HttpStatusCode.InternalServerError);
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
