# BILZ-1 — Bilora experimental multi-file archive container

**Status: research preview, version 1. Not for primary backups.** This is a
new container format; no compatibility with ZIP, RAR or 7Z is implied.

A `.bilz` archive stores an ordered sequence of files and directories. Each
file embeds one entire **BARM-1 stream**; directories store no payload. The
compression algorithm is *Bilora BARM-1* with existing LZ/RLE/prediction ideas,
not RAR's proprietary algorithm. Two encoder presets are compatible with the
same BARM-1 decoder.

## Binary layout

Every multibyte integer is **little-endian**. All names are strict UTF-8.

**Global header (10 bytes):**

| Offset | Bytes | Meaning |
|---:|---:|---|
| 0 | 4 | ASCII `BILZ` |
| 4 | 1 | version `0x01` |
| 5 | 1 | flags `0x00` |
| 6 | 4 | uint32 number of entries |

**Then exactly that many entries:**

| Bytes | Meaning |
|---:|---|
| 1 | kind: `0` file, `1` directory |
| 2 | uint16 byte length of path, 1..4096 |
| N | relative archive path (UTF-8, `/` separators) |
| 8 | uint64 uncompressed size (0 for dir) |
| 8 | uint64 embedded BARM stream size (0 for dir) |
| 32 | SHA-256 of uncompressed file bytes; all-zero for dir |
| M | embedded BARM-1 stream for file; no bytes for directory |

The stream must end immediately after the last entry. For empty files the BARM
payload contains the 18-byte empty BARM header and SHA-256 is that of the empty
byte string. Directory entries allow explicit empty folders. There is **no
trailing central directory** and no random-access index in version 1.

## Naming and validation

- Entry names are relative with `/` separator. Names must not be empty, root,
  contain `\\`, `:`, path traversal (`.` or `..` segments), control chars, or
  disallowed Windows filename characters. Reserved DOS names are rejected.
- Duplicate names are rejected case-insensitively (Windows compatibility).
- The writer rejects symlinks and reparse points; the archive has no symlink
  entry type. It preserves directory layout, not timestamps or permissions.
- Prototype limits: 100,000 entries; 4096 UTF-8 bytes/path; 1 GiB uncompressed
  per file; 4 GiB uncompressed total; 2 GiB stored payload per file.
- The CLI refuses to overwrite an existing output archive or extraction folder.
  Extraction writes to a fresh staging directory, verifies all SHA-256 checks,
  and renames the directory into place only on success. Failures clean up.
- SHA-256 and the BARM CRC32 provide **integrity detection, not authentication**.
  Anyone can rewrite both metadata and hashes. No encryption, signing or
  tamper-proof guarantee is provided.

## Example

```
BILZ header, entry count = 3
  DIR  MyFolder
  FILE MyFolder/example.txt (SHA-256, BARM-1 compressed stream)
  FILE MyFolder/empty.dat (SHA-256 of empty bytes, 18-byte BARM-1 header)
```

## Encoder profiles

- `Maximum`: seven BARM transform candidates, original prototype behavior.
- `Fast`: transforms 0,1,4 only, reducing encoder analysis at possible cost of
  larger output. Decoder is unchanged; neither profile is an archive flag.

## Current limitations

No solid compression across files, encryption, recovery records, file dates,
permissions, large-file streaming in the Python reference tool, deduplication,
multithreading, seekable index, or security audit. C# writes file streams through
per-file temporary payloads; Python holds each file in RAM. Neither runtime is
verified for hostile archives. Do not embed unreviewed parsing in privileged
services or extract untrusted archives outside a sandbox.
