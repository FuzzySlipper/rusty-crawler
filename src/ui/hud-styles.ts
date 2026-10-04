/**
 * The persistent adventure HUD styles: party bar, place column, facing line and world reticle. The HUD only
 * presents projection facts; visibility of the reticle is handed by the frame's presentation screen marker.
 */
export const HUD_STYLES = `
/* The bottom bar: four portraits, the purse and clock, the adventure controls and the books. */
.crawler-hud {
  position: absolute;
  left: 0; right: 0; bottom: 0;
  height: 8.4rem;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 7.5rem 12rem 12rem;
  gap: 0.6rem;
  padding: 0.45rem 0.6rem;
  box-sizing: border-box;
  background: linear-gradient(#2a241b, #16130f);
  border-top: 2px solid #8a7446;
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.5);
}
.crawler-roster { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.45rem; min-width: 0; }
.crawler-session .crawler-member {
  position: relative;
  display: grid;
  grid-template-columns: 4.1rem minmax(0, 1fr);
  grid-template-rows: auto auto auto 1fr;
  column-gap: 0.4rem;
  align-items: start;
  width: auto;
  height: 100%;
  padding: 0.25rem;
  text-align: left;
  border: 2px solid #4b4030;
  border-radius: 0.2rem;
  background: rgba(10, 9, 7, 0.8);
  overflow: hidden;
}
.crawler-session .crawler-member[data-selected='yes'] { border-color: #e2b060; box-shadow: 0 0 6px rgba(226, 176, 96, 0.6); }
.crawler-session .crawler-member[data-state='down'] { filter: grayscale(0.9) brightness(0.6); }
.crawler-member-face, .crawler-member-initial {
  grid-row: 1 / span 4;
  width: 4.1rem; height: 4.75rem;
  object-fit: cover;
  image-rendering: pixelated;
  border: 1px solid #000;
  background: #222;
}
.crawler-member-initial { display: grid; place-items: center; font-size: 1.8rem; color: #8d8a7a; }
.crawler-member-name { font-weight: 600; color: #f0e4c4; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.crawler-bar { height: 0.45rem; margin-top: 0.25rem; background: #000; border: 1px solid #3a3226; }
.crawler-bar-fill { height: 100%; background: #3fae4b; }
.crawler-bar-spell .crawler-bar-fill { background: #3f6fd1; }
/* The state line sits under the bars whether or not a member has a spell bar, so every card reads it in one place. */
.crawler-member-state { grid-column: 2; grid-row: 4; align-self: start; margin-top: 0.2rem; font-size: 0.68rem; color: #d6a76a; line-height: 1.2; overflow: hidden; }
.crawler-member[data-state='recovering'] .crawler-member-state { color: #9fb6d6; }
.crawler-purse { display: flex; flex-direction: column; justify-content: center; gap: 0.15rem; font-variant-numeric: tabular-nums; }
.crawler-coins { color: #f2cf5b; font-weight: 600; }
.crawler-food { color: #c9d48a; }
.crawler-date, .crawler-time { color: #b9ad8c; font-size: 0.78rem; }
.crawler-hud-actions, .crawler-books { display: grid; grid-template-columns: 1fr 1fr; grid-auto-rows: minmax(0, 1fr); gap: 0.2rem; }
.crawler-session .crawler-hud-actions button, .crawler-session .crawler-books button { padding: 0.1rem 0.3rem; font-size: 0.72rem; }
.crawler-session .crawler-hud-book[data-open='yes'] { border-color: #e2b060; background: rgba(96, 78, 50, 0.95); }
.crawler-session .crawler-hud-diagnostics { color: #8d8a7a; }

/* The right column: where the party is, the automap, and what is running on it. */
.crawler-hud-side {
  position: absolute;
  top: 0.6rem; right: 0.6rem;
  width: 12rem;
  max-height: calc(100vh - 10rem);
  overflow-y: auto;
  padding: 0.4rem;
  background: rgba(14, 12, 10, 0.78);
  border: 1px solid #5a4b31;
  border-radius: 0.25rem;
}
.crawler-hud-place { margin: 0 0 0.3rem; color: #f0dca0; font-weight: 600; text-align: center; }
.crawler-minimap .crawler-step-head, .crawler-minimap .crawler-map-state, .crawler-minimap .crawler-map-detection { display: none; }
.crawler-minimap .crawler-map-drawing { width: 100%; height: auto; aspect-ratio: 1; background: #0c0a08; }
.crawler-hud-effects { margin: 0.35rem 0 0; padding: 0; list-style: none; font-size: 0.72rem; }
.crawler-effect { color: #9fc7e6; }
.crawler-hud-side .crawler-followers { margin-top: 0.35rem; font-size: 0.72rem; }

/* The line along the top: what the party faces and what the last thing it did said. */
.crawler-hud-message { position: absolute; top: 0.6rem; left: 50%; transform: translateX(-50%); max-width: 34rem; text-align: center; pointer-events: none; }
.crawler-hud-message p { margin: 0 0 0.25rem; padding: 0.2rem 0.6rem; background: rgba(10, 9, 7, 0.72); border-radius: 0.2rem; }
.crawler-facing { color: #f0e4c4; }
.crawler-hud-said { color: #e8c98a; font-size: 0.82rem; }
.crawler-hud-said[data-outcome='refused'] { color: #f0b49a; }

/* The world reticle is a bounded context read, never a second selection. The Engine-backed projection supplies its
   target, disposition, distance and refusal reason; the companion only makes those facts legible over the scene. */
.crawler-reticle {
  position: absolute;
  top: 50%; left: 50%;
  z-index: 2;
  display: grid;
  justify-items: center;
  gap: 0.05rem;
  transform: translate(-50%, -50%);
  min-width: 9rem;
  max-width: min(22rem, calc(100vw - 2rem));
  text-align: center;
  pointer-events: none;
  text-shadow: 0 1px 3px #000, 0 0 5px #000;
}
.crawler-reticle-mark { color: #f0dca0; font-size: 1.45rem; line-height: 1; }
.crawler-reticle-target, .crawler-reticle-range {
  padding: 0.08rem 0.45rem;
  background: rgba(8, 7, 6, 0.68);
  border-radius: 0.15rem;
  font-size: 0.72rem;
}
.crawler-reticle-target { color: #f0e4c4; font-weight: 600; }
.crawler-reticle-range { color: #b9ad8c; }
.crawler-reticle[data-state='out-of-reach'] .crawler-reticle-range,
.crawler-reticle[data-state='occluded'] .crawler-reticle-range,
.crawler-reticle[data-state='unavailable'] .crawler-reticle-range,
.crawler-reticle[data-state='locked'] .crawler-reticle-range,
.crawler-reticle[data-state='stale-target'] .crawler-reticle-range { color: #f0b49a; }
`;
