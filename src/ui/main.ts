/**
 * The product's DOM companion: it renders the session projection and reports semantic actions.
 *
 * It owns no state, evaluates no rules, and starts no loop or timer. Every value it shows arrived in
 * the last projection from the product, and every action it sends is an action name the product
 * defines. When the projection says the session is held, the button offers to release it, and nothing
 * here decides that on its own.
 */

interface ProjectionEnvelope {
  readonly contract: string;
  readonly value: unknown;
}

interface ProductUiContext {
  readonly projection?: {
    subscribe(listener: (projection: ProjectionEnvelope | null) => void): () => void;
  };
  readonly intents?: {
    claim(
      intent: string,
      value: { kind: 'product-payload'; contract: string; data: Record<string, unknown> },
    ): void;
  };
}

/** The projection contract this companion renders; any other contract is not ours to interpret. */
const UI_CONTRACT = 'crawler.ui.snapshot.v1';

/** The intent and payload contract the product declares for semantic actions. */
const UI_ACTION_INTENT = 'crawler.ui';
const UI_ACTION_CONTRACT = 'crawler.ui.action.v1';

/** The two session actions this companion can ask for. */
const ACTION_PAUSE = 'session.pause';
const ACTION_RESUME = 'session.resume';

interface CompositionView {
  readonly ruleset: string;
  readonly title: string;
  /** The game bundle the product started from, empty when no bundle was selected. */
  readonly bundle: string;
  readonly contentPacks: number;
}

/** The world the party is in. Empty when the session has no world loaded. */
interface WorldView {
  readonly place: string;
  readonly name: string;
  readonly kind: string;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly yaw: number;
  readonly visited: number;
  readonly places: number;
}

interface SessionView {
  readonly mode: string;
  readonly simulationSeconds: number;
  readonly admittedSteps: number;
  readonly updates: number;
}

/**
 * What the party's last admitted movement step did, as the panel shows it. `motion` is `none` when the
 * session has no movement facts at all — no world, or no step yet — and the remaining fields then
 * describe no step rather than a quiet one.
 */
interface MovementView {
  /** The state the last step left the party in: `none`, `grounded`, or `airborne`. */
  readonly motion: string;
  /** What refused the step: a reason word, or `none` when nothing did. */
  readonly blocked: string;
  /** How high an accepted step-up raised the party; zero when the engine accepted none. */
  readonly stepRise: number;
  readonly fallDistance: number;
  readonly fallDamage: number;
}

/**
 * Where the game clock stands, as the product published it. `present` is false when the session's
 * ruleset composed no clock, and then the remaining fields carry nothing to show.
 */
interface ClockView {
  readonly present: boolean;
  /** The date on the clock's calendar, as the calendar's own numbers. */
  readonly date: string;
  /** The time of day, whole minutes. */
  readonly time: string;
  /** Which half of the daylight window the clock stands in: `day` or `night`. */
  readonly daylight: string;
  /** Whole game days elapsed since the session began. */
  readonly elapsedDays: number;
}

/**
 * The party's own accounts and standing, as the product published them. `present` is false when the
 * session holds no party, which is what content that declares none gets.
 */
interface PartyView {
  readonly present: boolean;
  readonly members: number;
  readonly coins: number;
  readonly provisions: number;
  /** The unit the provisions are stated in, so the number is never shown without its measure. */
  readonly unit: string;
  readonly reputation: number;
  readonly fame: number;
  /** The conditions acting on the party, empty when none act. */
  readonly conditions: string;
}

interface SnapshotView {
  readonly composition: CompositionView;
  readonly session: SessionView;
  readonly world: WorldView;
  readonly movement: MovementView;
  readonly clock: ClockView;
  readonly party: PartyView;
}

/** The clock of a session that has none, which the panel shows as not knowing rather than as a date. */
const CLOCK_UNKNOWN: ClockView = {
  present: false,
  date: '',
  time: '',
  daylight: '',
  elapsedDays: 0,
};

