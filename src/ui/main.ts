/**
 * The product's DOM companion: it renders the session projection and reports semantic actions.
 *
 * It owns no state, evaluates no rules, and starts no loop or timer. Every value it shows arrived in
 * the last projection from the product, and every action it sends is an action name the product
 * defines. When the projection says the session is held, the button offers to release it; when it says
 * a party is being created, the screen offers exactly the choices that projection listed and shows the
 * rule the flow answered with when one of them was refused. Nothing here decides on its own that a
 * choice is illegal — the refusal is the product's answer, and a screen that hid it would be hiding the
 * game's rules.
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

/** The session actions this companion can ask for. */
const ACTION_PAUSE = 'session.pause';
const ACTION_RESUME = 'session.resume';
const ACTION_SAVE = 'session.save';
const ACTION_USE = 'party.use';

/**
 * The creation actions this companion reports. Each names a choice, the product's flow validates it, and
 * its refusal is what the screen shows. The names are the product's wire vocabulary: this companion sends
 * them and reads nothing back except the projection.
 */
const ACTION_SELECT_MEMBER = 'creation.select-member';
const ACTION_SELECT_PORTRAIT = 'creation.select-portrait';
const ACTION_SELECT_CLASS = 'creation.select-class';
const ACTION_SET_NAME = 'creation.set-name';
const ACTION_RAISE_ATTRIBUTE = 'creation.raise-attribute';
const ACTION_LOWER_ATTRIBUTE = 'creation.lower-attribute';
const ACTION_CHOOSE_SKILL = 'creation.choose-skill';
const ACTION_REMOVE_SKILL = 'creation.remove-skill';
const ACTION_ADVANCE = 'creation.advance';
const ACTION_ACCEPT = 'creation.accept';

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

/**
 * How the session stands with its save slot, as the product published it. `state` is `none` until a save
 * is asked for, `saved` when one landed, and `failed` when one did not — and `message` then says what went
 * wrong, because a save that could not land must not look like one that did.
 */
interface SaveView {
  /** Whether the session has a save store at all. */
  readonly available: boolean;
  /** Whether this session was composed from the save in its slot rather than started fresh. */
  readonly resumed: boolean;
  /** The slot a save is written to. */
  readonly slot: string;
  /** `none`, `saved`, or `failed`. */
  readonly state: string;
  /** The game date and time the last save landed, empty when none has. */
  readonly at: string;
  /** Which failure this was, empty unless the state is `failed`. */
  readonly code: string;
  /** What happened, in the terms of the save. */
  readonly message: string;
}

/**
 * What the party is facing and what using it did, as the product published it. `available` is false when the
 * session holds no interaction at all, `reason` says why the reticle holds or refuses what it does, and
 * `outcome`, `code`, `message`, and `residue` describe the last use — a refusal keeps its own code and
 * sentence, because a use that silently did nothing must not look like one that did.
 */
interface InteractionView {
  /** Whether the session holds an interaction mechanism at all. */
  readonly available: boolean;
  /** The focused target's kind, empty when nothing is focused. */
  readonly target: string;
  /** What the focused target is called, empty when nothing is focused. */
  readonly label: string;
  /** The use that applies to it, empty when nothing is focused. */
  readonly verb: string;
  /** What the party has already done to it. */
  readonly state: string;
  /** How far it stands from the party, zero when nothing is focused. */
  readonly distance: number;
  /** Why the reticle holds or refuses what it does. */
  readonly reason: string;
  /** What the focused target requires, in the order the checks happen. */
  readonly requires: readonly string[];
  /** `none`, `applied`, or `refused`. */
  readonly outcome: string;
  /** The last refusal's code, empty when the last use applied or none has happened. */
  readonly code: string;
  /** What the last use reported. */
  readonly message: string;
  /** What the last use could not deliver. */
  readonly residue: string;
}

/** One member of the party being created, as the flow published it. */
interface CreationMemberView {
  readonly index: number;
  /** Where this member stands: `portrait`, `class`, `name`, `attributes`, `skills`, or `complete`. */
  readonly step: string;
  readonly name: string;
  /** The race the portrait decided, empty while no portrait has been chosen. */
  readonly race: string;
  /** The class chosen, empty while none has been. */
  readonly class: string;
  readonly portrait: string;
  /** How many attribute points this member has still to spend. */
  readonly pool: number;
}

