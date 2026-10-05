import { HUD_STYLES } from './hud-styles.js';

/**
 * The companion's stylesheet, mounted once beside the panel. It styles what the sections draw and decides
 * nothing: a `hidden` attribute is honoured by every rule, so a control or a line the product says is absent
 * stays absent.
 */
export const STYLES = `
.crawler-session {
  /* The frame covers the viewport and lets the world through: only its own parts take the pointer. */
  position: fixed;
  inset: 0;
  pointer-events: none;
  color: #e8e0cc;
  font: 13px/1.45 system-ui, sans-serif;
}
.crawler-session > * { pointer-events: auto; }
.crawler-session [hidden] { display: none !important; }
.crawler-session h2 { margin: 0; font-size: 1rem; letter-spacing: 0.03em; color: #f0dca0; }
${HUD_STYLES}
.crawler-ending { position: absolute; inset: 0; z-index: 90; display: grid; place-items: center; background: #090d18dc; }
.crawler-ending-card { max-width: 38rem; margin: 2rem; padding: 3rem; border: 2px solid #bba064; background: #1a2030; text-align: center; box-shadow: 0 0 5rem #000; }
.crawler-ending-banner { color: #dbc285; letter-spacing: .18em; text-transform: uppercase; }
.crawler-ending h1 { font: 2.3rem Georgia, serif; color: #f5dfa4; }
.crawler-ending-card > p:not(.crawler-ending-banner) { font: 1.1rem/1.7 Georgia, serif; margin: 2rem 0; }

/* The lifecycle menu: the adventure keeps a small launcher, while the title and unsaved confirmation cover the
   product surface so the player cannot act on an old adventure behind the decision. */
.crawler-menu { position: absolute; inset: 0; z-index: 20; pointer-events: none; }
.crawler-menu-launch {
  position: absolute; top: 0.6rem; left: 0.6rem; width: auto !important; padding: 0.3rem 0.65rem !important;
  background: rgba(30, 25, 18, 0.92) !important; border-color: #8a7446 !important; pointer-events: auto;
}
.crawler-menu-overlay {
  position: absolute; inset: 0; display: grid; place-items: center; padding: 1rem;
  background: radial-gradient(circle at 50% 35%, rgba(65, 53, 34, 0.86), rgba(8, 7, 6, 0.96));
  pointer-events: auto;
}
.crawler-menu-card {
  width: min(34rem, 100%); padding: 1.4rem 1.6rem; text-align: center;
  background: linear-gradient(#2b241a, #17130f); border: 2px solid #8a7446; border-radius: 0.35rem;
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.65);
}
.crawler-menu-title { margin: 0 0 0.4rem; color: #f0dca0; font-size: 1.65rem; letter-spacing: 0.05em; }
.crawler-menu-subtitle { margin: 0 0 0.8rem; color: #b9ad8c; font-size: 0.82rem; }
.crawler-menu-state { margin: 0 0 0.35rem; color: #d6a76a; text-transform: capitalize; }
.crawler-menu-message, .crawler-menu-confirm-message { margin: 0.45rem 0 0.8rem; color: #e8e0cc; }
.crawler-menu-actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 0.45rem; margin-top: 0.8rem; }
.crawler-menu-actions button { width: auto !important; min-width: 10rem; padding: 0.45rem 0.8rem !important; }
.crawler-menu-confirm { margin-top: 0.5rem; }
.crawler-menu-save-load, .crawler-menu-overwrite, .crawler-menu-load-confirm { margin-top: 0.7rem; }
.crawler-menu-save-slot { margin: 0; color: #f0dca0; font-weight: 600; }
.crawler-menu-save-summary {
  display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 0.2rem 0.8rem;
  margin: 0.7rem auto 0; max-width: 25rem; text-align: left; font-size: 0.82rem;
}
.crawler-menu-save-summary > span { display: contents; }
.crawler-menu-save-label { color: #b9ad8c; }
.crawler-menu-save-value { color: #f0e4c4; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.crawler-menu-save-details, .crawler-menu-save-message { margin: 0.55rem 0 0; color: #b9ad8c; font-size: 0.8rem; }
.crawler-menu-save-message { color: #e8c98a; }
.crawler-menu[data-state='failed'] .crawler-menu-save-message { color: #f0b49a; }
.crawler-menu[data-state='failed'] .crawler-menu-message, .crawler-menu[data-state='failed'] .crawler-menu-state { color: #f0b49a; }

/* The fight, beside the world while the party is in one. */
.crawler-fight { position: absolute; top: 0.6rem; left: 0.6rem; width: 19rem; }
.crawler-fight-panel { padding: 0.45rem 0.6rem; background: rgba(18, 16, 14, 0.86); border: 1px solid #5a4b31; border-radius: 0.25rem; color: #cfc3a2; font-size: 0.75rem; }
.crawler-fight-panel[hidden] { display: none; }
.crawler-fight-panel p { margin: 0 0 0.25rem; }
.crawler-fight-pace { color: #d8cba6; font-size: 0.8rem; }
.crawler-fight-actor[data-state='recovering'] { color: #e2c48a; }
.crawler-fight-actor[data-state='down'], .crawler-fight-selection:not([data-code='']) { color: #f0b49a; }
.crawler-fight-target { color: #f0e4c4; }
.crawler-fight-controls { display: flex; flex-wrap: wrap; gap: 0.2rem; margin: 0.15rem 0 0.3rem; }
.crawler-session .crawler-fight-controls button { width: auto; padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-session .crawler-fight-controls button[hidden] { display: none; }
.crawler-fight-foes { margin: 0 0 0.2rem; padding: 0; list-style: none; }
.crawler-fight-foe { display: flex; justify-content: space-between; gap: 0.4rem; padding: 0.05rem 0.2rem; }
.crawler-fight-foe[data-aim='yes'] { background: rgba(226, 196, 138, 0.16); color: #f0e4c4; }
.crawler-fight-foe[data-down='yes'] { opacity: 0.55; text-decoration: line-through; }
.crawler-fight-foe-facts { color: #b9ad8c; white-space: nowrap; }
.crawler-fight-more { color: #9c917a; }
.crawler-fight-last { padding: 0.2rem 0.35rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; }
.crawler-fight-last[hidden], .crawler-fight-more[hidden], .crawler-fight-selection[hidden] { display: none; }
.crawler-fight-last[data-by-party='no'] { border-color: rgba(226, 170, 96, 0.85); color: #ecd6ac; }
.crawler-fight-last[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }

/* A screen over the world: a book the player opened, or what the product put in front of the party. */
.crawler-screens { position: absolute; inset: 0.6rem 13.4rem 9rem 0.6rem; pointer-events: none; }
.crawler-session[data-screen='creation'] .crawler-screens { inset: 0.6rem; }
.crawler-screen {
  position: absolute; inset: 0;
  display: flex; flex-direction: column;
  pointer-events: auto;
  background: linear-gradient(#2b241a, #1b1711);
  border: 2px solid #8a7446;
  border-radius: 0.3rem;
  box-shadow: 0 4px 18px rgba(0, 0, 0, 0.6);
}
.crawler-screen-head { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.4rem 0.7rem; border-bottom: 1px solid #5a4b31; }
.crawler-session .crawler-screen-close { width: auto; }
.crawler-screen-body { flex: 1; overflow-y: auto; padding: 0.5rem 0.8rem; columns: 2 22rem; column-gap: 1.2rem; }
.crawler-screen-body > section { break-inside: avoid-column; margin-bottom: 0.6rem; }
.crawler-screen[data-screen='map'] .crawler-screen-body, .crawler-screen[data-screen='creation'] .crawler-screen-body,
.crawler-screen[data-screen='character'] .crawler-screen-body, .crawler-screen[data-screen='spellbook'] .crawler-screen-body, .crawler-screen[data-screen='journal'] .crawler-screen-body,
.crawler-screen[data-screen='conversation'] .crawler-screen-body, .crawler-screen[data-screen='service'] .crawler-screen-body { columns: auto; }
.crawler-screen[data-screen='map'] .crawler-map-drawing { display: block; height: calc(100vh - 21rem); width: auto; margin: 0 auto; aspect-ratio: 1; background: #0c0a08; }

/* The diagnostic panel: every fact and control the product publishes, kept out of the player's way. */
.crawler-diagnostics {
  position: absolute; top: 0.6rem; left: 0.6rem; bottom: 9rem;
  width: 24rem; overflow-y: auto;
  padding: 0.6rem 0.75rem;
  background: rgba(18, 16, 14, 0.92);
  border: 1px solid rgba(210, 196, 158, 0.35);
  border-radius: 0.35rem;
}
.crawler-session h1 { margin: 0 0 0.15rem; font-size: 1rem; letter-spacing: 0.02em; }
.crawler-session .crawler-ruleset { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.78rem; }
.crawler-session .crawler-bundle { margin: 0 0 0.6rem; color: #8d8a7a; font-size: 0.72rem; letter-spacing: 0.02em; }
.crawler-session .crawler-place { margin: 0 0 0.5rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-session dl { display: grid; grid-template-columns: auto 1fr; gap: 0.1rem 0.6rem; margin: 0 0 0.5rem; }
.crawler-session dt { color: #b9ad8c; }
.crawler-session dd { margin: 0; text-align: right; font-variant-numeric: tabular-nums; }
.crawler-session[data-mode='paused'] { border-color: rgba(226, 176, 96, 0.6); }
.crawler-session[data-mode='creating'] { border-color: rgba(150, 200, 226, 0.6); }
.crawler-session button {
  width: 100%;
  padding: 0.3rem 0.5rem;
  border: 1px solid rgba(210, 196, 158, 0.5);
  border-radius: 0.25rem;
  background: rgba(60, 54, 44, 0.9);
  color: inherit;
  font: inherit;
  cursor: pointer;
}
.crawler-session button:disabled { opacity: 0.55; cursor: default; }
.crawler-session .crawler-hint { margin: 0.4rem 0 0; color: #b9ad8c; font-size: 0.72rem; }
.crawler-creation { margin: 0 0 0.5rem; }
.crawler-creation .crawler-step-head { margin: 0 0 0.45rem; color: #e0d3ae; font-size: 0.95rem; }
.crawler-creation .crawler-row { margin: 0 0 0.5rem; }
.crawler-creation .crawler-row-label { display: block; margin-bottom: 0.2rem; color: #b9ad8c; font-size: 0.78rem; }
.crawler-creation .crawler-options { display: flex; flex-wrap: wrap; gap: 0.25rem; }
.crawler-creation .crawler-options button { width: auto; padding: 0.2rem 0.5rem; font-size: 0.8rem; }
.crawler-creation .crawler-options button[data-selected='true'] { border-color: rgba(226, 176, 96, 0.9); background: rgba(96, 78, 50, 0.9); }
.crawler-creation .crawler-options button[data-available='false'] { opacity: 0.7; }
/* The party across the top: one card per member, the one being made outlined. */
.crawler-creation-members { display: grid; grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr)); gap: 0.5rem; margin-bottom: 0.5rem; }
.crawler-session .crawler-creation-member {
  display: grid; grid-template-columns: 3.6rem minmax(0, 1fr); column-gap: 0.5rem; align-items: center;
  width: auto; padding: 0.3rem; text-align: left;
  border: 2px solid #4b4030; border-radius: 0.2rem; background: rgba(10, 9, 7, 0.8);
}
.crawler-session .crawler-creation-member[data-selected='true'] { border-color: #e2b060; box-shadow: 0 0 6px rgba(226, 176, 96, 0.6); }
.crawler-creation-member .crawler-creation-face { grid-row: 1 / span 4; width: 3.6rem; height: 4.2rem; }
.crawler-creation-member-name { font-weight: 600; color: #f0e4c4; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.crawler-creation-member-kind, .crawler-creation-member-pool { font-size: 0.75rem; color: #b9ad8c; }
.crawler-creation-member-step { font-size: 0.72rem; color: #d6a76a; text-transform: capitalize; }
.crawler-creation-member[data-step='complete'] .crawler-creation-member-step { color: #8fc27a; }
.crawler-creation-face { display: grid; place-items: center; object-fit: cover; image-rendering: pixelated; border: 1px solid #000; background: #222; color: #8d8a7a; font-size: 1.5rem; }
/* The choices on the left, the member's name and attributes on the right. */
.crawler-creation-layout { display: grid; grid-template-columns: minmax(0, 3fr) minmax(16rem, 2fr); gap: 1rem; }
@media (max-width: 760px) { .crawler-creation-layout { grid-template-columns: minmax(0, 1fr); } }
.crawler-creation-portraits .crawler-options button { display: flex; flex-direction: column; align-items: center; gap: 0.15rem; padding: 0.25rem; }
.crawler-creation-portraits .crawler-creation-face { width: 4rem; height: 4.7rem; }
.crawler-creation-face-name { font-size: 0.7rem; }
.crawler-creation .crawler-attribute { display: grid; grid-template-columns: minmax(0, 1fr) 1.8rem 1.8rem; align-items: center; gap: 0.3rem; padding: 0.1rem 0; font-size: 0.85rem; border-bottom: 1px solid rgba(210, 196, 158, 0.12); }
.crawler-creation .crawler-attribute button { width: 1.8rem; padding: 0; text-align: center; }
.crawler-creation .crawler-name { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 0.3rem; margin: 0 0 0.6rem; }
.crawler-creation .crawler-name .crawler-row-label { grid-column: 1 / -1; }
.crawler-creation .crawler-name input {
  min-width: 0; padding: 0.3rem 0.45rem; font: inherit; font-size: 0.95rem;
  color: #f0e4c4; background: rgba(0, 0, 0, 0.55); border: 1px solid #6a5a3a; border-radius: 0.2rem;
}
.crawler-creation .crawler-name button { width: auto; }
.crawler-creation .crawler-actions { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-top: 0.6rem; }
.crawler-creation .crawler-actions button { width: auto; padding: 0.3rem 0.8rem; font-size: 0.85rem; }
.crawler-refusal { margin: 0.4rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(226, 120, 96, 0.8); color: #e8c8b0; font-size: 0.75rem; }
.crawler-refusal[hidden] { display: none; }
.crawler-accepted { margin: 0.35rem 0 0; padding: 0; list-style: none; color: #d8cba6; font-size: 0.75rem; }
.crawler-session .crawler-save { margin: 0.3rem 0 0; }
.crawler-conversation { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-conversation[hidden] { display: none; }
.crawler-conversation .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-conversation-greeting { margin: 0 0 0.35rem; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(210, 196, 158, 0.5); color: #e6dcc0; font-size: 0.78rem; }
.crawler-conversation .crawler-row { display: flex; flex-wrap: wrap; gap: 0.2rem; margin: 0 0 0.3rem; }
.crawler-conversation .crawler-row button { width: auto; padding: 0.1rem 0.35rem; font-size: 0.72rem; }
.crawler-conversation .crawler-options { display: flex; flex-wrap: wrap; gap: 0.2rem; }
.crawler-conversation .crawler-options button { width: auto; padding: 0.15rem 0.4rem; font-size: 0.74rem; }
.crawler-withheld { margin: 0.3rem 0 0; padding: 0; list-style: none; color: #a89a78; font-size: 0.7rem; }
.crawler-said { margin: 0.35rem 0 0; padding: 0; list-style: none; color: #dcc9a0; font-size: 0.74rem; }
.crawler-conversation .crawler-actions { display: flex; gap: 0.2rem; margin-top: 0.3rem; }
.crawler-conversation .crawler-actions button { padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-conversation-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-conversation-result[hidden] { display: none; }
.crawler-conversation-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-conversation-residue { margin: 0.15rem 0 0; color: #b8a888; font-size: 0.7rem; }
.crawler-conversation-residue[hidden] { display: none; }
.crawler-service { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-service[hidden] { display: none; }
.crawler-service .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-service-state { margin: 0 0 0.2rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-service-access { margin: 0 0 0.2rem; color: #cfe0e8; font-size: 0.72rem; }
.crawler-service-member { display: flex; align-items: center; gap: 0.3rem; margin: 0 0 0.25rem; font-size: 0.72rem; }
.crawler-service-member[hidden] { display: none; }
.crawler-service .crawler-row { margin: 0 0 0.25rem; }
.crawler-service .crawler-row-label { display: block; color: #b9ad8c; font-size: 0.72rem; }
.crawler-service .crawler-options { display: flex; flex-wrap: wrap; gap: 0.2rem; }
.crawler-service .crawler-options button { width: auto; padding: 0.1rem 0.35rem; font-size: 0.72rem; }
.crawler-service .crawler-actions { display: flex; gap: 0.2rem; margin-top: 0.3rem; }
.crawler-service .crawler-actions button { padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-service-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-service-result[hidden] { display: none; }
.crawler-service-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-session .crawler-use { margin: 0.3rem 0.4rem 0 0; }
.crawler-use-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-use-result[hidden] { display: none; }
.crawler-use-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-use-residue { margin: 0.15rem 0 0; color: #b8a888; font-size: 0.7rem; }
.crawler-use-residue[hidden] { display: none; }
.crawler-save-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-save-result[hidden] { display: none; }
.crawler-save-result[data-state='failed'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-combat { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-combat[hidden] { display: none; }
.crawler-combat .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-combat-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-combat .crawler-actions button { width: auto; padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-combat-members, .crawler-combat-enemies { margin: 0.2rem 0 0; padding: 0; list-style: none; color: #cfc3a2; font-size: 0.72rem; }
.crawler-fighter[data-ready='no'] { color: #a8967a; }
.crawler-combat-turn { margin: 0.2rem 0 0.25rem; color: #cfc3a2; font-size: 0.75rem; }
.crawler-turn-order { margin: 0.2rem 0 0; padding: 0; list-style: none; color: #b9ad8c; font-size: 0.72rem; }
.crawler-turn-order .crawler-turn-actor[data-current='yes'] { color: #efe6c8; }
.crawler-turn-order .crawler-turn-actor[data-waiting='yes'] { font-style: italic; }
.crawler-combat-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-combat-result[hidden] { display: none; }
.crawler-combat-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-combat-result[data-by-party='no'] { border-color: rgba(226, 170, 96, 0.85); color: #ecd6ac; }
.crawler-fighter[data-activity='closing'] { color: #e2cba0; }
.crawler-fighter[data-activity='backing away'] { color: #b9c8a4; }
.crawler-fighter[data-activity='down'] { color: #8f8878; text-decoration: line-through; }
.crawler-progression { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-progression[hidden] { display: none; }
.crawler-progression .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-progression-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-progression-member { display: flex; align-items: center; gap: 0.4rem; margin: 0 0 0.2rem; font-size: 0.72rem; color: #cfc3a2; }
.crawler-progression-member .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-progression .crawler-train { width: auto; padding: 0.15rem 0.35rem; font-size: 0.7rem; }
.crawler-progression-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-progression-result[hidden] { display: none; }
.crawler-progression-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-promotion { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-promotion[hidden] { display: none; }
.crawler-promotion .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-promotion-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-promotion-member { margin: 0 0 0.3rem; }
.crawler-promotion-member .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-promotion-rank { margin: 0 0 0.15rem; font-size: 0.72rem; color: #cfc3a2; }
.crawler-promotion-granted { margin: 0.2rem 0 0; }
.crawler-promotion-grant { font-size: 0.72rem; color: #cfe0e8; }
.crawler-promotion-denial { font-size: 0.72rem; color: #e8c8b0; }
.crawler-promotion-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-promotion-result[hidden] { display: none; }
.crawler-promotion-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-skills { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-skills[hidden] { display: none; }
.crawler-skills .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-skills-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-skills-member { margin: 0 0 0.3rem; }
.crawler-skills-member .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-skill { display: flex; align-items: center; gap: 0.4rem; margin: 0 0 0.15rem; font-size: 0.72rem; color: #cfc3a2; }
.crawler-skill-refusal { color: #e8c8b0; }
.crawler-skills .crawler-raise { width: auto; padding: 0.15rem 0.35rem; font-size: 0.7rem; }
.crawler-skills-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-skills-result[hidden] { display: none; }
.crawler-skills-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-magic { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-magic[hidden] { display: none; }
.crawler-magic .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-magic-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-magic-member { margin: 0 0 0.3rem; }
.crawler-magic-member .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-spell { display: flex; flex-wrap: wrap; gap: 0.25rem; align-items: center; font-size: 0.7rem; color: #c6bb9c; }
.crawler-magic .crawler-cast, .crawler-magic .crawler-quick { width: auto; padding: 0.15rem 0.35rem; font-size: 0.7rem; }
.crawler-alchemy { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-alchemy[hidden] { display: none; }
.crawler-alchemy .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-alchemy-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-alchemy-item, .crawler-alchemy-mixture { margin: 0 0 0.3rem; }
.crawler-alchemy-item .crawler-row-label, .crawler-alchemy-mixture .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-alchemy-result { margin: 0.2rem 0 0; color: #d8cba6; font-size: 0.74rem; }
.crawler-alchemy-result[hidden] { display: none; }
.crawler-alchemy .crawler-mix { width: auto; padding: 0.15rem 0.35rem; font-size: 0.7rem; }
.crawler-quests { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-quests[hidden] { display: none; }
.crawler-quests .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-quests-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-quest { margin: 0 0 0.35rem; }
.crawler-quest .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-quest-note { color: #b9ad8c; font-size: 0.72rem; }
.crawler-quest-objective { color: #cfc3a2; font-size: 0.72rem; }
.crawler-quest-objective[data-met='true'] { color: #cfe0c8; }
.crawler-quest-residue { color: #d8c2a0; font-size: 0.7rem; font-style: italic; }
.crawler-quests-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-quests-result[hidden] { display: none; }
.crawler-quests-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-setup { margin: 0 0 0.6rem; border: 1px solid #b08a4a; padding: 0.5rem 0.6rem; background: rgba(176, 138, 74, 0.12); }
.crawler-setup[hidden] { display: none; }
.crawler-setup .crawler-setup-guidance { margin: 0.3rem 0 0; color: #d8cba6; font-size: 0.78rem; white-space: pre-wrap; overflow-wrap: anywhere; }
.crawler-awards { margin: 0.4rem 0 0; border-top: 1px solid rgba(210, 196, 158, 0.35); padding-top: 0.5rem; }
.crawler-awards[hidden] { display: none; }
.crawler-awards > .crawler-step-head { margin: 0 0 0.3rem; color: #e0d3ae; font-size: 0.85rem; }
.crawler-awards-state { margin: 0 0 0.3rem; }
.crawler-awards-list { display: flex; flex-direction: column; gap: 0.15rem; }
.crawler-award { font-size: 0.85rem; }
.crawler-map { margin: 0.4rem 0 0; border-top: 1px solid rgba(210, 196, 158, 0.35); padding-top: 0.5rem; }
.crawler-map[hidden] { display: none; }
.crawler-map > .crawler-step-head { margin: 0 0 0.3rem; color: #e0d3ae; font-size: 0.85rem; }
.crawler-map-state, .crawler-map-orientation { margin: 0 0 0.3rem; color: #cbbf9e; font-size: 0.8rem; }
.crawler-map-detection { margin: 0 0 0.3rem; color: #9fd3e0; font-size: 0.8rem; }
.crawler-map-detection[hidden] { display: none; }
.crawler-map-drawing { display: block; width: 12rem; height: 12rem; background: #12100c; border: 1px solid rgba(210, 196, 158, 0.35); }
.crawler-map-drawing[hidden] { display: none; }
.crawler-map-cell { stroke: none; }
.crawler-map-cell-low { fill: #3f4a2c; }
.crawler-map-cell-upland { fill: #55603a; }
.crawler-map-cell-highland { fill: #6d6b46; }
.crawler-map-cell-peak { fill: #8a8460; }
.crawler-map-cell-floor { fill: #4a4436; }
.crawler-map-cell-wall { fill: #b9a878; }
.crawler-map-mark { fill: #d8c98f; stroke: #12100c; stroke-width: 1; }
.crawler-map-mark-container { fill: #d8a45f; }
.crawler-map-mark-building { fill: #b58cd8; }
.crawler-map-mark-person { fill: #7fc7d8; }
.crawler-map-mark-creature { fill: #d87f7f; }
.crawler-map-mark-mind { fill: #d8d07f; }
.crawler-map-mark[data-detected='true'] { stroke: #f4ead0; stroke-width: 2; }
.crawler-map-party { fill: #f4ead0; stroke: #12100c; stroke-width: 1; }

.crawler-journal { margin: 0.4rem 0 0; border-top: 1px solid rgba(210, 196, 158, 0.35); padding-top: 0.5rem; }
.crawler-journal[hidden] { display: none; }
.crawler-journal > .crawler-step-head { margin: 0 0 0.3rem; color: #e0d3ae; font-size: 0.85rem; }
.crawler-book { margin: 0 0 0.45rem; padding-left: 0.5rem; border-left: 2px solid rgba(150, 140, 110, 0.5); }
.crawler-book[hidden] { display: none; }
.crawler-book[data-available='false'] { border-left-color: rgba(150, 140, 110, 0.25); }
.crawler-book .crawler-step-head { margin: 0 0 0.15rem; color: #d8cba6; font-size: 0.78rem; }
.crawler-book-state { margin: 0 0 0.2rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-book-row { margin: 0 0 0.25rem; }
.crawler-book-row .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-book-detail { color: #b9ad8c; font-size: 0.7rem; }
.crawler-book-row-state { color: #a8bcc9; font-size: 0.7rem; }
.crawler-magic .crawler-target { font-size: 0.7rem; }
.crawler-magic-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-magic-result[hidden] { display: none; }
.crawler-magic-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-magic-facts { margin: 0.2rem 0 0; padding-left: 1.1rem; color: #cfe0e8; font-size: 0.72rem; }
.crawler-magic-facts[hidden] { display: none; }
.crawler-magic-running { margin: 0.2rem 0 0; color: #b9c9a8; font-size: 0.72rem; }
.crawler-magic-running[hidden] { display: none; }
.crawler-magic-member-running { margin: 0.15rem 0 0; padding-left: 1.1rem; color: #a8bcc9; font-size: 0.72rem; }
.crawler-magic-member-running[hidden] { display: none; }
.crawler-magic-items { margin: 0.3rem 0 0; }
.crawler-magic-items[hidden] { display: none; }
.crawler-magic-item { margin: 0 0 0.3rem; }
.crawler-magic-item .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-magic-items .crawler-item-member, .crawler-magic-items .crawler-target { font-size: 0.7rem; }
.crawler-magic-items .crawler-use-item { width: auto; padding: 0.15rem 0.35rem; font-size: 0.7rem; }
.crawler-rest { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-rest[hidden] { display: none; }
.crawler-rest .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-rest-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-rest .crawler-actions { display: grid; grid-template-columns: repeat(auto-fill, minmax(13rem, 1fr)); gap: 0.4rem; }
.crawler-rest-option { padding: 0.35rem 0.45rem; border: 1px solid #5a4b31; border-radius: 0.2rem; background: rgba(30, 26, 20, 0.6); }
.crawler-rest-option[data-offered='no'] { border-color: rgba(226, 120, 96, 0.55); }
.crawler-rest-option button { width: 100%; }
.crawler-rest-judgment { margin: 0.25rem 0 0; color: #cfc3a2; font-size: 0.74rem; }
.crawler-rest-option[data-offered='no'] .crawler-rest-judgment { color: #f0b49a; }
.crawler-rest-unrestored { margin: 0.2rem 0 0; padding: 0; list-style: none; color: #e2c48a; font-size: 0.72rem; }
.crawler-rest-unrestored[hidden] { display: none; }
.crawler-rest .crawler-actions button { width: auto; padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-rest-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-rest-result[hidden] { display: none; }
.crawler-rest-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-problems { margin: 0.4rem 0 0; padding: 0.25rem 0.4rem 0.25rem 1.2rem; border-left: 2px solid rgba(226, 120, 96, 0.9); color: #e8c8b0; font-size: 0.7rem; }
.crawler-problems[hidden] { display: none; }
/* The character book: one member, four pages. */
.crawler-character-faces { display: flex; gap: 0.4rem; margin-bottom: 0.4rem; }
.crawler-session .crawler-character-face { width: 3rem; height: 3.5rem; padding: 0; border: 2px solid #4b4030; background: #222; color: #8d8a7a; overflow: hidden; }
.crawler-session .crawler-character-face[data-selected='yes'] { border-color: #e2b060; box-shadow: 0 0 6px rgba(226, 176, 96, 0.6); }
.crawler-character-face img { width: 100%; height: 100%; object-fit: cover; image-rendering: pixelated; }
.crawler-character-title { margin: 0 0 0.4rem; color: #f0e4c4; font-size: 1rem; font-weight: 600; }
.crawler-character-tabs { display: flex; gap: 0.3rem; margin-bottom: 0.5rem; border-bottom: 1px solid #5a4b31; }
.crawler-session .crawler-character-tab { width: auto; padding: 0.3rem 0.9rem; border-radius: 0.2rem 0.2rem 0 0; }
.crawler-session .crawler-character-tab[data-open='yes'] { border-color: #e2b060; background: rgba(96, 78, 50, 0.9); }
.crawler-sheet { display: grid; grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr)); gap: 0.6rem; margin-bottom: 0.6rem; }
.crawler-sheet-box { padding: 0.4rem 0.6rem; border: 1px solid #5a4b31; border-radius: 0.2rem; background: rgba(0, 0, 0, 0.25); margin-bottom: 0.5rem; }
.crawler-sheet-title { margin: 0 0 0.3rem; color: #e0d3ae; font-weight: 600; }
.crawler-sheet-rows { display: grid; grid-template-columns: auto auto; gap: 0.15rem 0.8rem; margin: 0; font-size: 0.85rem; }
.crawler-sheet-rows dt { color: #b9ad8c; }
.crawler-sheet-rows dd { margin: 0; text-align: right; color: #f0e4c4; font-variant-numeric: tabular-nums; }
.crawler-sheet-rows dd[data-detail] { color: #8fc27a; }
.crawler-growth-hint, .crawler-inspect-hint { color: #b9ad8c; font-size: 0.8rem; }
.crawler-session .crawler-character-train { width: auto; }
.crawler-rank { margin: 0.15rem 0; font-size: 0.82rem; }
.crawler-skill-list { display: grid; gap: 0.15rem; }
.crawler-character-skill { display: grid; grid-template-columns: 9rem 8rem 9rem minmax(0, 1fr); align-items: center; font-size: 0.85rem; }
.crawler-skill-name { color: #f0e4c4; }
.crawler-skill-ceiling { color: #b9ad8c; }
.crawler-character-skill-refusal { color: #e8c8b0; font-size: 0.8rem; }
.crawler-session .crawler-character-raise { width: auto; justify-self: start; padding: 0.1rem 0.5rem; }
/* The inventory page: the member's figure, the shared pack, and the inspector. */
.crawler-inventory { display: grid; grid-template-columns: minmax(15rem, 1fr) minmax(0, 1.4fr) minmax(14rem, 1fr); gap: 0.8rem; align-items: start; }
.crawler-inventory > .crawler-inventory-outcome, .crawler-inventory > .crawler-inventory-use-outcome, .crawler-inventory > .crawler-inventory-cast-result { grid-column: 1 / -1; }
.crawler-inventory-slots { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.3rem; }
.crawler-inventory-slot { display: grid; grid-template-columns: 5rem minmax(0, 1fr); align-items: center; gap: 0.3rem; min-height: 2.6rem; padding: 0.15rem 0.3rem; border: 1px solid #3a3226; background: rgba(0, 0, 0, 0.3); }
.crawler-slot-label { color: #b9ad8c; font-size: 0.72rem; text-transform: capitalize; }
.crawler-slot-empty { color: #5a5240; }
.crawler-inventory-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(4rem, 1fr)); gap: 0.3rem; max-height: calc(100vh - 22rem); overflow-y: auto; }
.crawler-session .crawler-item-tile { display: grid; place-items: center; width: auto; height: 5.5rem; padding: 0.2rem; border: 1px solid #4b4030; background: rgba(0, 0, 0, 0.45); overflow: hidden; }
.crawler-inventory-slot .crawler-item-tile { height: 3rem; }
.crawler-session .crawler-item-tile[data-picked='true'] { border-color: #e2b060; box-shadow: 0 0 5px rgba(226, 176, 96, 0.7); }
.crawler-session .crawler-item-tile[data-retained='yes'] { border-color: #8a6ab0; }
.crawler-item-picture { width: 100%; height: 100%; min-height: 0; object-fit: contain; image-rendering: pixelated; }
.crawler-item-words { font-size: 0.62rem; line-height: 1.1; color: #d8cba6; text-align: center; }
.crawler-inventory-inspect { padding: 0.5rem; border: 1px solid #5a4b31; background: rgba(0, 0, 0, 0.3); }
.crawler-inspect-head { display: grid; grid-template-columns: 4.5rem minmax(0, 1fr); gap: 0.5rem; align-items: center; }
.crawler-session .crawler-inspect-picture { width: 4.5rem; height: 4.5rem; padding: 0.2rem; border: 1px solid #4b4030; background: rgba(0, 0, 0, 0.45); }
.crawler-inspect-name { margin: 0; color: #f0e4c4; font-weight: 600; }
.crawler-inspect-kind { margin: 0; color: #b9ad8c; font-size: 0.8rem; }
.crawler-inspect-text { white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.5; }
.crawler-inspect-facts { margin: 0.4rem 0; padding-left: 1.1rem; font-size: 0.85rem; }
.crawler-inspect-retained { color: #c9b0e8; font-size: 0.8rem; }
.crawler-inspect-actions { display: flex; flex-wrap: wrap; gap: 0.3rem; }
.crawler-inspect-actions button { width: auto; }
.crawler-inventory-outcome, .crawler-inventory-use-outcome, .crawler-inventory-cast-result, .crawler-character-skill-outcome, .crawler-character-train-outcome {
  margin: 0 0 0.4rem; padding: 0.3rem 0.5rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.85rem;
}
:is(.crawler-inventory-outcome, .crawler-inventory-use-outcome, .crawler-inventory-cast-result, .crawler-character-skill-outcome, .crawler-character-train-outcome)[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.9); color: #e8c8b0; }
/* The spellbook: a page per school, the picked spell beside it, and the Mixing page. */
.crawler-magic-book-layout { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(16rem, 1fr); gap: 0.8rem; align-items: start; }
.crawler-magic-book-spells { display: grid; gap: 0.25rem; }
.crawler-session .crawler-magic-book-spell { display: flex; justify-content: space-between; gap: 0.6rem; width: auto; padding: 0.3rem 0.6rem; text-align: left; }
.crawler-session .crawler-magic-book-spell[data-known='false'] { opacity: 0.55; }
.crawler-session .crawler-magic-book-spell[data-picked='true'] { border-color: #e2b060; background: rgba(96, 78, 50, 0.9); }
.crawler-magic-book-spell-meta { color: #b9ad8c; font-size: 0.8rem; }
.crawler-magic-book-detail { padding: 0.5rem; border: 1px solid #5a4b31; background: rgba(0, 0, 0, 0.3); }
.crawler-spell-ready { color: #8fc27a; }
.crawler-spell-refusal { color: #e8c8b0; }
.crawler-magic-book-outcome { margin: 0 0 0.4rem; padding: 0.3rem 0.5rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.85rem; }
.crawler-magic-book-outcome[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.9); color: #e8c8b0; }
/* The dialogue: the speaker beside their words. */
.crawler-dialogue-layout { display: grid; grid-template-columns: 15rem minmax(0, 1fr); gap: 1rem; align-items: start; }
.crawler-dialogue-speaker { display: grid; gap: 0.4rem; justify-items: center; text-align: center; }
.crawler-dialogue-face { width: 9rem; height: 10.5rem; object-fit: cover; image-rendering: pixelated; border: 2px solid #8a7446; background: #222; display: grid; place-items: center; font-size: 3rem; color: #8d8a7a; }
.crawler-dialogue-name { margin: 0; color: #f0e4c4; font-size: 1.05rem; font-weight: 600; }
.crawler-dialogue-greeting { margin: 0; color: #d8cba6; font-style: italic; }
.crawler-dialogue-others { display: flex; flex-wrap: wrap; gap: 0.3rem; justify-content: center; }
.crawler-session .crawler-dialogue-other { display: grid; justify-items: center; gap: 0.15rem; width: auto; padding: 0.2rem; font-size: 0.75rem; }
.crawler-dialogue-other-face { width: 2.6rem; height: 3rem; object-fit: cover; image-rendering: pixelated; display: grid; place-items: center; background: #222; }
.crawler-dialogue-said { max-height: 14rem; overflow-y: auto; margin-bottom: 0.6rem; padding: 0.4rem 0.6rem; border: 1px solid #5a4b31; background: rgba(0, 0, 0, 0.3); }
.crawler-dialogue-line { margin: 0 0 0.35rem; color: #e8dcc0; }
.crawler-dialogue-topics { display: grid; gap: 0.3rem; margin-bottom: 0.4rem; }
.crawler-session .crawler-dialogue-topic { text-align: left; padding: 0.35rem 0.7rem; font-size: 0.92rem; }
.crawler-dialogue-withheld { margin: 0 0 0.4rem; padding-left: 1.1rem; color: #9d927a; font-size: 0.82rem; }
.crawler-dialogue-thieves { display: flex; gap: 0.3rem; margin-bottom: 0.4rem; }
.crawler-dialogue-thieves button { width: auto; }
/* The counter: the establishment, a page per thing it does. */
.crawler-counter-head { display: grid; grid-template-columns: minmax(0, 1fr) auto; column-gap: 1rem; }
.crawler-counter-title { margin: 0; color: #f0e4c4; font-size: 1.05rem; font-weight: 600; }
.crawler-counter-state { margin: 0; grid-column: 1; color: #b9ad8c; font-size: 0.85rem; }
.crawler-counter-state[data-state='closed'] { color: #e8a080; }
.crawler-counter-purse { margin: 0; grid-column: 2; grid-row: 1 / span 2; align-self: center; color: #f2cf5b; font-weight: 600; }
.crawler-counter-page { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(14rem, 1fr); grid-auto-rows: auto; gap: 0.8rem; align-items: start; margin-bottom: 0.8rem; }
.crawler-counter > .crawler-actions { clear: both; margin-top: 0.8rem; }
.crawler-counter-card { display: flex; flex-direction: column; align-items: flex-start; }
.crawler-counter-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(5rem, 1fr)); gap: 0.35rem; }
.crawler-session .crawler-counter-tile { display: grid; grid-template-rows: 4.5rem auto; place-items: center; width: auto; padding: 0.2rem; border: 1px solid #4b4030; background: rgba(0, 0, 0, 0.45); }
.crawler-session .crawler-counter-tile[data-picked='true'] { border-color: #e2b060; box-shadow: 0 0 5px rgba(226, 176, 96, 0.7); }
.crawler-counter-caption { font-size: 0.72rem; color: #f2cf5b; }
.crawler-counter-detail, .crawler-counter-card { padding: 0.5rem; border: 1px solid #5a4b31; background: rgba(0, 0, 0, 0.3); }
.crawler-counter-detail button, .crawler-counter-card button { width: auto; margin: 0.2rem 0.3rem 0 0; }
.crawler-counter-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr)); gap: 0.5rem; grid-column: 1 / -1; }
.crawler-counter-page > .crawler-counter-card, .crawler-counter-page > .crawler-counter-amount, .crawler-counter-page > .crawler-counter-who, .crawler-counter-page > .crawler-counter-notice, .crawler-counter-page > .crawler-inspect-hint { grid-column: 1 / -1; }
.crawler-counter-choice { display: flex; flex-wrap: wrap; align-items: center; gap: 0.4rem; }
.crawler-counter-reason { color: #e8c8b0; font-size: 0.8rem; }
.crawler-counter-who { display: flex; align-items: center; gap: 0.5rem; }
.crawler-counter-notice { margin: 0 0 0.4rem; padding: 0.4rem 0.6rem; border-left: 2px solid #8a7446; color: #e8dcc0; }
.crawler-counter-outcome, .crawler-dialogue-outcome { margin: 0.3rem 0; padding: 0.3rem 0.5rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.85rem; }
:is(.crawler-counter-outcome, .crawler-dialogue-outcome)[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.9); color: #e8c8b0; }
/* The journal: a page per book. */
.crawler-journal-standing { margin: 0 0 0.4rem; color: #b9ad8c; font-size: 0.85rem; }
.crawler-journal-page { display: grid; gap: 0.5rem; }
.crawler-journal-quest { padding: 0.5rem 0.7rem; border: 1px solid #5a4b31; background: rgba(0, 0, 0, 0.3); }
.crawler-journal-quest-head { display: flex; justify-content: space-between; align-items: baseline; gap: 1rem; }
.crawler-journal-quest-state { color: #d6a76a; font-size: 0.8rem; text-transform: capitalize; }
.crawler-journal-quest[data-state='completed'] .crawler-journal-quest-state, .crawler-journal-quest[data-state='turned-in'] .crawler-journal-quest-state { color: #8fc27a; }
.crawler-journal-quest-note { margin: 0.3rem 0; color: #e8dcc0; }
.crawler-journal-objectives { margin: 0.2rem 0; padding-left: 1.2rem; }
.crawler-journal-objectives li[data-met='true'] { color: #8fc27a; }
.crawler-journal-objectives li[data-met='true']::marker { content: '✓ '; }
.crawler-journal-ready { color: #8fc27a; }
.crawler-journal-residue { color: #9d927a; font-size: 0.8rem; font-style: italic; }
.crawler-journal-rows { margin: 0; padding: 0; list-style: none; display: grid; gap: 0.2rem; }
.crawler-journal-row { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; gap: 0.8rem; padding: 0.25rem 0.4rem; border-bottom: 1px solid rgba(210, 196, 158, 0.12); }
.crawler-journal-row[data-marked='true'] .crawler-journal-row-label::before { content: '✓ '; color: #8fc27a; }
.crawler-journal-row-detail, .crawler-journal-row-state { color: #b9ad8c; font-size: 0.82rem; }
.crawler-session .crawler-journal-open-map { width: auto; justify-self: start; }
/* The automap book: zoom inside a scrolling frame. */
.crawler-map-zoom { display: flex; align-items: center; gap: 0.4rem; margin-bottom: 0.4rem; }
.crawler-map-zoom button { width: auto; min-width: 2rem; }
.crawler-screen[data-screen='map'] .crawler-map-frame { overflow: auto; max-height: calc(100vh - 21rem); }
.crawler-screen[data-screen='map'] .crawler-map-frame[data-zoom='2'] .crawler-map-drawing { height: calc((100vh - 21rem) * 2); }
.crawler-screen[data-screen='map'] .crawler-map-frame[data-zoom='4'] .crawler-map-drawing { height: calc((100vh - 21rem) * 4); }
@media (max-width: 900px) { .crawler-inventory { grid-template-columns: minmax(0, 1fr); } }
`;