/** The party of a session that holds none. */
const PARTY_UNKNOWN: PartyView = {
  present: false,
  members: 0,
  coins: 0,
  provisions: 0,
  unit: '',
  reputation: 0,
  fame: 0,
  conditions: '',
};

const STYLES = `
.crawler-session {
  position: fixed;
  top: 0.75rem;
  left: 0.75rem;
  min-width: 15rem;
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
`;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

/**
 * Reads the clock block, or the not-known clock. A block that is missing, or that is present but does
 * not carry the facts this panel renders, is not a reason to reject the whole projection: the session it
 * describes has a clock this companion cannot read, and showing that is honest where refusing to render
 * everything else would hide the rest of the session behind it.
 */
function readClock(value: unknown): ClockView {
  if (!isRecord(value) || value.present !== true) return CLOCK_UNKNOWN;
  const { date, time, daylight, elapsedDays } = value;
  if (
    typeof date !== 'string' ||
    typeof time !== 'string' ||
    typeof daylight !== 'string' ||
    typeof elapsedDays !== 'number'
  ) {
    return CLOCK_UNKNOWN;
  }

  return { present: true, date, time, daylight, elapsedDays };
}

/** Reads the party block, or the not-known party, on the same terms as the clock block. */
function readParty(value: unknown): PartyView {
  if (!isRecord(value) || value.present !== true) return PARTY_UNKNOWN;
  const { members, coins, provisions, unit, reputation, fame, conditions } = value;
  if (
    typeof members !== 'number' ||
    typeof coins !== 'number' ||
    typeof provisions !== 'number' ||
    typeof unit !== 'string' ||
    typeof reputation !== 'number' ||
    typeof fame !== 'number' ||
    typeof conditions !== 'string'
  ) {
    return PARTY_UNKNOWN;
  }

  return { present: true, members, coins, provisions, unit, reputation, fame, conditions };
}

function readSnapshot(value: unknown): SnapshotView | null {
  if (!isRecord(value)) return null;
  const composition = value.composition;
  const session = value.session;
  // A projection that does not carry a session is not one this companion understands, and rendering
  // half of it would show a panel that disagrees with the product.
  if (!isRecord(composition) || !isRecord(session)) return null;

  // A projection with no world, though, is not a broken projection: a session whose content has not
  // been generated yet has no places, and the panel says so rather than refusing to render at all.
  const world = isRecord(value.world) ? value.world : {};
  // Movement facts are optional in the same way: a session that has never moved has none to publish,
  // and the panel shows that it does not know rather than that the way is clear.
  const movement = isRecord(value.movement) ? value.movement : {};
  // The clock and the party are optional on the same terms: a ruleset that composed neither publishes
  // blocks that say so, and a projection that carries none at all still describes a session worth showing.
  const clock = readClock(value.clock);
  const party = readParty(value.party);
  const { ruleset, title, bundle, contentPacks } = composition;
  const { mode, simulationSeconds, admittedSteps, updates } = session;
  if (
    typeof ruleset !== 'string' ||
    typeof title !== 'string' ||
    typeof bundle !== 'string' ||
    typeof contentPacks !== 'number' ||
    typeof mode !== 'string' ||
    typeof simulationSeconds !== 'number' ||
    typeof admittedSteps !== 'number' ||
    typeof updates !== 'number'
  ) {
    return null;
  }
  const place = world.place ?? '';
  const placeName = world.name ?? '';
  const kind = world.kind ?? '';
  const x = world.x ?? 0;
  const y = world.y ?? 0;
  const z = world.z ?? 0;
  const yaw = world.yaw ?? 0;
  const visited = world.visited ?? 0;
  const places = world.places ?? 0;
  const motion = movement.motion ?? 'none';
  const blocked = movement.blocked ?? 'none';
  const stepRise = movement.stepRise ?? 0;
  const fallDistance = movement.fallDistance ?? 0;
  const fallDamage = movement.fallDamage ?? 0;
  if (
    typeof place !== 'string' ||
    typeof placeName !== 'string' ||
    typeof kind !== 'string' ||
    typeof x !== 'number' ||
    typeof y !== 'number' ||
    typeof z !== 'number' ||
    typeof yaw !== 'number' ||
    typeof visited !== 'number' ||
    typeof places !== 'number' ||
    typeof motion !== 'string' ||
    typeof blocked !== 'string' ||
    typeof stepRise !== 'number' ||
    typeof fallDistance !== 'number' ||
    typeof fallDamage !== 'number'
  ) {
    return null;
  }

  return {
    composition: { ruleset, title, bundle, contentPacks },
    session: { mode, simulationSeconds, admittedSteps, updates },
    world: { place, name: placeName, kind, x, y, z, yaw, visited, places },
    movement: { motion, blocked, stepRise, fallDistance, fallDamage },
    clock,
    party,
  };
}