/** One member of the party a session accepted, as it stands in the party being played. */
interface CreationPartyMemberView {
  readonly index: number;
  readonly name: string;
  readonly race: string;
  readonly class: string;
  readonly portrait: string;
}

/** One portrait creation offers. */
interface CreationPortraitView {
  readonly id: string;
  readonly name: string;
  readonly race: string;
  readonly selected: boolean;
}

/** One class creation offers. */
interface CreationClassView {
  readonly id: string;
  readonly name: string;
  readonly selected: boolean;
}

/** One skill of the member being created, and where it stands: `fixed`, `chosen`, or `available`. */
interface CreationSkillView {
  readonly id: string;
  readonly name: string;
  readonly state: string;
}

/** One attribute of the member being created, with the range its race allows. */
interface CreationAttributeView {
  readonly id: string;
  readonly name: string;
  readonly value: number;
  readonly minimum: number;
  readonly maximum: number;
  readonly canRaise: boolean;
  readonly canLower: boolean;
}

/**
 * The creation screen, as the product published it: where the flow stands, what the member being created
 * has chosen, what it may still choose, the rule the last illegal choice broke, and — once a party has
 * been accepted — the members of the party being played.
 */
interface CreationView {
  readonly active: boolean;
  readonly accepted: boolean;
  readonly hasDefault: boolean;
  readonly member: number;
  readonly members: number;
  readonly step: string;
  readonly pool: number;
  readonly refusalCode: string;
  readonly refusalMessage: string;
  readonly roster: readonly CreationMemberView[];
  readonly portraits: readonly CreationPortraitView[];
  readonly classes: readonly CreationClassView[];
  readonly skills: readonly CreationSkillView[];
  readonly attributes: readonly CreationAttributeView[];
  readonly party: readonly CreationPartyMemberView[];
}

