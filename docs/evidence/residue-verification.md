# Residue verification

Point-in-time reading on 2026-10-02, covering travel settlement, spent container
traps, interaction persistence, promotion eligibility and rest recovery.

The final `scripts/verify.sh` run passed every build, importer inventory and map
check, deterministic pack write, UI check and CoreCLR staging step. Suite totals
were 725 Kit, 85 Host, 285 ruleset, 184 importer and 67 UI tests. Its aggregate
exit was 1: the architecture suite treated a donor citation in the ruleset README
as a repository path. Qualifying that citation with `OpenEnroth/` corrected the
document, and a subsequent complete architecture run passed all 21 tests. This
does not change the historic aggregate exit. NativeAOT was not run.

The three persistent review lanes examined these changes and their callers:
Engine reuse, existing product reuse and runtime trust. No unresolved
source-backed findings remained. The product continues to use its existing
travel executive, interaction ledger, progression owner and rest owner; the
changes introduce no second clock, persistence store, entity graph or host.

Focused semantic checks additionally cover refused travel admission and retry,
trap/search/repeated use, target-state round trips and malformed saves, remembered
deaths and purses, all four laid-out promotion conditions through the shared
rank routes, and rest/camp with dead, petrified and eradicated members. These are
code and contract checks. They do not certify traversal or visible interaction;
live readings are published separately.

The ruleset's additional noticed-trap and successful-disarm cases exposed a
missing `Disarm` wire name in the existing interaction projection. The mapping
now publishes `disarm`, which the companion already renders as a string. The
complete focused rest/schedule suite, including unseen traps, failed disarming
and successful disarming followed by search and repeated refusal, passed 17/17.

The subsequent whole `scripts/verify.sh` run passed every step with exit 0,
including all 21 architecture, 725 Kit, 85 Host, 287 ruleset, 184 importer and
67 UI tests, the imported-data checks and CoreCLR staging. NativeAOT remained
outside this run.
