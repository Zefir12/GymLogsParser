## What
<!-- Short summary of the change: data/API, migration, UI. -->

Closes #

## Why

## How it was tested
- [ ] `dotnet build GymLogsParser.sln` green (backend changes)
- [ ] `dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project GymLogsParser` says none (model or migration changes)
- [ ] `pnpm build` in UI/gymlogsparser green (frontend changes)
- [ ] Checked in a browser, including a phone-width layout (UI changes)
<!-- Say which checks could not run on this host, and what zefir should run or click through, with SQL to verify data migrations. -->

## Notes for the reviewer
<!-- Migrations (hand-written?), deviations from the issue, risks, follow-ups (as new issues). -->
