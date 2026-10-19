# BARM-1 — Bilora Adaptive Residual Match

Status: **EXPERIMENTAL, v0.1** (not an interchange standard; do not trust for backups).

BARM-1 is a from-scratch, deterministic, lossless single-byte-stream codec.
The design combines independently implemented reversible stride residuals with
four elementary token commands and bounded LZ matches. Those primitives are
well-known compression ideas; we make **no claim of worldwide first invention,
patent novelty, superiority to RAR, or production security**.

## Byte order

All multi-byte integers are **little-endian**; there are no alignment bytes.

### Global header: 18 bytes

| Offset | Size | Meaning |
|---:|---:|---|
| 0 | 4 | ASCII `BARM` |
| 4 | 1 | version = 1 |
| 5 | 4 | block size, unsigned 32-bit, 1..65536 |
| 9 | 8 | full uncompressed size, unsigned 64-bit |
| 17 | 1 | flags = 0 |

For empty input, only this header is present. For nonempty input, blocks follow
until their combined original byte counts equal the global original size.

### Block header: 14 bytes

| Size | Meaning |
|---:|---|
| 4 | original length, 1..block size |
| 1 | transform ID 0..6 |
| 1 | storage mode: 0 = raw, 1 = token stream |
| 4 | following payload byte count, <= original length |
| 4 | CRC32/ISO-HDLC of **original** bytes (polynomial 0xEDB88320) |

Raw storage requires transform 0 and payload size equal original length.
Token storage requires payload size strictly less than original length.

### Reversible transforms

Each block is processed **independently**; missing predecessor bytes are zero.

| ID | Forward at byte `i` | Inverse |
|---:|---|---|
| 0 | original byte | no-op |
| 1 | `x[i] - x[i-1] mod 256` | `x[i] = r[i] + x[i-1] mod 256` |
| 2 | `x[i] - x[i-2] mod 256` | analogous |
| 3 | `x[i] - x[i-4] mod 256` | analogous |
| 4 | `x[i] XOR x[i-1]` | `x[i] = r[i] XOR x[i-1]` |
| 5 | `x[i] XOR x[i-2]` | analogous |
| 6 | `x[i] XOR x[i-4]` | analogous |

Encoder tries each of the 7 modes and chooses whichever final token payload is
smallest, only if it is smaller than uncompressed bytes; otherwise uses raw.
Tie-breaking favors earlier modes (0 first). Decoder does not need encoder
match-search details; they affect only compression ratio and time.

### Token stream

The top 2 bits of a command byte specify the operation; low 6 bits carry length.

| Bits 7..6 | Meaning | Length | Extra bytes |
|---|---|---|---|
| `00` | literal bytes | 1 + low6 | `length` literal bytes |
| `01` | repeated byte | 4 + low6 | one repeated byte |
| `10` | backward match | 4 + low6 | uint16 little-endian distance |
| `11` | zero-byte run | 4 + low6 | none |

Backward distance must be 1..65535 and no more than current token-decoded
output length. Overlap is permitted, and bytes are copied **one at a time**.
All tokens decode to the transformed bytes, then the inverse transform yields
original bytes. Tokens must expand to exactly original block length.

### Decoder failure conditions

Reject invalid header/flags/version/mode, invalid lengths or backward distances,
truncated input, token overrun or underrun, CRC mismatch, and trailing bytes.
Decoder should enforce a caller-defined maximum extracted size (prototype
limit defaults to 1 GiB) to reduce decompression bombs.

## Scope

- No multi-file container, directory paths, filesystem metadata, solid blocks,
  encryption, signatures, parallelism or recovery records (future `.bilz` container).
- CRC32 detects accidental corruption; it does **not** authenticate data.
- 64 KiB independent blocks are used for bounded memory consumption and damage
  isolation, at the cost of potential compression ratio versus solid coders.
- Greedy lookback with a 3-byte key and last 4 candidates is an implementation
  choice, not part of the format.

## Integration rule

**Do not replace ZIP/RAR/7Z support.** Add BARM experimentally as a codec
for `.bilz` in a later version only after independent decode tests, fuzzing,
performance profiling, and version-compatibility testing.
