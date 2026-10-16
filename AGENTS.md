# AGENTS.md — M3U Analyzer

Read-only IPTV catalog analysis app: .NET 10, Blazor Server + MudBlazor, SQLite.
Not a player, proxy, DVR, or playlist editor. Never writes to providers.

## One task at a time
- Your task card is `.ai_docs/tasks/<ID>-*.md`. Read it and only the files it lists.
- Implement only that card. Never start the next card. Never commit.
- Write your report to `.ai_docs/tasks/reports/<ID>.md`. Do not edit other `.ai_docs` files.
- If the card and the code disagree, stop and report. Do not guess.

## Projects and allowed references
- src/M3UAnalyzer.Domain     -> nothing (BCL only)
- src/M3UAnalyzer.Ingestion  -> Domain, M3Undle.Core
- src/M3UAnalyzer.Analysis   -> Domain, M3Undle.Core
- src/M3UAnalyzer.Data       -> Domain, EF Core SQLite
- src/M3UAnalyzer.Web        -> all of the above, MudBlazor
- tests/M3UAnalyzer.TestSupport -> Domain
Enforced by tests in tests/M3UAnalyzer.Web.Tests/Architecture.

## M3Undle.Core
- Built from source at the commit in build/m3undle-core.ref, via build/M3UndleCore.targets.
  One-time setup: scripts/setup-core.sh (creates ../M3Undle-core).
- Error M3A001, or any NuGet 401/403: STOP and report. Never work around it.
- Never copy Core source here. Never edit the M3Undle or m3undle-cli repositories.
- EpgChannelMatcher / EpgChannelIndex are for channel-to-EPG mapping only,
  never for deciding two providers carry the same channel.
- ProviderChannelNormalizer.ParseEntry throws when entry.Url is null: check first.
- LiveClassifier.ClassifyContent says "live" when it has no evidence: use ContentTypeInference.
- UrlRedactor keeps URL paths (Xtream credentials live there): use SecretRedactor.

## Commands
- Build:  dotnet build M3UAnalyzer.slnx
- Test:   dotnet test --project tests/<Name>.Tests/<Name>.Tests.csproj
- Filter: dotnet test --project tests/<Name>.Tests/<Name>.Tests.csproj --filter "FullyQualifiedName~<Class>"
- All:    dotnet test --solution M3UAnalyzer.slnx
- `dotnet test <path>` without --project / --solution fails on this SDK.
- Domain-only cards: build and test only the Domain projects.

## Code rules
- net10.0, nullable on, warnings are errors. Fix warnings; never add NoWarn,
  #pragma warning disable, or a `!` that hides a real null.
- No new packages and no version changes unless the card lists them.
- Touch only the files listed in the card.
- File-scoped namespaces that match folders; sealed by default.

## Tests
- MSTest 4: [TestClass], [TestMethod], [DataRow], Assert.ThrowsExactly.
- Pre-written tests in a card are copied verbatim and never edited.
- Never delete, skip, or weaken a test to get a green run.
- Asserting on a const triggers MSTEST0032; read constants by reflection.
- No internet in unit tests. Fixtures: tests/Fixtures, hosts example.invalid,
  fake credentials only.

## Security
- Never store, log, show, or export a playable stream URL. Store StreamKey hashes.
- Before logging or saving: URLs go through SecretRedactor; messages that may
  contain URLs go through TextRedactor.
- Secrets only through ICredentialProtector.
- Outbound HTTP only through ISourceFetcher.

## Finish
- Report: files changed, exact commands run, test totals copied from output,
  open issues. Then stop.
- Escalate (stop and report) when: an API differs from the card, a file outside
  the card is needed, a security choice is not specified, or the same command
  fails twice without a new hypothesis.