interface SnapshotView {
  readonly composition: CompositionView;
  readonly session: SessionView;
  readonly world: WorldView;
  readonly movement: MovementView;
  readonly clock: ClockView;
  readonly party: PartyView;
  readonly creation: CreationView;
  readonly save: SaveView;
  readonly interaction: InteractionView;
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

/** The save state of a projection that carries none: a session this companion cannot read as saveable. */
const SAVE_NONE: SaveView = {
  available: false,
  resumed: false,
  slot: '',
  state: 'none',
  at: '',
  code: '',
  message: '',
};

/** The interaction of a session that holds no mechanism, or one this companion cannot read as interactive. */
const INTERACTION_NONE: InteractionView = {
  available: false,
  target: '',
  label: '',
  verb: '',
  state: '',
  distance: 0,
  reason: '',
  requires: [],
  outcome: 'none',
  code: '',
  message: '',
  residue: '',
};

/** The creation of a session that is neither creating a party nor playing one it accepted. */
const CREATION_NONE: CreationView = {
  active: false,
  accepted: false,
  hasDefault: false,
  member: 0,
  members: 0,
  step: '',
  pool: 0,
  refusalCode: '',
  refusalMessage: '',
  roster: [],
  portraits: [],
  classes: [],
  skills: [],
  attributes: [],
  party: [],
};

const STYLES = `
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
.crawler-session .crawler-use { margin: 0.3rem 0.4rem 0 0; }
.crawler-use-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-use-result[hidden] { display: none; }
.crawler-use-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-use-residue { margin: 0.15rem 0 0; color: #b8a888; font-size: 0.7rem; }
.crawler-use-residue[hidden] { display: none; }
.crawler-save-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-save-result[hidden] { display: none; }
.crawler-save-result[data-state='failed'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
`;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

/**
 * Reads a list the projection published, skipping entries that are not the shape this companion reads.
 * A list that is missing, or carries an entry that is not an object, is not a reason to reject the whole
 * projection: the screen shows the choices it can read and never invents the ones it cannot.
 */
function readList<T>(value: unknown, read: (entry: Record<string, unknown>) => T | null): T[] {
  if (!Array.isArray(value)) return [];
  const items: T[] = [];
  for (const entry of value) {
    if (!isRecord(entry)) continue;
    const item = read(entry);
    if (item !== null) items.push(item);
  }

  return items;
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

/**
 * Reads the save block, or the not-readable save. A block that is missing, or that is present but does not
 * carry the facts this panel renders, is not a reason to reject the whole projection: the session it
 * describes has a save this companion cannot read, and showing that is honest where refusing to render
 * everything else would hide the rest of the session behind it.
 */
function readSave(value: unknown): SaveView {
  if (!isRecord(value)) return SAVE_NONE;
  const { slot, state, at, code, message } = value;
  if (
    typeof slot !== 'string' ||
    typeof state !== 'string' ||
    typeof at !== 'string' ||
    typeof code !== 'string' ||
    typeof message !== 'string'
  ) {
    return SAVE_NONE;
  }

  return {
    available: value.available === true,
    resumed: value.resumed === true,
    slot,
    state,
    at,
    code,
    message,
  };
}

/**
 * Reads the interaction block, or the no-mechanism value. A block that is missing, or that is present but
 * does not carry the facts this panel renders, is not a reason to reject the whole projection: the session it
 * describes faces a world this companion cannot read, and saying so is honest where refusing to render
 * everything else would hide the rest of the session behind it.
 */
function readInteraction(value: unknown): InteractionView {
  if (!isRecord(value)) return INTERACTION_NONE;
  const { target, label, verb, state, reason, outcome, code, message, residue } = value;
  const distance = value.distance ?? 0;
  const requires = Array.isArray(value.requires)
    ? value.requires.filter((entry): entry is string => typeof entry === 'string')
    : [];
  if (
    typeof target !== 'string' ||
    typeof label !== 'string' ||
    typeof verb !== 'string' ||
    typeof state !== 'string' ||
    typeof distance !== 'number' ||
    typeof reason !== 'string' ||
    typeof outcome !== 'string' ||
    typeof code !== 'string' ||
    typeof message !== 'string' ||
    typeof residue !== 'string'
  ) {
    return INTERACTION_NONE;
  }

  return {
    available: value.available === true,
    target,
    label,
    verb,
    state,
    distance,
    reason,
    requires,
    outcome,
    code,
    message,
    residue,
  };
}

/**
 * Reads the creation block, or the empty creation. The block is published in every mode, so a session
 * that is doing neither — a resumed one — carries the empty shape and the screen shows no creation
 * section at all, which is a different reading from a party that is still being made.
 */
function readCreation(value: unknown): CreationView {
  if (!isRecord(value)) return CREATION_NONE;
  const { member, members, step, pool, refusalCode, refusalMessage } = value;
  if (
    typeof member !== 'number' ||
    typeof members !== 'number' ||
    typeof step !== 'string' ||
    typeof pool !== 'number' ||
    typeof refusalCode !== 'string' ||
    typeof refusalMessage !== 'string'
  ) {
    return CREATION_NONE;
  }

  return {
    active: value.active === true,
    accepted: value.accepted === true,
    hasDefault: value.hasDefault === true,
    member,
    members,
    step,
    pool,
    refusalCode,
    refusalMessage,
    roster: readList(value.roster, (entry) =>
      typeof entry.index === 'number' &&
      typeof entry.step === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.race === 'string' &&
      typeof entry.class === 'string' &&
      typeof entry.portrait === 'string' &&
      typeof entry.pool === 'number'
        ? {
            index: entry.index,
            step: entry.step,
            name: entry.name,
            race: entry.race,
            class: entry.class,
            portrait: entry.portrait,
            pool: entry.pool,
          }
        : null),
    portraits: readList(value.portraits, (entry) =>
      typeof entry.id === 'string' && typeof entry.name === 'string' && typeof entry.race === 'string'
        ? { id: entry.id, name: entry.name, race: entry.race, selected: entry.selected === true }
        : null),
    classes: readList(value.classes, (entry) =>
      typeof entry.id === 'string' && typeof entry.name === 'string'
        ? { id: entry.id, name: entry.name, selected: entry.selected === true }
        : null),
    skills: readList(value.skills, (entry) =>
      typeof entry.id === 'string' && typeof entry.name === 'string' && typeof entry.state === 'string'
        ? { id: entry.id, name: entry.name, state: entry.state }
        : null),
    attributes: readList(value.attributes, (entry) =>
      typeof entry.id === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.value === 'number' &&
      typeof entry.minimum === 'number' &&
      typeof entry.maximum === 'number'
        ? {
            id: entry.id,
            name: entry.name,
            value: entry.value,
            minimum: entry.minimum,
            maximum: entry.maximum,
            canRaise: entry.canRaise === true,
            canLower: entry.canLower === true,
          }
        : null),
    party: readList(value.party, (entry) =>
      typeof entry.index === 'number' &&
      typeof entry.name === 'string' &&
      typeof entry.race === 'string' &&
      typeof entry.class === 'string' &&
      typeof entry.portrait === 'string'
        ? {
            index: entry.index,
            name: entry.name,
            race: entry.race,
            class: entry.class,
            portrait: entry.portrait,
          }
        : null),
  };
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
  const creation = readCreation(value.creation);
  const save = readSave(value.save);
  const interaction = readInteraction(value.interaction);
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
    creation,
    save,
    interaction,
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
  panel.dataset.creation = 'none';
  panel.dataset.save = 'none';
  panel.dataset.interaction = 'none';
  panel.dataset.use = 'none';

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
    ['member', 'Member'],
    ['creationStep', 'Creation step'],
    ['pool', 'Pool'],
    ['save', 'Save'],
    ['start', 'Start'],
    ['facing', 'Facing'],
    ['requires', 'Requires'],
    ['use', 'Use'],
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

  // The save control: one button that asks for a save now, and the outcome of the last request. The
  // button is disabled while there is nothing to save — during creation and before the session runs — and
  // while the session has no store to write to; asking anyway is possible with the key, and the answer is
  // then shown here rather than swallowed.
  const saveButton = document.createElement('button');
  saveButton.type = 'button';
  saveButton.className = 'crawler-save';
  saveButton.disabled = true;
  saveButton.textContent = 'Save session';

  const saveResult = document.createElement('p');
  saveResult.className = 'crawler-save-result';
  saveResult.hidden = true;

  // The use control: one button that uses whatever the party faces, and the outcome of the last use. It is
  // disabled while the session holds no interaction — during creation, or in a session whose ruleset
  // answered none — and while nothing is focused, because a button that cannot work must not look like one
  // that can; the reason the reticle holds nothing is on the Facing row beside it.
  const useButton = document.createElement('button');
  useButton.type = 'button';
  useButton.className = 'crawler-use';
  useButton.disabled = true;
  useButton.textContent = 'Use';

  const useResult = document.createElement('p');
  useResult.className = 'crawler-use-result';
  useResult.hidden = true;
  const useResidue = document.createElement('p');
  useResidue.className = 'crawler-use-residue';
  useResidue.hidden = true;

  const hint = document.createElement('p');
  hint.className = 'crawler-hint';
  hint.textContent = 'Pause or resume with the button, or with the P key.';

  // The creation screen: the members as they stand, the choices creation offers the member being made,
  // the rule the last choice broke, and the party once one has been accepted. Every list is rebuilt from
  // the projection, so the screen holds nothing the product did not publish.
  const creation = document.createElement('section');
  creation.className = 'crawler-creation';
  creation.hidden = true;
  const stepHead = document.createElement('p');
  stepHead.className = 'crawler-step-head';
  const memberList = document.createElement('div');
  memberList.className = 'crawler-row';
  const portraitRow = document.createElement('div');
  portraitRow.className = 'crawler-row';
  const classRow = document.createElement('div');
  classRow.className = 'crawler-row';
  const skillRow = document.createElement('div');
  skillRow.className = 'crawler-row';
  const attributeRow = document.createElement('div');
  attributeRow.className = 'crawler-row';
  const nameRow = document.createElement('div');
  nameRow.className = 'crawler-name';
  const nameInput = document.createElement('input');
  nameInput.type = 'text';
  nameInput.placeholder = 'Name this character';
  const nameButton = document.createElement('button');
  nameButton.type = 'button';
  nameButton.textContent = 'Set name';
  nameRow.append(nameInput, nameButton);
  const flowRow = document.createElement('div');
  flowRow.className = 'crawler-actions';
  const advanceButton = document.createElement('button');
  advanceButton.type = 'button';
  advanceButton.textContent = 'Confirm step';
  const acceptButton = document.createElement('button');
  acceptButton.type = 'button';
  acceptButton.textContent = 'Accept party';
  flowRow.append(advanceButton, acceptButton);
  const refusal = document.createElement('p');
  refusal.className = 'crawler-refusal';
  refusal.hidden = true;
  const acceptedList = document.createElement('ul');
  acceptedList.className = 'crawler-accepted';
  creation.append(
    stepHead,
    memberList,
    portraitRow,
    classRow,
    skillRow,
    attributeRow,
    nameRow,
    flowRow,
    refusal,
    acceptedList,
  );

  // The creation screen comes before the long list of facts below: while a party is being made its
  // choices are what a player acts on, and a screen whose controls sat below twenty rows of values would
  // put them off the bottom of a short window.
  panel.append(
    title,
    ruleset,
    bundle,
    place,
    creation,
    details,
    action,
    saveButton,
    useButton,
    saveResult,
    useResult,
    useResidue,
    hint,
  );
  root.append(style, panel);

  let current = 'starting';
  const claim = (name: string, data: Record<string, unknown> = {}): void => {
    context.intents?.claim(UI_ACTION_INTENT, {
      kind: 'product-payload',
      contract: UI_ACTION_CONTRACT,
      data: { action: name, ...data },
    });
  };

  action.addEventListener('click', () => {
    if (current === 'running') claim(ACTION_PAUSE);
    else if (current === 'paused') claim(ACTION_RESUME);
  });

  saveButton.addEventListener('click', () => claim(ACTION_SAVE));
  useButton.addEventListener('click', () => claim(ACTION_USE));
  advanceButton.addEventListener('click', () => claim(ACTION_ADVANCE));
  acceptButton.addEventListener('click', () => claim(ACTION_ACCEPT));
  nameButton.addEventListener('click', () => claim(ACTION_SET_NAME, { name: nameInput.value }));

  /** The parameter a choice action carries, so the action name and its payload field stay in step. */
  const parameter = (name: string): string => {
    switch (name) {
      case ACTION_SELECT_PORTRAIT:
        return 'portrait';
      case ACTION_SELECT_CLASS:
        return 'class';
      case ACTION_CHOOSE_SKILL:
      case ACTION_REMOVE_SKILL:
        return 'skill';
      case ACTION_RAISE_ATTRIBUTE:
      case ACTION_LOWER_ATTRIBUTE:
        return 'attribute';
      default:
        return 'id';
    }
  };

  /** One labelled row of options, each a button that reports the choice it names. */
  const options = (
    label: string,
    entries: readonly {
      readonly id: string;
      readonly text: string;
      readonly selected?: boolean;
      readonly available?: boolean;
      readonly choice?: string;
    }[],
  ): HTMLElement => {
    const row = document.createElement('div');
    const heading = document.createElement('span');
    heading.className = 'crawler-row-label';
    heading.textContent = label;
    const list = document.createElement('div');
    list.className = 'crawler-options';
    for (const entry of entries) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = entry.text;
      button.dataset.id = entry.id;
      if (entry.selected !== undefined) button.dataset.selected = String(entry.selected);
      if (entry.available !== undefined) button.dataset.available = String(entry.available);
      // A choice the projection lists is offered whatever its state: the flow is what decides whether it
      // is legal, and its refusal is what the screen shows. Hiding a choice here would hide the rule.
      if (entry.choice !== undefined) {
        const field = parameter(entry.choice);
        button.addEventListener('click', () => claim(entry.choice!, { [field]: entry.id }));
      } else {
        button.disabled = true;
      }

      list.append(button);
    }

    row.append(heading, list);
    return row;
  };

  /** The members as they stand, each a button that moves creation onto it. */
  const members = (view: CreationView): HTMLElement => {
    const row = document.createElement('div');
    const heading = document.createElement('span');
    heading.className = 'crawler-row-label';
    heading.textContent = `Party — member ${view.member + 1} of ${view.members}`;
    const list = document.createElement('div');
    list.className = 'crawler-options';
    for (const member of view.roster) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = describe(member);
      button.dataset.member = String(member.index);
      button.dataset.step = member.step;
      button.dataset.selected = String(member.index === view.member);
      button.addEventListener('click', () => claim(ACTION_SELECT_MEMBER, { member: member.index }));
      list.append(button);
    }

    row.append(heading, list);
    return row;
  };

