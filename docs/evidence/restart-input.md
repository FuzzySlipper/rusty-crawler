# Input after a dev-host restart

Reconciled on 2 October 2026 from the 30 September observer retest already reviewed by the Engine-adoption
task. This is a published copy of that existing report, not a new restart exercise.

The observer stopped `rusty dev`, started it again with a resumed session, attached a fresh playtest
browser session and clicked the play area. A 700 ms physical W hold (virtual key 87) moved the party
about 50 units and took the sewer transition. The corrected launch record says `--live-debug`, without
`--headless`. This covers keys reaching the product after a dev-host restart on a freshly attached page.
It does not reproduce restarting the original `rc-live-b` systemd unit underneath an existing playtest
service, or certify reconnecting an old browser session.

The original failing runtime used an older repository-local Engine pack and input ingress. The adopted
runtime moved to the Engine's current binding and incarnation handling. Crawler added no input alias,
replacement browser path or product-owned restart machinery. The current installed input declarations
still use Engine runtime bindings and string-valued canonical decimal sequences for declared intents.
The live-check procedure records the direct-intent diagnostic fallback beside ordinary keys, including
one claim per admitted update and the need to obtain a fresh binding after replacement. Such a fallback
checks Engine admission and product intent handling, not page key capture.

The earlier retest and its correction remain in Den. Its screenshots were not newly inspected for this
reconciliation, and no current-pair restart, systemd replay, broad traversal or rendered-world claim is
made. The current source gate passed CoreCLR staging and all product suites; it is source/runtime-build
coverage, not a substitute for the historical ordinary-key observation.
