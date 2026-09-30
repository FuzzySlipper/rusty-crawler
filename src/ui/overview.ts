/**
 * The blocks the panel's own rows are read from: what the session is, where the party is, what its last step did,
 * where the clock stands, what the party holds, how the session stands with its save slot, what the party faces,
 * and the stand-alone controls with whether each would be taken now and the key it is bound to.
 */

import type { Fields } from './reader.js';

export interface CompositionView {
  readonly ruleset: string;
  readonly title: string;
  /** The game bundle the product started from, empty when no bundle was selected. */
  readonly bundle: string;
  readonly contentPacks: number;
}

export interface SessionView {
  readonly mode: string;
  readonly simulationSeconds: number;
  readonly admittedSteps: number;
}

/** The world the party is in. Empty when the session has no world loaded. */
export interface WorldView {
  readonly place: string;
  readonly name: string;
  readonly kind: string;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly yaw: number;
  readonly visited: number;
  readonly places: number;
  /** Whether the place's own buildings are open at the hour this projection was built; a clock read. */
  readonly open: boolean;
  /** The hours the place keeps, empty when content clocks nothing in it. */
  readonly hours: string;
  /** When those hours next change, as a point on the game calendar, empty when nothing does. */
  readonly nextChange: string;
}

/**
 * What the party's last admitted movement step did. `motion` is `none` when the session has no movement facts at
 * all — no world, or no step yet — and the remaining fields then describe no step rather than a quiet one.
 */
export interface MovementView {
  /** The state the last step left the party in: `none`, `grounded`, or `airborne`. */
  readonly motion: string;
  /** What refused the step: a reason word, or `none` when nothing did. */
  readonly blocked: string;
  /** How high an accepted step-up raised the party; zero when the engine accepted none. */
  readonly stepRise: number;
  readonly fallDistance: number;
  readonly fallDamage: number;
}