  /** The attributes with the two moves creation allows, each carrying what the race permits. */
  const attributes = (view: CreationView): HTMLElement => {
    const row = document.createElement('div');
    const heading = document.createElement('span');
    heading.className = 'crawler-row-label';
    heading.textContent = `Attributes — ${view.pool} point${view.pool === 1 ? '' : 's'} left`;
    row.append(heading);
    for (const attribute of view.attributes) {
      const line = document.createElement('div');
      line.className = 'crawler-attribute';
      const value = document.createElement('span');
      value.textContent = `${attribute.name} ${attribute.value} (${attribute.minimum}–${attribute.maximum})`;
      const lower = document.createElement('button');
      lower.type = 'button';
      lower.textContent = '−';
      lower.dataset.attribute = attribute.id;
      lower.dataset.canLower = String(attribute.canLower);
      lower.addEventListener('click', () => claim(ACTION_LOWER_ATTRIBUTE, { attribute: attribute.id }));
      const raise = document.createElement('button');
      raise.type = 'button';
      raise.textContent = '+';
      raise.dataset.attribute = attribute.id;
      raise.dataset.canRaise = String(attribute.canRaise);
      raise.addEventListener('click', () => claim(ACTION_RAISE_ATTRIBUTE, { attribute: attribute.id }));
      line.append(value, lower, raise);
      row.append(line);
    }

    return row;
  };

