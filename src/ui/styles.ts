/**
 * The companion's stylesheet, mounted once beside the panel. It styles what the sections draw and decides
 * nothing: a `hidden` attribute is honoured by every rule, so a control or a line the product says is absent
 * stays absent.
 */
export const STYLES = `
.crawler-session {
  position: fixed;
  top: 0.75rem;
  left: 0.75rem;
  min-width: 15rem;
  max-width: 24rem;
  /* The creation screen is taller than a short viewport, and a panel that runs off the bottom would put
     the flow's own controls where nobody can reach them. */
  max-height: calc(100vh - 1.5rem);
  overflow-y: auto;
  padding: 0.6rem 0.75rem;
  border: 1px solid rgba(210, 196, 158, 0.35);
  border-radius: 0.35rem;
  background: rgba(18, 16, 14, 0.82);
  color: #e8e0cc;
  font: 13px/1.45 system-ui, sans-serif;
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
.crawler-creation { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-creation[hidden] { display: none; }
.crawler-creation .crawler-step-head { margin: 0 0 0.35rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-creation .crawler-row { margin: 0 0 0.25rem; }
.crawler-creation .crawler-row-label { display: block; color: #b9ad8c; font-size: 0.72rem; }
.crawler-creation .crawler-options { display: flex; flex-wrap: wrap; gap: 0.2rem; }
.crawler-creation .crawler-options button { width: auto; padding: 0.1rem 0.35rem; font-size: 0.72rem; }
.crawler-creation .crawler-options button[data-selected='true'] { border-color: rgba(226, 176, 96, 0.9); background: rgba(96, 78, 50, 0.9); }
.crawler-creation .crawler-options button[data-available='false'] { opacity: 0.7; }
.crawler-creation .crawler-fixed { color: #8d8a7a; font-size: 0.75rem; }
.crawler-creation .crawler-attribute { display: flex; align-items: center; gap: 0.3rem; font-size: 0.72rem; line-height: 1.3; }
.crawler-creation .crawler-attribute span { flex: 1; }
.crawler-creation .crawler-attribute button { width: 1.3rem; padding: 0 0; font-size: 0.72rem; text-align: center; }
.crawler-creation .crawler-name { display: flex; gap: 0.2rem; margin-top: 0.2rem; }
/* A row the screen hides must actually be hidden: these two rows carry a display rule of their own, which
   without this would keep the accepting player's name box and the creation controls on screen after the
   party has been accepted, and would keep them in the tab order too. */
.crawler-creation .crawler-name[hidden] { display: none; }
.crawler-creation .crawler-actions[hidden] { display: none; }
.crawler-creation .crawler-name input {
  flex: 1;
  min-width: 0;
  padding: 0.2rem 0.35rem;
  border: 1px solid rgba(210, 196, 158, 0.5);
  border-radius: 0.25rem;
  background: rgba(18, 16, 14, 0.9);
  color: inherit;
  font: inherit;
}
.crawler-creation .crawler-actions { display: flex; gap: 0.2rem; margin-top: 0.3rem; }
.crawler-creation .crawler-actions button { padding: 0.2rem 0.4rem; font-size: 0.75rem; }
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
.crawler-map-state { margin: 0 0 0.3rem; color: #cbbf9e; font-size: 0.8rem; }
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
.crawler-rest .crawler-actions { display: flex; flex-wrap: wrap; gap: 0.2rem; }
.crawler-rest .crawler-actions button { width: auto; padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-rest-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-rest-result[hidden] { display: none; }
.crawler-rest-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-problems { margin: 0.4rem 0 0; padding: 0.25rem 0.4rem 0.25rem 1.2rem; border-left: 2px solid rgba(226, 120, 96, 0.9); color: #e8c8b0; font-size: 0.7rem; }
.crawler-problems[hidden] { display: none; }
`;
