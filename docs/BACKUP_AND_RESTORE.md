# Backup & Restore

WealthLedger stores **all** of your data in a single local SQLite database. There is no cloud copy, so keep backups. WealthLedger now creates them for you; this guide explains how they work, how to manage them in **Settings → Backups**, and how to restore.

> **You are responsible for your own backups.** Keep at least one copy somewhere else (external drive, private encrypted storage). The software is provided "as is" with no warranty.

---

## Where your data lives

The database folder is configured through `WealthLedger:DataDirectory` (or the `WEALTHLEDGER_DATA_DIR` environment variable). The live path is **logged on startup**:

```
Using SQLite database at <full path to wealthledger.db>
```

Backups are stored in a `Backups` folder **next to the database file**:

```
<DataDirectory>\
    wealthledger.db
    backup-settings.json      <- your backup policy
    Backups\
        wealthledger-20260714-020000-scheduled.db
        wealthledger-20260713-101500-manual.db
        ...
```

---

## Automatic backups

| Setting | Default | Range |
|---------|---------|-------|
| Create backups automatically | On | on / off |
| Every (days) | 15 | 1 – 365 |
| Maximum copies kept | 10 | 1 – 100 |

- The app checks **at startup and then every hour**. A scheduled backup runs when the newest backup is older than the interval.
- Once the maximum is reached, the **oldest** copies are deleted automatically.
- Manual backups, pre-upgrade, pre-import and pre-restore copies all count toward the maximum.

### Backups created by the app itself

| Reason | When it is created |
|--------|--------------------|
| `scheduled` | Automatic, per the interval above |
| `manual` | When you press **Back up now** |
| `pre-upgrade` | Before a release changes the database schema (only when an upgrade is actually needed) |
| `pre-import` | Before a JSON import overwrites existing rows |
| `pre-restore` | Automatically, before any restore replaces your data |

Each backup is a consistent snapshot made with SQLite's `VACUUM INTO`, so it is safe to take while the app is in use. Every new copy is checked with an integrity check before it is kept.

---

## Managing backups in Settings

Open **Settings** (gear icon in the toolbar) and go to the **Backups** section. You can:

- See the last backup, the next scheduled one, how many copies are kept, and the backup folder.
- Change the policy (on/off, interval, maximum copies) and **Save settings**.
- **Back up now**.
- **Download** a copy (for example, to store it on another drive).
- **Restore** a copy (see below).
- **Delete** a copy you no longer need.

---

## Restoring

Restoring is deliberately two-step, so the live database is never replaced while it is in use:

1. In **Settings → Backups**, press **Restore** on the copy you want and confirm.
   - A `pre-restore` safety copy of your *current* data is created first.
   - The restore is **staged** (a `restore.pending` marker is written). Nothing has changed yet.
2. **Restart WealthLedger.** On startup the staged backup is applied before the database is opened.

If a staged backup is missing or fails its integrity check, the restore is cancelled, your current data is kept, and the error is written to the log.

### Restoring without the app (last resort)

If the app cannot start, stop it first, then:

1. Copy the chosen file from `Backups\` over `wealthledger.db` in your data folder.
2. Delete `wealthledger.db-wal` and `wealthledger.db-shm` if they exist.
3. Start the app again.

---

## JSON export and import

The JSON export (`GET api/portfolio/export-json`) writes a full copy of every table, including bills, income settings and statement imports. Importing a JSON file (`POST api/portfolio/import-json`):

- takes an automatic `pre-import` backup first;
- runs as **one transaction**, so a bad file leaves your data exactly as it was;
- is refused if the file was written by a newer WealthLedger version.

JSON export is a portable copy, not a replacement for database backups.

---

## Deploys and upgrades

- Your database is **never stored in `publish/`**. If the deploy script finds a database file there, it **moves** it into a timestamped `wealthledger-legacy-db-…` folder next to `publish/`. It does not delete it. Point `WealthLedger:DataDirectory` at the folder that holds your live database.
- Before a release changes the database schema, the app takes a `pre-upgrade` backup. Keep your own copies before major upgrades as well.

---

## Moving to a new machine

1. In **Settings → Backups**, press **Back up now**, then **Download** the newest copy (or copy the whole `Backups` folder).
2. Install and deploy WealthLedger on the new machine (see the [README](../README.md)).
3. Set `WealthLedger:DataDirectory` to the new folder, place the database file there as `wealthledger.db`, and start the app.

Your entire financial history moves with that one `.db` file.