/**
 * Mounts the companion into `root` and returns a disposer. The returned object holds the only
 * subscription this module creates; disposing it leaves no listener behind.
 */
export function mountProductUi(root: HTMLElement, context: ProductUiContext): { dispose(): void } {
  const style = document.createElement('style');
  style.textContent = STYLES;

  const panel = document.createElement('section');
  panel.className = 'crawler-session';
  panel.dataset.mode = 'starting';
  // Nothing has moved yet, so the panel's own state says it has no movement facts rather than that
  // nothing stopped the party.
  panel.dataset.motion = 'none';
  panel.dataset.blocked = 'none';
  panel.dataset.clock = 'none';
  panel.dataset.party = 'none';
  panel.dataset.light = 'unknown';

  const title = document.createElement('h1');
  const ruleset = document.createElement('p');
  ruleset.className = 'crawler-ruleset';
  const bundle = document.createElement('p');
  bundle.className = 'crawler-bundle';
  const place = document.createElement('p');
  place.className = 'crawler-place';

  const details = document.createElement('dl');
  const rows: Record<string, HTMLElement> = {};
  for (const [key, label] of [
    ['mode', 'Session'],
    ['simulation', 'Simulation'],
    ['steps', 'Admitted steps'],
    ['updates', 'Updates'],
    ['content', 'Content'],
    ['date', 'Date'],
    ['time', 'Time'],
    ['days', 'Days'],
    ['party', 'Party'],
    ['coins', 'Coins'],
    ['food', 'Food'],
    ['standing', 'Standing'],
    ['condition', 'Condition'],
    ['place', 'Place'],
    ['pose', 'Position'],
    ['explored', 'Explored'],
    ['motion', 'Motion'],
    ['blocked', 'Blocked'],
    ['step', 'Step up'],
    ['fall', 'Fall'],
  ] as const) {
    const term = document.createElement('dt');
    term.textContent = label;
    const value = document.createElement('dd');
    value.textContent = '—';
    details.append(term, value);
    rows[key] = value;
  }

  const action = document.createElement('button');
  action.type = 'button';
  action.disabled = true;
  action.textContent = 'Starting…';

  const hint = document.createElement('p');
  hint.className = 'crawler-hint';
  hint.textContent = 'Pause or resume with the button, or with the P key.';

  panel.append(title, ruleset, bundle, place, details, action, hint);
  root.append(style, panel);

  let current = 'starting';

  const claim = (name: string): void => {
    context.intents?.claim(UI_ACTION_INTENT, {
      kind: 'product-payload',
      contract: UI_ACTION_CONTRACT,
      data: { action: name },
    });
  };

  action.addEventListener('click', () => {
    if (current === 'running') claim(ACTION_PAUSE);
    else if (current === 'paused') claim(ACTION_RESUME);
  });

  const render = (snapshot: SnapshotView): void => {
    current = snapshot.session.mode;
    title.textContent = 'Rusty Crawler';
    ruleset.textContent = snapshot.composition.title;
    bundle.textContent =
      snapshot.composition.bundle === ''
        ? 'No game bundle selected'
        : `${snapshot.composition.bundle} · ${snapshot.composition.contentPacks} pack${snapshot.composition.contentPacks === 1 ? '' : 's'}`;
    panel.dataset.mode = current;
    panel.dataset.ruleset = snapshot.composition.ruleset;
    panel.dataset.bundle = snapshot.composition.bundle;
    rows.mode.textContent = current;
    rows.simulation.textContent = `${snapshot.session.simulationSeconds.toFixed(1)} s`;
    rows.steps.textContent = String(snapshot.session.admittedSteps);
    rows.updates.textContent = String(snapshot.session.updates);
    rows.content.textContent = String(snapshot.composition.contentPacks);
    // The clock's own facts, printed as they arrived: the date and the time are what the calendar and the
    // clock published, and the panel derives none of them. `present` is what tells a session whose ruleset
    // composed no clock from one standing on the first day of its calendar, and the two must not look alike.
    const { clock, party } = snapshot;
    panel.dataset.clock = clock.present ? 'present' : 'none';
    panel.dataset.party = party.present ? 'present' : 'none';
    panel.dataset.light = clock.present ? clock.daylight : 'unknown';
    rows.date.textContent = clock.present ? clock.date : '—';
    rows.time.textContent = clock.present ? `${clock.time} · ${clock.daylight}` : '—';
    rows.days.textContent = clock.present ? String(clock.elapsedDays) : '—';
    // The party's accounts and standing are read from the party the product holds, never counted here.
    rows.party.textContent = party.present ? String(party.members) : '—';
    rows.coins.textContent = party.present ? String(party.coins) : '—';
    rows.food.textContent = party.present ? `${party.provisions} ${party.unit}` : '—';
    rows.standing.textContent = party.present ? `${party.reputation} / ${party.fame}` : '—';
    rows.condition.textContent = party.present && party.conditions !== '' ? party.conditions : '—';
    const world = snapshot.world;
    place.textContent =
      world.places === 0
        ? 'No world loaded'
        : `${world.name}${world.kind === '' ? '' : ` · ${world.kind}`}`;
    panel.dataset.place = world.place;
    rows.place.textContent = world.place === '' ? '—' : world.place;
    rows.pose.textContent =
      world.places === 0
        ? '—'
        : `${world.x.toFixed(0)}, ${world.y.toFixed(0)}, ${world.z.toFixed(0)} @ ${world.yaw.toFixed(0)}`;
    rows.explored.textContent = `${world.visited} / ${world.places}`;
    const movement = snapshot.movement;
    // A session with no movement facts shows that it does not know what the last step did. Showing a
    // clear path there would be the very mistake this panel exists to prevent: a refused step and an
    // input that never arrived would look the same again.
    panel.dataset.motion = movement.motion;
    panel.dataset.blocked = movement.blocked;
    rows.motion.textContent = movement.motion === 'none' ? '—' : movement.motion;
    rows.blocked.textContent = movement.blocked === 'none' ? '—' : movement.blocked;
    rows.step.textContent = movement.stepRise > 0 ? `+${movement.stepRise.toFixed(1)}` : '—';
    rows.fall.textContent =
      movement.fallDistance > 0
        ? `${movement.fallDistance.toFixed(0)} · ${movement.fallDamage.toFixed(0)} damage`
        : '—';
    if (current === 'running') {
      action.disabled = false;
      action.textContent = 'Pause session';
    } else if (current === 'paused') {
      action.disabled = false;
      action.textContent = 'Resume session';
    } else {
      action.disabled = true;
      action.textContent = current === 'stopped' ? 'Session stopped' : 'Starting…';
    }
  };

  const unsubscribe = context.projection?.subscribe((projection) => {
    if (projection?.contract !== UI_CONTRACT) return;
    const snapshot = readSnapshot(projection.value);
    if (snapshot !== null) render(snapshot);
  });

  return {
    dispose(): void {
      unsubscribe?.();
      panel.remove();
      style.remove();
    },
  };
}
