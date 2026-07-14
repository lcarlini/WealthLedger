# Backup & Restore

WealthLedger stores **all** of your data in a single local SQLite database. There is no cloud copy, so keeping your own backups is important. This guide explains exactly what to back up, how to do it safely, and how to restore.

> **You are responsible for your own backups.** The software is provided "as is" with no warranty.

---

## Where your data lives

| Environment | Database location |
|-------------|-------------------|
| **Development** (`dotnet run` in `Source/Run/WebApp`) | `Source/Run/WebApp/wealthledger.db` |
| **Production deploy** (`deploy/publish.ps1`) | `C:\LocalApps\WealthLedger\wealthledger.db` |

The database path is **logged on startup**:

```
Using SQLite database at <full path to wealthledger.db>
```

Check that line if you are unsure which file is live.

### The three database files

SQLite uses **Write-Ahead Logging (WAL)**, so at any moment your data may be spread across three files:

| File | Purpose |
|------|---------|
| `wealthledger.db` | Main database |
| `wealthledger.db-wal` | Write-ahead log (recent, not-yet-merged changes) |
| `wealthledger.db-shm` | Shared-memory index for the WAL |

> ⚠️ **Do not copy only `wealthledger.db` while the app is running.** Recent changes may still be in the `-wal` file. Copying the `.db` alone can produce a stale or inconsistent backup.

---

## Backing up

### Option A — App stopped (recommended, simplest)

1. Stop the WealthLedger app.
2. Copy the main database file to a safe location:

```powershell
Copy-Item "C:\LocalApps\WealthLedger\wealthledger.db" "C:\Backups\wealthledger-$(Get-Date -Format 'yyyy-MM-dd').db"
```

When the app is stopped cleanly, SQLite merges the WAL back into the main file, so a single `.db` copy is complete.

### Option B — App running

Copy **all three** files together so the WAL is preserved:

```powershell
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$src = "C:\LocalApps\WealthLedger"
$dst = "C:\Backups\wealthledger-$stamp"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item "$src\wealthledger.db"     "$dst\" -ErrorAction SilentlyContinue
Copy-Item "$src\wealthledger.db-wal" "$dst\" -ErrorAction SilentlyContinue
Copy-Item "$src\wealthledger.db-shm" "$dst\" -ErrorAction SilentlyContinue
```

### Option C — Consistent single-file backup with the sqlite3 CLI

If you have the [`sqlite3`](https://www.sqlite.org/download.html) CLI, the `.backup` command produces a single, consistent file even while the app runs:

```powershell
sqlite3 "C:\LocalApps\WealthLedger\wealthledger.db" ".backup 'C:\Backups\wealthledger-clean.db'"
```

---

## Restoring

> **Always stop the app before restoring.** Restoring over a live database will corrupt it.

### Using the helper script (Windows)

```powershell
./deploy/restore-db.ps1 -Source "C:\Backups\wealthledger-2026-07-14.db"
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Source` | largest `wealthledger.db` found in the repo | Path to the backup file to restore |
| `-TargetDir` | `C:\LocalApps\WealthLedger` | Install/deploy folder to restore into |

The script copies your backup into place and removes any stale `-wal` / `-shm` sidecar files so the restored database is used cleanly.

### Manual restore

```powershell
# 1. Stop the app first.
# 2. Replace the database and clear stale sidecars:
Copy-Item "C:\Backups\wealthledger-2026-07-14.db" "C:\LocalApps\WealthLedger\wealthledger.db" -Force
Remove-Item "C:\LocalApps\WealthLedger\wealthledger.db-wal","C:\LocalApps\WealthLedger\wealthledger.db-shm" -ErrorAction SilentlyContinue
# 3. Start the app again.
```

If you backed up all three files (Option B), restore all three together and then start the app.

---

## Deploys never destroy your data

`deploy/publish.ps1` is designed to be safe to re-run:

1. It **backs up** the live database files in memory before publishing.
2. It publishes the new API build and SPA into the install folder.
3. It **restores** your database afterward — so an upgrade never overwrites your data.

Even so, keep independent backups (Options A–C) before major upgrades.

---

## Scheduling automatic backups (optional)

On Windows you can schedule a daily copy with Task Scheduler. Save this as `backup-wealthledger.ps1`:

```powershell
$stamp = Get-Date -Format 'yyyy-MM-dd'
$src   = "C:\LocalApps\WealthLedger\wealthledger.db"
$dstDir = "C:\Backups\WealthLedger"
New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
Copy-Item $src "$dstDir\wealthledger-$stamp.db" -Force

# Keep only the 30 most recent backups
Get-ChildItem "$dstDir\wealthledger-*.db" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip 30 |
    Remove-Item -Force
```

Then register it (runs daily at 02:00):

```powershell
$action  = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-File C:\path\to\backup-wealthledger.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
Register-ScheduledTask -TaskName "WealthLedger Backup" -Action $action -Trigger $trigger
```

> For extra safety, sync `C:\Backups\WealthLedger` to an external drive or your own private, encrypted storage.

---

## Moving to a new machine

1. Back up the database on the old machine (Option A).
2. Install prerequisites and deploy WealthLedger on the new machine (see the [README](../README.md)).
3. Copy your backup file across.
4. Restore it with `deploy/restore-db.ps1` (or manually), then start the app.

Your entire financial history moves with that one `.db` file.