  /**
   * One member of the party as a person reads it, from the values the flow published and no others. The
   * race is left to the chosen portrait and to the accepted list: it is the portrait that decided it, and
   * a member button that named it too would wrap onto a line of its own in a short window.
   */
  const describe = (member: CreationMemberView): string => {
    const named = member.name === '' ? 'unnamed' : member.name;
    return `${member.index + 1}. ${named} · ${member.class === '' ? 'no class' : member.class} · ${member.step}`;
  };

  /**
   * Renders the creation screen. The choices are rebuilt only when what they carry changed, because the
   * product publishes an update for every admitted step and rebuilding a screen sixty times a second
   * would fight the pointer for no reason: what changed is what is redrawn.
   */
  let renderedChoices = '';
  const renderCreation = (view: CreationView, resumed: boolean): void => {
    // A resumed session plays a party it did not create here, and says so rather than claiming a creation
    // that never happened: the roster below it is the same either way.
    panel.dataset.creation = view.active ? 'active' : view.accepted ? (resumed ? 'resumed' : 'accepted') : 'none';
    creation.hidden = !view.active && !view.accepted;
    refusal.hidden = view.refusalMessage === '';
    refusal.dataset.code = view.refusalCode;
    refusal.textContent = view.refusalMessage;
    // What changed is what is redrawn: the product publishes an update for every admitted step, and
    // rebuilding a screen sixty times a second would fight the pointer for no reason. The refusal and the
    // mode are written every time because they are the two things a player must not miss.
    const signature = JSON.stringify(view);
    if (signature === renderedChoices) return;
    renderedChoices = signature;

    if (!view.active) {
      // The party a session accepted is shown from the party itself, so the screen's list is the party
      // being played rather than the draft that described it.
      acceptedList.replaceChildren(
        ...view.party.map((member) => {
          const item = document.createElement('li');
          item.textContent = `${member.index + 1}. ${member.name} · ${member.race} · ${member.class} · ${member.portrait}`;
          return item;
        }),
      );
      stepHead.textContent = view.accepted ? (resumed ? 'Party resumed' : 'Party accepted') : '';
      memberList.replaceChildren();
      portraitRow.replaceChildren();
      classRow.replaceChildren();
      skillRow.replaceChildren();
      attributeRow.replaceChildren();
      nameRow.hidden = true;
      flowRow.hidden = true;
      return;
    }

    acceptedList.replaceChildren();
    nameRow.hidden = false;
    flowRow.hidden = false;
    stepHead.textContent = view.roster.every((member) => member.step === 'complete')
      ? 'Every member is finished; accept the party, or reopen one to change it.'
      : `Creating member ${view.member + 1} of ${view.members} · ${view.step}`;
    memberList.replaceChildren(members(view));
    portraitRow.replaceChildren(
      options(
        'Portrait — decides the race',
        view.portraits.map((portrait) => ({
          id: portrait.id,
          text: portrait.name,
          selected: portrait.selected,
          choice: ACTION_SELECT_PORTRAIT,
        })),
      ),
    );
    classRow.replaceChildren(
      options(
        'Class — decides the skills',
        view.classes.map((option) => ({
          id: option.id,
          text: option.name,
          selected: option.selected,
          choice: ACTION_SELECT_CLASS,
        })),
      ),
    );
    skillRow.replaceChildren(
      options(
        'Skills — the fixed ones, the chosen ones, and the rest',
        view.skills.map((skill) => ({
          id: skill.id,
          text: skill.state === 'fixed' ? `${skill.name} (fixed)` : skill.name,
          selected: skill.state === 'chosen',
          available: skill.state === 'available',
          choice:
            skill.state === 'chosen'
              ? ACTION_REMOVE_SKILL
              : skill.state === 'available'
                ? ACTION_CHOOSE_SKILL
                : undefined,
        })),
      ),
    );
    attributeRow.replaceChildren(attributes(view));
  };

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
    const { clock, party, creation: creating, save } = snapshot;
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
    // The save's own facts: whether this session can save at all, whether it came from the slot, and what
    // happened the last time the player asked. Nothing is derived here — the outcome word, the moment, and
    // the reason are the product's, and the panel prints them unchanged.
    panel.dataset.save = save.state;
    rows.save.textContent =
      !save.available
        ? 'unavailable'
        : save.state === 'saved'
          ? `${save.at} · ${save.slot}`
          : save.state === 'failed'
            ? 'failed'
            : '—';
    rows.start.textContent = save.resumed ? 'resumed' : 'fresh';
    saveResult.hidden = save.message === '';
    saveResult.dataset.state = save.state;
    saveResult.dataset.code = save.code;
    saveResult.textContent = save.message;
    // The interaction's own facts: what the party faces, what it requires, and what the last use did. Every
    // word here is the product's — the reason the reticle holds nothing, the requirement a lock states, and
    // the sentence a refusal answered with — and the panel spells none of them itself.
    const { interaction } = snapshot;
    panel.dataset.interaction = interaction.available ? interaction.reason : 'none';
    panel.dataset.use = interaction.outcome;
    rows.facing.textContent =
      !interaction.available
        ? '—'
        : interaction.label === ''
          ? interaction.reason
          : `${interaction.label} · ${interaction.verb}${interaction.state === '' ? '' : ` · ${interaction.state}`} · ${interaction.distance.toFixed(0)}`;
    rows.requires.textContent = interaction.requires.length === 0 ? '—' : interaction.requires.join(', ');
    rows.use.textContent = interaction.outcome === 'none' ? '—' : interaction.outcome;
    useResult.hidden = interaction.message === '';
    useResult.dataset.outcome = interaction.outcome;
    useResult.dataset.code = interaction.code;
    useResult.textContent = interaction.message;
    useResidue.hidden = interaction.residue === '';
    useResidue.textContent = interaction.residue;
    // Creation's own facts: which member is being made, where it stands, and what the pool still holds.
    rows.member.textContent = creating.active || creating.accepted ? `${creating.member + 1} / ${creating.members}` : '—';
    rows.creationStep.textContent = creating.step === '' ? '—' : creating.step;
    rows.pool.textContent = creating.active ? String(creating.pool) : '—';
    renderCreation(creating, save.resumed);
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
    } else if (current === 'creating') {
      action.disabled = true;
      action.textContent = 'Creating a party…';
    } else {
      action.disabled = true;
      action.textContent = current === 'stopped' ? 'Session stopped' : 'Starting…';
    }

    // The save control follows the session rather than the screen: there is nothing to save while a party
    // is being made, and a session with no store says so on its Save row instead of offering a button that
    // cannot work.
    saveButton.disabled = !save.available || (current !== 'running' && current !== 'paused');
    // The use control follows what is faced rather than the mode: a use is an instant, so a held session can
    // still use what it is looking at, and a session with nothing in front of the party offers the button
    // disabled with the reason on the Facing row.
    useButton.disabled =
      !interaction.available || interaction.label === '' || current === 'creating' || current === 'stopped';
    const pauseHint = 'Pause or resume with the button, or with the P key.';
    const saveHint = save.available ? ' Save with the button, or with the F key.' : '';
    const useHint = interaction.available ? ' Use with the button, or with the G key.' : '';
    hint.textContent =
      current === 'creating'
        ? 'Choose a portrait, a class, a name, attributes, and skills. Enter confirms the step you are on; Space accepts a finished party.'
        : `${pauseHint}${saveHint}${useHint}`;
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
