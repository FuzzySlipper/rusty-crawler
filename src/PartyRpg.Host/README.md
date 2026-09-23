# PartyRpg.Host

Planned home of the product lifecycle and the one ordinary product entry.

Owns, once implemented:

- The explicit built-in ruleset and bundle selection and the product defaults.
- Session construction and the launcher/diagnostics surface around it.
- Product metadata (product id, title, UI root, content root, lifecycle mode,
  declared input intents and mappings, content bundles).

Boundary rules:

- The host may select the Might and Magic ruleset and bundle; it may not
  interpret Might and Magic rules, read original game data, or contain
  Might and Magic formulas.
- Exactly one concrete product entry type is declared. The immutable SDK
  generates the CoreCLR and NativeAOT composition beneath ignored `obj` output;
  no generated bridge, binding, or export is checked in.
- No second loop, clock, timer, thread, renderer, or browser authority.

Nothing is implemented yet.
