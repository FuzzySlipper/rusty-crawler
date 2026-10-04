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
  /** Which start the party took: `creation`, `scenario` (the party the scenario fixes), or `resumed`. */
  readonly partyStart: string;
  /** The requested bundle whose packs are absent, and how an operator produces them; null when the session plays. */
  readonly setup: SetupView | null;
}

/** A requested bundle the product could not play because the packs it names are not in the content root. */
export interface SetupView {
  readonly bundle: string;
  readonly missingPacks: readonly string[];
  readonly guidance: string;
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
  /** The state the last step left the party in: `none`, `grounded`, `airborne`, or `flying`. */
  readonly motion: string;
  /** What refused the step: a reason word, or `none` when nothing did. */
  readonly blocked: string;
  /** How high an accepted step-up raised the party; zero when the engine accepted none. */
  readonly stepRise: number;
  readonly fallDistance: number;
  readonly fallDamage: number;
  /** What the ground under the party does to it now. */
  readonly footing: FootingView;
}

/** One effect that spares the party, or one member, the harm of the ground it stands on. */
export interface FootingShelterView {
  readonly effect: string;
  /** What the game calls it. */
  readonly name: string;
  readonly member: string;
  readonly memberName: string;
  /** Whether it spares the whole party; otherwise its carrier alone. */
  readonly everybody: boolean;
}

/**
 * The ground under the party, as the product read it: `ground` is empty while the party stands on nothing, and the
 * harm fields describe no harm while `harmful` is false.
 */
export interface FootingView {
  /** The ground's name as content states it, or empty. */
  readonly ground: string;
  readonly harmful: boolean;
  /** How often the ground harms the party, in game seconds. */
  readonly every: number;
  /** How many game seconds before the next harm lands. */
  readonly nextHarmIn: number;
  readonly shelters: readonly FootingShelterView[];
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

/** What the party owes on one account. */
export interface DebtView {
  /** The game's word for the account. */
  readonly account: string;
  /** What is owed on it. */
  readonly coins: number;
}

/** One member as the adventure frame shows them: who they are, their face, their pools and whether they are selected. */
export interface RosterMemberView {
  /** The member's identity, which selecting them and every per-member block names them by. */
  readonly member: string;
  readonly name: string;
  /** The class the member plays, as content names it. */
  readonly class: string;
  readonly portrait: string;
  /** Where the Engine serves the portrait's image, empty when none is drawn. */
  readonly portraitImage: string;
  readonly hitPoints: number;
  readonly hitPointsMax: number;
  readonly spellPoints: number;
  readonly spellPointsMax: number;
  /** How full the member's hit points are, a whole percentage the product worked out, which a bar is drawn at. */
  readonly hitPointsPercent: number;
  readonly spellPointsPercent: number;
  /** The conditions acting on the member, in the product's own words, empty when none act. */
  readonly conditions: string;
  readonly selected: boolean;
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
  /** What the party owes, account by account; empty when it owes nothing. */
  readonly debts: readonly DebtView[];
  readonly followers: readonly {
    readonly id: string;
    readonly name: string;
    readonly portrait: string;
    readonly kind: string;
    readonly canTalk: boolean;
    readonly talkAction: string;
  }[];
  /** The conditions acting on the party, empty when none act. */
  readonly conditions: string;
  readonly hitPoints: number;
  readonly hitPointsMax: number;
  readonly spellPoints: number;
  readonly spellPointsMax: number;
  /** The members in roster order. */
  readonly roster: readonly RosterMemberView[];
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
  /** Whether live state changed after the slot's last successful save. */
  readonly dirty: boolean;
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
  /** The ruleset's disposition for a being, empty for doors, containers and other objects. */
  readonly disposition: string;
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
  readonly nextMember: ControlView;
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
    partyStart: f.text('partyStart'),
    setup: f.nullable('setup', (entry) => ({
      bundle: entry.text('bundle'),
      missingPacks: entry.words('missingPacks'),
      guidance: entry.text('guidance'),
    })),
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
    footing: readFooting(f.object('footing')),
  };
}

function readFooting(f: Fields): FootingView {
  return {
    ground: f.text('ground'),
    harmful: f.flag('harmful'),
    every: f.number('every'),
    nextHarmIn: f.number('nextHarmIn'),
    shelters: f.list('shelters', (entry) => ({
      effect: entry.text('effect'),
      name: entry.text('name'),
      member: entry.text('member'),
      memberName: entry.text('memberName'),
      everybody: entry.flag('everybody'),
    })),
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
    debts: f.list('debts', (debt) => ({ account: debt.text('account'), coins: debt.number('coins') })),
    followers: f.list('followers', (follower) => ({
      id: follower.text('id'), name: follower.text('name'), portrait: follower.text('portrait'),
      kind: follower.text('kind'), canTalk: follower.flag('canTalk'), talkAction: follower.text('talkAction'),
    })),
    conditions: f.text('conditions'),
    hitPoints: f.number('hitPoints'),
    hitPointsMax: f.number('hitPointsMax'),
    spellPoints: f.number('spellPoints'),
    spellPointsMax: f.number('spellPointsMax'),
    roster: f.list('roster', (member) => ({
      member: member.text('member'),
      name: member.text('name'),
      class: member.text('class'),
      portrait: member.text('portrait'),
      portraitImage: member.text('portraitImage'),
      hitPoints: member.number('hitPoints'),
      hitPointsMax: member.number('hitPointsMax'),
      spellPoints: member.number('spellPoints'),
      spellPointsMax: member.number('spellPointsMax'),
      hitPointsPercent: member.number('hitPointsPercent'),
      spellPointsPercent: member.number('spellPointsPercent'),
      conditions: member.text('conditions'),
      selected: member.flag('selected'),
    })),
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
    dirty: f.flag('dirty'),
  };
}

export function readInteraction(f: Fields): InteractionView {
  return {
    available: f.flag('available'),
    target: f.text('target'),
    label: f.text('label'),
    disposition: f.text('disposition'),
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
    nextMember: readControl(f.object('nextMember')),
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