/** Where the game clock stands. `present` is false when the session's ruleset composed no clock. */
export interface ClockView {
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
 * One thing the party has accomplished: the game's own words for a record the party carries, and the family the
 * game counts it in. What a record means, and whether it is an accomplishment at all, is the ruleset's reading.
 */
export interface AwardView {
  /** The record's own identity, which is the state the accomplishment is carried as. */
  readonly id: string;
  /** What family the game counts it in, as the game words it: a promotion, an errand, a membership, a deed. */
  readonly kind: string;
  readonly label: string;
  /** What is true of it beyond its name, empty when nothing is. */
  readonly detail: string;
}

/** The party's own accounts and standing. `present` is false when the session holds no party. */
export interface PartyView {
  readonly present: boolean;
  readonly members: number;
  /** How many items lie in the party's one shared pack, which is where everything the party takes goes. */
  readonly pack: number;
  readonly coins: number;
  readonly provisions: number;
  /** The unit the provisions are stated in, so the number is never shown without its measure. */
  readonly unit: string;
  readonly reputation: number;
  readonly fame: number;
  /** Whether this game reads a standing and the party's accomplishments at all. */
  readonly standingRead: boolean;
  /** What the game calls the band the party's standing falls in, empty when its ruleset names no bands. */
  readonly standing: string;
  /** What that band means for how the party is treated, empty when nothing is read. */
  readonly standingDetail: string;
  readonly awards: readonly AwardView[];
  /** The conditions acting on the party, empty when none act. */
  readonly conditions: string;
  readonly hitPoints: number;
  readonly hitPointsMax: number;
  readonly spellPoints: number;
  readonly spellPointsMax: number;
}

/**
 * How the session stands with its save slot. `state` is `none` until a save is asked for, `saved` when one landed,
 * and `failed` when one did not — and `message` then says what went wrong.
 */
export interface SaveView {
  /** Whether the session has a save store at all. */
  readonly available: boolean;
  /** Whether this session was composed from the save in its slot rather than started fresh. */
  readonly resumed: boolean;
  readonly slot: string;
  /** `none`, `saved`, or `failed`. */
  readonly state: string;
  /** The game date and time the last save landed, empty when none has. */
  readonly at: string;
  readonly code: string;
  readonly message: string;
}

/**
 * What the party is facing and what using it did. `reason` says why the reticle holds or refuses what it does, and
 * the last use keeps its own outcome, code, sentence, and residue.
 */
export interface InteractionView {
  readonly available: boolean;
  /** The focused target's kind, empty when nothing is focused. */
  readonly target: string;
  readonly label: string;
  /** The use that applies to it, empty when nothing is focused. */
  readonly verb: string;
  /** What the party has already done to it. */
  readonly state: string;
  readonly distance: number;
  readonly reason: string;
  /** What the focused target requires, in the order the checks happen. */
  readonly requires: readonly string[];
  /** How many bodies lie in the place the party stands in. */
  readonly bodies: number;
  /** `none`, `applied`, or `refused`. */
  readonly outcome: string;
  readonly code: string;
  readonly message: string;
  /** What the last use could not deliver. */
  readonly residue: string;
}

/** One stand-alone control: the action it sends, whether the product would take it now, and its key. */
export interface ControlView {
  /** The action a press asks for, as the product declared it; empty when there is nothing to ask. */
  readonly action: string;
  /** Whether the product would take a press now, which is exactly when the panel offers the control. */
  readonly enabled: boolean;
  /** The key the host bound the control to, as a person reads it; empty when none. */
  readonly key: string;
}

/** Every stand-alone control the panel offers, each as the product published it. */
export interface ControlsView {
  readonly pause: ControlView;
  readonly save: ControlView;
  readonly use: ControlView;
  readonly attack: ControlView;
  readonly turnBased: ControlView;
  readonly turnSkip: ControlView;
  readonly turnWait: ControlView;
  readonly rest: ControlView;
  readonly camp: ControlView;
  readonly waitDawn: ControlView;
  readonly waitHour: ControlView;
  readonly waitFiveMinutes: ControlView;
  readonly serviceLeave: ControlView;
  readonly conversationLeave: ControlView;
  readonly creationAdvance: ControlView;
  readonly creationAccept: ControlView;
}

export function readComposition(f: Fields): CompositionView {
  return {
    ruleset: f.text('ruleset'),
    title: f.text('title'),
    bundle: f.text('bundle'),
    contentPacks: f.number('contentPacks'),
  };
}

export function readSession(f: Fields): SessionView {
  return {
    mode: f.text('mode', 'starting'),
    simulationSeconds: f.number('simulationSeconds'),
    admittedSteps: f.number('admittedSteps'),
  };
}

export function readWorld(f: Fields): WorldView {
  return {
    place: f.text('place'),
    name: f.text('name'),
    kind: f.text('kind'),
    x: f.number('x'),
    y: f.number('y'),
    z: f.number('z'),
    yaw: f.number('yaw'),
    visited: f.number('visited'),
    places: f.number('places'),
    open: f.flag('open'),
    hours: f.text('hours'),
    nextChange: f.text('nextChange'),
  };
}

export function readMovement(f: Fields): MovementView {
  return {
    motion: f.text('motion', 'none'),
    blocked: f.text('blocked', 'none'),
    stepRise: f.number('stepRise'),
    fallDistance: f.number('fallDistance'),
    fallDamage: f.number('fallDamage'),
  };
}

export function readClock(f: Fields): ClockView {
  return {
    present: f.flag('present'),
    date: f.text('date'),
    time: f.text('time'),
    daylight: f.text('daylight'),
    elapsedDays: f.number('elapsedDays'),
  };
}

export function readParty(f: Fields): PartyView {
  return {
    present: f.flag('present'),
    members: f.number('members'),
    pack: f.number('pack'),
    coins: f.number('coins'),
    provisions: f.number('provisions'),
    unit: f.text('unit'),
    reputation: f.number('reputation'),
    fame: f.number('fame'),
    standingRead: f.flag('standingRead'),
    standing: f.text('standing'),
    standingDetail: f.text('standingDetail'),
    awards: f.list('awards', (award) => ({
      id: award.text('id'),
      kind: award.text('kind'),
      label: award.text('label'),
      detail: award.text('detail'),
    })),
    conditions: f.text('conditions'),
    hitPoints: f.number('hitPoints'),
    hitPointsMax: f.number('hitPointsMax'),
    spellPoints: f.number('spellPoints'),
    spellPointsMax: f.number('spellPointsMax'),
  };
}

export function readSave(f: Fields): SaveView {
  return {
    available: f.flag('available'),
    resumed: f.flag('resumed'),
    slot: f.text('slot'),
    state: f.text('state', 'none'),
    at: f.text('at'),
    code: f.text('code'),
    message: f.text('message'),
  };
}

export function readInteraction(f: Fields): InteractionView {
  return {
    available: f.flag('available'),
    target: f.text('target'),
    label: f.text('label'),
    verb: f.text('verb'),
    state: f.text('state'),
    distance: f.number('distance'),
    reason: f.text('reason'),
    requires: f.words('requires'),
    bodies: f.number('bodies'),
    outcome: f.text('outcome', 'none'),
    code: f.text('code'),
    message: f.text('message'),
    residue: f.text('residue'),
  };
}

function readControl(f: Fields): ControlView {
  return { action: f.text('action'), enabled: f.flag('enabled'), key: f.text('key') };
}

export function readControls(f: Fields): ControlsView {
  return {
    pause: readControl(f.object('pause')),
    save: readControl(f.object('save')),
    use: readControl(f.object('use')),
    attack: readControl(f.object('attack')),
    turnBased: readControl(f.object('turnBased')),
    turnSkip: readControl(f.object('turnSkip')),
    turnWait: readControl(f.object('turnWait')),
    rest: readControl(f.object('rest')),
    camp: readControl(f.object('camp')),
    waitDawn: readControl(f.object('waitDawn')),
    waitHour: readControl(f.object('waitHour')),
    waitFiveMinutes: readControl(f.object('waitFiveMinutes')),
    serviceLeave: readControl(f.object('serviceLeave')),
    conversationLeave: readControl(f.object('conversationLeave')),
    creationAdvance: readControl(f.object('creationAdvance')),
    creationAccept: readControl(f.object('creationAccept')),
  };
}
