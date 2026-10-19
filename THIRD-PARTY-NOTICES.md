# Third-party components — license review required

- **SharpCompress 0.50.5** — third-party archive reading library. Verify its license and its transitive dependency notices in the restored NuGet package before distributing binaries.
- **SharpZipLib 1.4.2** — third-party ZIP encryption library. Verify its license and copyright notices in the restored NuGet package before distributing binaries.
- **.NET 10 Desktop Runtime** — self-contained deployment includes Microsoft runtime components. Review the applicable Microsoft distribution notices and licenses.
- **Inno Setup** — installer compiler; check its redistribution terms for the setup EXE.

The top-level `LICENSE` (Apache-2.0) applies only to original BiloraZIP project code and assets for which contributors hold the rights. It does **not** relicense third-party components.

This is a pending audit checklist, **not** a representation that the release has passed a full legal review.
