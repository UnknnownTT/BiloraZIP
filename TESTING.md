# BiloraZIP v0.9.0 — Windows smoke / regression checklist

## Build & update
- [ ] dotnet restore and dotnet build -c Release (no compile errors)
- [ ] Installer compiles with Inno Setup 6 or 7; installed exe opens
- [ ] In-place update v0.8.0 → v0.9.0 keeps favorites and saved dark/light choice
- [ ] File associations retain user selection; no automatic forced Windows default
- [ ] About Bilora shows 0.9.0 + creator/ChatGPT credit

## UI / localization
- [ ] Dark + Light switch stays after restart; main window and all dialogs readable
- [ ] Language → Монгол changes main menus, toolbar, common dialogs/tabs
- [ ] Language → English restores original labels; selection survives restart
- [ ] Resize 1250 → 950 → 650 px: no second toolbar row; actions move into More and back
- [ ] More actions remain enabled/disabled in sync with selection and archive state
- [ ] Info tabs have themed background instead of white default tabs
- [ ] Title bar follows Windows immersive dark preference on supported Windows builds

## Core archive operations
- [ ] Open ZIP/RAR/7Z/TAR and browse internal folders via double-click
- [ ] Select one file, Shift-select range, Ctrl-select disjoint files → Extract selected
- [ ] Select virtual directory → extracted descendants appear; other entries not extracted
- [ ] Wrong password fails; correct password extracts selected protected files
- [ ] All normal ZIP operations still work; ZIP password create/test/extract round-trip
- [ ] Drop text/photo/folder from Explorer into plain ZIP; reopen and confirm entries
- [ ] Repeat with duplicate ZIP member → fails without changing original ZIP bytes
- [ ] Cancel add midway → original ZIP opens and unchanged
- [ ] Drop onto RAR/password ZIP does NOT silently alter archive

## Closing and tray
- [ ] Exit opens choice; tray minimizes; double-click icon restores
- [ ] Tray right-click Exit closes app
- [ ] Remember Exit / Remember Tray persisted in LocalAppData
- [ ] Exit while processing blocks shutdown and leaves archive operation alive

## Still pending
- [ ] Native Windows 11 first-level shell extension (not yet implemented)
- [ ] Dragging archive members outward into Explorer (not yet implemented)
- [ ] Compiler/runtime verification on a real Windows PC
