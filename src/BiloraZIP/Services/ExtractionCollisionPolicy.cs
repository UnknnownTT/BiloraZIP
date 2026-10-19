namespace BiloraZIP.Services;

/// <summary>Explicit, per-operation destination conflict policy. Default never overwrites.</summary>
public enum ExtractionCollisionPolicy { Fail, Skip, Rename, Overwrite }
