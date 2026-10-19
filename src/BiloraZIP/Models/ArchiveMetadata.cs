namespace BiloraZIP.Models;

public sealed record ArchiveMetadata(
    string FileName, string FullPath, string Format, long ArchiveBytes,
    long OriginalBytes, long PackedContentsBytes, int Files, int Folders,
    string CompressionRatio, string Encryption, string LastModified,
    string HeaderEncryption, string Comment, string RecoveryRecord,
    string DictionarySize, string Integrity);
