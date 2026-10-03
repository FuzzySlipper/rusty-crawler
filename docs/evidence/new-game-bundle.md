# The ordinary new game and its missing-content start

Reading of rusty-crawler#9228, 2026-10-02 (America/Los_Angeles), through the crew-services browser playtest
backend: each session started its own `rusty dev --live-debug` host from a checkout and the browser showed the
product's own page at 1280×720. No scenario, save, bundle or environment variable was staged for either run;
the operator's packs were regenerated with the current importer (`mm7import write`, 76 places emitted, none
refused) before the first. Screenshots stay in the operator's `local/evidence-9228/`.

## With the operator's packs

The default launch read `mm7-new-game · 3 packs` under the ruleset title and opened creation with the
default four members. Pressing the panel's ordinary **Accept party** button left creation and the head read
`Emerald Island · region`; the panel showed `Party accepted` with the four members and the rest, camp and fight
controls of a running session. The arrival is the authored `mm7-new-game` scenario start (place `1`, entry
point `Party Start`, `party: creation`), selected by the bundle and nothing else.

## Without them

A second checkout of the same commit, whose ignored `content/partyrpg/imports` held only its README, started
without refusing. The head read `No game bundle selected` and `No world: game content is not prepared`, and a
boxed section titled **Game content is not prepared** stated `mm7-new-game needs mm7-tables, mm7-world, which
are not in the content root.` followed by the bundle's own `setup` text (build the importer, run `write`
against one's own installation, restart). No creation screen was offered, so no party could be accepted into
the absent world. The guidance's long tool path overflowed the box at that width; the style now wraps it.

## Limits

This reading does not cover title-screen New/Load choice, the operator preparation command's own reliability,
or the rendered world, which have their own receivers. The focused suites cover a bundle whose only defect is
absent packs (`BundleSelectionTests`, `ProductCompositionTests`) and a bundle with another defect, which still
stops the product with every problem named.
