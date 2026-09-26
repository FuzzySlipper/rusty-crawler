/**
 * The DOM companion's contract: it renders what the projection carries, reports the two session
 * actions it offers, owns no state, and starts no timer. The product half of the same round trip is
 * covered by tests/PartyRpg.Kit.Tests/SessionInputRouterTests.cs.
 */
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';
import { JSDOM } from 'jsdom';

import { mountProductUi } from '../../src/ui/main.ts';

const CONTRACT = 'crawler.ui.snapshot.v1';
const ACTION_INTENT = 'crawler.ui';
const ACTION_CONTRACT = 'crawler.ui.action.v1';

function world(overrides = {}) {
  return {
    place: '1',
    name: 'Emerald Island',
    kind: 'region',
    x: 1234,
    y: 5678,
    z: 0,
    yaw: 512,
    visited: 1,
    places: 76,
    open: true,
    hours: '',
    nextChange: '',
    ...overrides,
  };
}

/** The movement block as the product publishes it: `motion` is `none` until the party has stepped. */
function movement(overrides = {}) {
  return {
    motion: 'grounded',
    blocked: 'none',
    stepRise: 0,
    fallDistance: 0,
    fallDamage: 0,
    ...overrides,
  };
}

/** The clock block as the product publishes it when the ruleset composed a clock. */
function clock(overrides = {}) {
  return {
    present: true,
    date: '1168-01-01',
    time: '09:00',
    daylight: 'day',
    elapsedDays: 0,
    ...overrides,
  };
}

/** The party block as the product publishes it when content supplied a party. */
function party(overrides = {}) {
  return {
    present: true,
    members: 4,
    pack: 0,
    coins: 200,
    provisions: 6,
    unit: 'portions',
    reputation: 0,
    fame: 0,
    conditions: '',
    hitPoints: 40,
    hitPointsMax: 40,
    spellPoints: 10,
    spellPointsMax: 10,
    ...overrides,
  };
}

/**
 * The save block as the product publishes it. `state` is `none` until a save is asked for, `saved` when one
 * landed, and `failed` when one did not; `message` is then what the player is told.
 */
function save(overrides = {}) {
  return {
    available: true,
    resumed: false,
    slot: 'session',
    state: 'none',
    at: '',
    code: '',
    message: '',
    ...overrides,
  };
}

/**
 * The interaction block as the product publishes it: what the party faces, what it requires, and what the
 * last use did. `available` is false when the session holds no mechanism at all; `outcome` is `none` until
 * the party has used something.
 */
function interaction(overrides = {}) {
  return {
    available: true,
    target: '',
    label: '',
    verb: '',
    state: '',
    distance: 0,
    reason: 'no-candidate',
    requires: [],
    bodies: 0,
    outcome: 'none',
    code: '',
    message: '',
    residue: '',
    ...overrides,
  };
}

/**
 * The service block as the product publishes it. `available` is false when the session holds no service
 * mechanism at all; `open` says whether the party stands at a counter; `state` says whether that counter is
 * serving. Every list is what the product published, prices included.
 */
function service(overrides = {}) {
  return {
    available: true,
    open: false,
    id: '',
    kind: '',
    name: '',
    proprietor: '',
    state: '',
    hours: '',
    operations: [],
    memberships: [],
    stock: [],
    lessons: [],
    sales: [],
    members: [],
    action: '',
    outcome: 'none',
    code: '',
    message: '',
    paid: 0,
    earned: 0,
    coins: 0,
    ...overrides,
  };
}

/** A weapon shop the party has walked into, with a shelf, a lesson, and one of the party's own items. */
function openShop(overrides = {}) {
  return service({
    open: true,
    id: 'sword-and-shield',
    kind: 'Weapon Shop',
    name: 'The Sword and Shield',
    proprietor: 'Bertram',
    state: 'open',
    hours: '06:00–18:00',
    operations: ['buy', 'sell', 'identify', 'repair', 'teach'],
    memberships: [],
    stock: [
      { lot: 'stock:sword', item: 'sword', name: 'A fine sword', count: 2, price: 110, sale: false },
      { lot: 'sold:7', item: 'dagger', name: 'dagger', count: 1, price: 22, sale: true },
      { lot: 'stock:potion', item: 'potion', name: 'potion', count: 0, price: 30, sale: false },
    ],
    // Two lessons of one skill: the first rung every counter that teaches a trade sells, and the mastery the
    // guild's own depth reaches. The rung is part of what identifies a lesson, so both rows are real.
    lessons: [
      { kind: 'skill', subject: 'Sword', name: 'Sword', amount: 1, price: 25, tier: 1 },
      { kind: 'skill', subject: 'Sword', name: 'Sword, expert', amount: 1, price: 1000, tier: 2 },
    ],
    sales: [{ item: '3', definition: 'shield', name: 'shield', price: 12, damage: 3, identified: false }],
    members: [
      { index: 0, name: 'Roderick' },
      { index: 1, name: 'Nyx' },
    ],
    coins: 200,
    ...overrides,
  });
}

/** A closed door the party faces, as the product publishes it while it is usable. */
function facedDoor(overrides = {}) {
  return interaction({
    target: 'door',
    label: 'A door',
    verb: 'open',
    state: 'closed',
    distance: 128,
    reason: 'ready',
    ...overrides,
  });
}

/**
 * The rest block as the product publishes it. `available` is false when the session holds no rest mechanism
 * at all; `outcome` is `none` until the party has stopped; `interrupted` says a night was broken; and the
 * fatigue facts are the clock's own deadline rather than a count kept by the screen.
 */
function rest(overrides = {}) {
  return {
    available: true,
    kind: '',
    outcome: 'none',
    code: '',
    message: '',
    from: '1168-01-01 09:00',
    to: '1168-01-01 09:00',
    elapsedSeconds: 0,
    charged: 0,
    covered: 0,
    unit: '',
    interrupted: false,
    recovered: false,
    restored: 0,
    cleared: '',
    shortage: '',
    tired: false,
    fatigueDue: '1168-01-02 09:00',
    fatigueLanded: 0,
    ...overrides,
  };
}

/**
 * The fight block as the product publishes it. `available` is false when the session holds no fight at all;
 * `engaged` says whether anything is hostile; each member and enemy carries the readiness the product
 * published, which is the only thing the panel shows.
 */
function combat(overrides = {}) {
  return {
    available: true,
    engaged: false,
    opposition: 0,
    ready: 2,
    members: [
      {
        id: 'member:1', name: 'Roderick', ready: true, recoverySeconds: 0, distance: 0,
        hitPoints: 40, hitPointsMax: 40, conditions: '', down: false, activity: '',
      },
      {
        id: 'member:2', name: 'Aelina', ready: true, recoverySeconds: 0, distance: 0,
        hitPoints: 24, hitPointsMax: 24, conditions: '', down: false, activity: '',
      },
    ],
    enemies: [],
    actor: '',
    kind: '',
    target: '',
    outcome: 'none',
    code: '',
    message: '',
    recoverySeconds: 0,
    resolved: false,
    hit: false,
    chance: 0,
    damageRolled: 0,
    damage: 0,
    damageKind: '',
    resistance: '',
    condition: '',
    targetDown: false,
    byParty: true,
    pacing: 'realtime',
    turn: round(),
    ...overrides,
  };
}

/** The round of a fight that is being played in real time: no phase, no turn, and an empty order. */
function round(overrides = {}) {
  return {
    phase: 'none',
    round: 0,
    actor: '',
    actorName: '',
    playerTurn: false,
    dueSeconds: 0,
    roundSeconds: 0,
    elapsedSeconds: 0,
    movementSeconds: 0,
    last: '',
    order: [],
    ...overrides,
  };
}

/** One actor's place in a paced round's order, as the product publishes it. */
function ordered(overrides = {}) {
  return {
    id: 'member:1',
    name: 'Roderick',
    side: 'party',
    remainingSeconds: 0,
    ready: true,
    canAct: true,
    waiting: false,
    current: false,
    ...overrides,
  };
}

/**
 * A fight being paced turn-based: the first member holds the turn, the creature owes a second, and the order
 * is the fight's own reading of the same recovery that paces real time.
 */
function paced(overrides = {}) {
  return combat({
    engaged: true,
    opposition: 1,
    ready: 1,
    pacing: 'turnbased',
    turn: round({
      phase: 'action',
      round: 2,
      actor: 'member:1',
      actorName: 'Roderick',
      playerTurn: true,
      dueSeconds: 0,
      roundSeconds: 23.438,
      elapsedSeconds: 4,
      last: 'skip',
      order: [
        ordered({ current: true }),
        ordered({ id: 'member:2', name: 'Aelina', side: 'party', remainingSeconds: 1.5, ready: false }),
        ordered({ id: 'actor:1', name: 'A beast', side: 'opposition', remainingSeconds: 1, ready: false }),
      ],
    }),
    members: [
      {
        id: 'member:1', name: 'Roderick', ready: true, recoverySeconds: 0, distance: 0,
        hitPoints: 27, hitPointsMax: 40, conditions: 'Poison Weak', down: false, activity: '',
      },
      {
        id: 'member:2', name: 'Aelina', ready: false, recoverySeconds: 1.5, distance: 0,
        hitPoints: 24, hitPointsMax: 24, conditions: '', down: false, activity: '',
      },
    ],
    enemies: [
      {
        id: 'actor:1', name: 'A beast', ready: false, recoverySeconds: 1, distance: 100,
        hitPoints: 120, hitPointsMax: 200, conditions: '', down: false, activity: 'attacking',
      },
    ],
    actor: 'A beast',
    kind: 'melee',
    target: 'Roderick',
    outcome: 'applied',
    message: 'A beast attacks Roderick (melee: attack1) and must recover 23438ms of game time.',
    resolved: true,
    hit: true,
    damage: 13,
    byParty: false,
    ...overrides,
  });
}

/** A fight in progress: one creature engaged with the party, which has just swung at it. */
function fighting(overrides = {}) {
  return combat({
    engaged: true,
    opposition: 1,
    ready: 0,
    members: [
      {
        id: 'member:1', name: 'Roderick', ready: false, recoverySeconds: 22.969, distance: 0,
        hitPoints: 40, hitPointsMax: 40, conditions: '', down: false,
      },
      {
        id: 'member:2', name: 'Aelina', ready: false, recoverySeconds: 22.266, distance: 0,
        hitPoints: 24, hitPointsMax: 24, conditions: '', down: false,
      },
    ],
    enemies: [
      {
        id: 'actor:1', name: 'A beast', ready: true, recoverySeconds: 0, distance: 100,
        hitPoints: 14, hitPointsMax: 40, conditions: '', down: false, activity: 'attacking',
      },
    ],
    actor: 'Roderick',
    kind: 'melee',
    target: 'A beast',
    outcome: 'applied',
    message: 'Roderick attacks A beast (melee) and must recover 22969ms of game time. Roderick hits A beast (melee): 5 Phys damage landed; A beast is at 14/40.',
    recoverySeconds: 22.969,
    resolved: true,
    hit: true,
    chance: 4595,
    damageRolled: 5,
    damage: 5,
    damageKind: 'Phys',
    resistance: '0',
    condition: '',
    targetDown: false,
    byParty: true,
    ...overrides,
  });
}

/** The same fight a moment later: the creature's own blow, which the party took. */
function struck(overrides = {}) {
  return fighting({
    actor: 'A beast',
    target: 'Roderick',
    message: 'A beast attacks Roderick (melee) and must recover 23438ms of game time. A beast hits Roderick (melee): 7 Phys damage landed; Roderick is at 33/40.',
    byParty: false,
    ...overrides,
  });
}

/**
 * The conversation block as the product publishes it. `available` is false when the session holds no
 * conversation mechanism at all; `open` says whether anybody is being spoken with; `topics` is what the
 * state offers and `withheld` what it does not, each with the reason it is not.
 */
function conversation(overrides = {}) {
  return {
    available: true,
    open: false,
    subject: '',
    speaker: '',
    greeting: '',
    people: [],
    topics: [],
    withheld: [],
    said: [],
    action: '',
    outcome: 'none',
    code: '',
    message: '',
    residue: '',
    handoff: '',
    topic: '',
    ...overrides,
  };
}

/** Somebody the party is speaking with: a person on their own, with a line to say and two topics. */
function talking(overrides = {}) {
  return conversation({
    open: true,
    subject: 'person-0',
    speaker: 'Tester Two',
    greeting: "'A fine day for it.'",
    people: [{ id: 'np-2', name: 'Tester Two', portrait: '707', speaking: true }],
    topics: [{ id: 'topic-1', label: 'The contest', available: true, reason: '' }],
    withheld: [{ id: 'topic-2', label: 'The errand', available: false, reason: 'the errand the table calls 7 is not finished' }],
    said: [
      { speaker: 'np-2', text: "'A fine day for it.'", residue: 'the event programs behind a reply are not run' },
    ],
    action: 'open',
    outcome: 'applied',
    message: "Tester Two: 'A fine day for it.'",
    ...overrides,
  });
}

/**
 * The creation block as the product publishes it while a party is being made: the flow's own options and
 * the member's own answers, so every choice below arrived in the projection.
 */
function creation(overrides = {}) {
  return {
    active: true,
    accepted: false,
    hasDefault: true,
    member: 0,
    members: 2,
    step: 'portrait',
    pool: 50,
    refusalCode: '',
    refusalMessage: '',
    roster: [
      { index: 0, step: 'portrait', name: '', race: '', class: '', portrait: '', pool: 50 },
      { index: 1, step: 'complete', name: 'Aelina', race: 'Elf', class: 'Sorcerer', portrait: 'elf-woman', pool: 0 },
    ],
    portraits: [
      { id: 'human-woman', name: 'Human woman', race: 'Human', selected: false },
      { id: 'elf-woman', name: 'Elf woman', race: 'Elf', selected: true },
    ],
    classes: [
      { id: 'Knight', name: 'Knight', selected: false },
      { id: 'Sorcerer', name: 'Sorcerer', selected: false },
    ],
    skills: [
      { id: 'Staff', name: 'Staff', state: 'fixed' },
      { id: 'Air', name: 'Air', state: 'chosen' },
      { id: 'Water', name: 'Water', state: 'available' },
    ],
    attributes: [
      { id: 'Might', name: 'Might', value: 11, minimum: 9, maximum: 25, canRaise: true, canLower: true },
      { id: 'Intellect', name: 'Intellect', value: 14, minimum: 12, maximum: 30, canRaise: true, canLower: true },
    ],
    party: [],
    ...overrides,
  };
}

/** The creation block of a session that accepted a party and is playing it. */
function acceptedParty(overrides = {}) {
  return creation({
    active: false,
    accepted: true,
    step: '',
    pool: 0,
    member: 0,
    roster: [],
    portraits: [],
    classes: [],
    skills: [],
    attributes: [],
    party: [
      { index: 0, name: 'Roderick', race: 'Human', class: 'Knight', portrait: 'human-man' },
      { index: 1, name: 'Nyx', race: 'Goblin', class: 'Thief', portrait: 'goblin-woman' },
    ],
    ...overrides,
  });
}

/**
 * The progression block as the product publishes it: the level, the experience against the curve the
 * ruleset states, the points held, and the fee the counter the party stands at would charge. `available` is
 * false when the session holds no owner at all.
 */
function progression(overrides = {}) {
  return {
    available: true,
    members: [
      {
        index: 0, member: '1', name: 'Roderick', level: 1, experience: 0, skillPoints: 0,
        nextLevel: 1000, fee: 0, cap: 0,
      },
    ],
    outcome: 'none',
    source: '',
    earned: 0,
    code: '',
    message: '',
    ...overrides,
  };
}

function snapshot(mode, seconds = 0, steps = 0, updates = 0, facts = undefined, blocks = undefined) {
  const value = {
    composition: {
      ruleset: 'mightandmagic7',
      title: 'Might and Magic VII: For Blood and Honor',
      bundle: 'partyrpg-default',
      contentPacks: 2,
    },
    session: { mode, simulationSeconds: seconds, admittedSteps: steps, updates },
    world: world(),
  };
  // A projection is free to carry no movement block at all — that is what a session without movement
  // looks like — so the helper adds one only when a case asks for it. The clock and the party are
  // optional on the same terms: a case that asks for neither gets a projection that carries neither.
  if (facts !== undefined) value.movement = facts;
  if (blocks?.clock !== undefined) value.clock = blocks.clock;
  if (blocks?.party !== undefined) value.party = blocks.party;
  // Creation is published in every mode, so the helper adds the block only when a case asks for one: a
  // case that asks for none covers the projection a session that is doing neither publishes.
  if (blocks?.creation !== undefined) value.creation = blocks.creation;
  // The save block is published in every mode too, so a case that asks for none covers a projection whose
  // session the companion cannot read as saveable.
  if (blocks?.save !== undefined) value.save = blocks.save;
  // The interaction block is published in every mode too, so a case that asks for none covers a projection
  // whose session holds no interaction at all.
  if (blocks?.interaction !== undefined) value.interaction = blocks.interaction;
  // The service block is published in every mode too, so a case that asks for none covers a projection
  // whose session holds no service mechanism.
  if (blocks?.service !== undefined) value.service = blocks.service;
  // The rest block is published in every mode too, so a case that asks for none covers a projection whose
  // session holds no rest mechanism.
  if (blocks?.rest !== undefined) value.rest = blocks.rest;
  // The conversation block is published in every mode too, so a case that asks for none covers a projection
  // whose session holds no conversation mechanism.
  if (blocks?.conversation !== undefined) value.conversation = blocks.conversation;
  // The fight block is published in every mode too, so a case that asks for none covers a projection whose
  // session holds no fight mechanism.
  if (blocks?.combat !== undefined) value.combat = blocks.combat;
  // The progression block is published in every mode too, so a case that asks for none covers a projection
  // whose session holds no progression owner.
  if (blocks?.progression !== undefined) value.progression = blocks.progression;
  // The skills block is published in every mode too, so a case that asks for none covers a projection whose
  // ruleset stated no skill policy.
  if (blocks?.skills !== undefined) value.skills = blocks.skills;
  // The magic block is published in every mode too, so a case that asks for none covers a projection whose
  // ruleset stated no spell policy.
  if (blocks?.magic !== undefined) value.magic = blocks.magic;
  return value;
}

function harness() {
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>');
  // The companion is browser code: it reads the global document, exactly as it does in the shell.
  globalThis.window = dom.window;
  globalThis.document = dom.window.document;
  const { document } = dom.window;
  const claims = [];
  let listener = null;
  let unsubscribed = false;
  const context = {
    intents: {
      claim: (intent, value) => claims.push({ intent, value }),
    },
    projection: {
      subscribe: (next) => {
        listener = next;
        return () => {
          unsubscribed = true;
          listener = null;
        };
      },
    },
  };
  const root = document.querySelector('#root');
  const timers = { setTimeout: 0, setInterval: 0, requestAnimationFrame: 0 };
  const originals = {
    setTimeout: globalThis.setTimeout,
    setInterval: globalThis.setInterval,
    requestAnimationFrame: globalThis.requestAnimationFrame,
  };
  globalThis.setTimeout = (...args) => {
    timers.setTimeout += 1;
    return originals.setTimeout(...args);
  };
  globalThis.setInterval = (...args) => {
    timers.setInterval += 1;
    return originals.setInterval(...args);
  };
  globalThis.requestAnimationFrame = (...args) => {
    timers.requestAnimationFrame += 1;
    return 0;
  };

  const restore = () => {
    globalThis.setTimeout = originals.setTimeout;
    globalThis.setInterval = originals.setInterval;
    globalThis.requestAnimationFrame = originals.requestAnimationFrame;
    delete globalThis.document;
    delete globalThis.window;
  };

  return {
    dom,
    document,
    root,
    context,
    claims,
    timers,
    restore,
    emit: (value, contract = CONTRACT) => listener?.({ contract, value }),
    panel: () => root.querySelector('.crawler-session'),
    // The session's own action button is a direct child of the panel: the creation screen's buttons
    // live inside their own section, and a case that clicks 'the button' means the session's.
    button: () => root.querySelector('.crawler-session > button'),
    // The save control is the panel's other direct-child button, named by the class the panel gives it.
    saveButton: () => root.querySelector('.crawler-session > button.crawler-save'),
    // The use control is named the same way, so a case that clicks 'the save button' and one that clicks
    // 'the use button' can never mean each other.
    useButton: () => root.querySelector('.crawler-session > button.crawler-use'),
    get unsubscribed() {
      return unsubscribed;
    },
  };
}

function readPanel(h) {
  const panel = h.panel();
  return {
    mode: panel?.getAttribute('data-mode'),
    place: panel?.querySelector('.crawler-place')?.textContent,
    ruleset: panel?.querySelector('.crawler-ruleset')?.textContent,
    bundle: panel?.querySelector('.crawler-bundle')?.textContent,
    title: panel?.querySelector('h1')?.textContent,
    button: h.button()?.textContent,
    disabled: h.button()?.disabled,
    values: [...(panel?.querySelectorAll('dd') ?? [])].map((entry) => entry.textContent),
  };
}

/** The four movement rows, read by the label a person reads rather than by their position. */
function movementRows(h) {
  const panel = h.panel();
  const labels = [...(panel?.querySelectorAll('dt') ?? [])].map((entry) => entry.textContent);
  const values = [...(panel?.querySelectorAll('dd') ?? [])].map((entry) => entry.textContent);
  const read = (label) => values[labels.indexOf(label)];
  return { motion: read('Motion'), blocked: read('Blocked'), step: read('Step up'), fall: read('Fall') };
}

/** Any named rows, read by their label, so a case asserts what a person reads and not a row number. */
function rows(h, ...labels) {
  const panel = h.panel();
  const terms = [...(panel?.querySelectorAll('dt') ?? [])].map((entry) => entry.textContent);
  const values = [...(panel?.querySelectorAll('dd') ?? [])].map((entry) => entry.textContent);
  return Object.fromEntries(labels.map((label) => [label, values[terms.indexOf(label)]]));
}

/** The creation section as a person reads it: the step it is headlining, the refusal, and its buttons. */
function creationPanel(h) {
  const panel = h.panel();
  const section = panel?.querySelector('.crawler-creation');
  const buttons = [...(section?.querySelectorAll('.crawler-options button') ?? [])];
  return {
    state: panel?.getAttribute('data-creation'),
    hidden: section?.hidden,
    head: section?.querySelector('.crawler-step-head')?.textContent,
    refusal: section?.querySelector('.crawler-refusal')?.textContent,
    refusalCode: section?.querySelector('.crawler-refusal')?.getAttribute('data-code'),
    members: buttons
      .filter((button) => button.dataset.member !== undefined)
      .map((button) => ({ text: button.textContent, index: button.dataset.member, step: button.dataset.step })),
    options: buttons
      .filter((button) => button.dataset.id !== undefined)
      .map((button) => ({ id: button.dataset.id, text: button.textContent, disabled: button.disabled })),
    attributes: [...(section?.querySelectorAll('.crawler-attribute') ?? [])].map((line) => ({
      text: line.querySelector('span')?.textContent,
      lower: line.querySelectorAll('button')[0]?.dataset.canLower,
      raise: line.querySelectorAll('button')[1]?.dataset.canRaise,
    })),
    accepted: [...(section?.querySelectorAll('.crawler-accepted li') ?? [])].map((item) => item.textContent),
    name: section?.querySelector('input')?.value,
  };
}

/** The service section as a person reads it: the counter, its offers, and the last command's answer. */
function servicePanel(h) {
  const panel = h.panel();
  const section = panel?.querySelector('.crawler-service');
  const result = section?.querySelector('.crawler-service-result');
  return {
    state: panel?.getAttribute('data-service'),
    counterState: panel?.getAttribute('data-service-state'),
    action: panel?.getAttribute('data-service-action'),
    outcome: panel?.getAttribute('data-service-outcome'),
    hidden: section?.hidden,
    head: section?.querySelector('.crawler-step-head')?.textContent,
    status: section?.querySelector('.crawler-service-state')?.textContent,
    access: section?.querySelector('.crawler-service-access')?.textContent,
    options: [...(section?.querySelectorAll('.crawler-options button') ?? [])].map((button) => ({
      id: button.dataset.id,
      text: button.textContent,
      disabled: button.disabled,
    })),
    members: [...(section?.querySelectorAll('select option') ?? [])].map((option) => ({
      index: option.value,
      name: option.textContent,
    })),
    leave: [...(section?.querySelectorAll('.crawler-actions button') ?? [])].map((button) => button.textContent),
    message: result?.hidden ? '' : result?.textContent,
    messageOutcome: result?.getAttribute('data-outcome'),
    messageCode: result?.getAttribute('data-code'),
    resolved: result?.getAttribute('data-resolved'),
    hit: result?.getAttribute('data-hit'),
    chance: result?.getAttribute('data-chance'),
    damage: result?.getAttribute('data-damage'),
    damageRolled: result?.getAttribute('data-damage-rolled'),
    damageKind: result?.getAttribute('data-damage-kind'),
    resistance: result?.getAttribute('data-resistance'),
    condition: result?.getAttribute('data-condition'),
    targetDown: result?.getAttribute('data-target-down'),
    byParty: result?.getAttribute('data-by-party'),
  };
}

/** The stop section as a person reads it: the controls, the night's own facts, and the fatigue line. */
function restPanel(h) {
  const panel = h.panel();
  const section = panel?.querySelector('.crawler-rest');
  const result = section?.querySelector('.crawler-rest-result');
  return {
    state: panel?.getAttribute('data-rest'),
    action: panel?.getAttribute('data-rest-action'),
    outcome: panel?.getAttribute('data-rest-outcome'),
    tired: panel?.getAttribute('data-tired'),
    hidden: section?.hidden,
    status: section?.querySelector('.crawler-rest-state')?.textContent,
    controls: [...(section?.querySelectorAll('.crawler-actions button') ?? [])].map((button) => ({
      id: button.dataset.id,
      text: button.textContent,
      disabled: button.disabled,
    })),
    message: result?.hidden ? '' : result?.textContent,
    messageOutcome: result?.getAttribute('data-outcome'),
    messageCode: result?.getAttribute('data-code'),
    resolved: result?.getAttribute('data-resolved'),
    hit: result?.getAttribute('data-hit'),
    chance: result?.getAttribute('data-chance'),
    damage: result?.getAttribute('data-damage'),
    damageRolled: result?.getAttribute('data-damage-rolled'),
    damageKind: result?.getAttribute('data-damage-kind'),
    resistance: result?.getAttribute('data-resistance'),
    condition: result?.getAttribute('data-condition'),
    targetDown: result?.getAttribute('data-target-down'),
    byParty: result?.getAttribute('data-by-party'),
  };
}

/** The fight section as a person reads it: who is in it, who may act, and what the last order did. */
/** The progression section as the panel rendered it: each member's row and the train control it offers. */
function progressionPanel(h) {
  const panel = h.panel();
  const section = panel?.querySelector('.crawler-progression');
  const result = section?.querySelector('.crawler-progression-result');
  return {
    section,
    hidden: section?.hidden,
    state: section?.querySelector('.crawler-progression-state')?.textContent ?? null,
    outcome: result?.getAttribute('data-outcome') ?? null,
    code: result?.getAttribute('data-code') ?? null,
    message: result?.textContent ?? null,
    members: [...(section?.querySelectorAll('.crawler-progression-member') ?? [])].map((row) => ({
      label: row.querySelector('.crawler-row-label')?.textContent ?? null,
      id: row.getAttribute('data-member'),
      level: row.getAttribute('data-level'),
      train: row.querySelector('.crawler-train')?.textContent ?? null,
    })),
  };
}

/**
 * The skills block as the product publishes it: each member's own rows with the rung they stand at, the
 * ceiling their class and rank impose, and what one more point would cost or why it would be refused.
 */
function skills(overrides = {}) {
  return {
    available: true,
    members: [
      {
        index: 0, member: '1', name: 'Roderick', class: 'Knight', rank: 1,
        skills: [
          {
            skill: 'Sword', block: 'weapon', level: 3, tier: 'basic', ceilingLevel: 12,
            ceilingTier: 'master', pointsSpent: 5, reached: 4, cost: 4, refusal: '',
          },
        ],
      },
    ],
    outcome: 'none',
    member: '',
    skill: '',
    level: 0,
    cost: 0,
    code: '',
    message: '',
    ...overrides,
  };
}

/** The magic block as the product publishes it: one member's spellbook and the actors a cast may name. */
function magic(overrides = {}) {
  return {
    available: true,
    members: [
      {
        index: 0, member: '1', name: 'Aelina', class: 'Sorcerer',
        spellPoints: 33, spellPointsMax: 36, quickSpell: '', quickSpellName: '',
        spells: [
          {
            spell: '2', name: 'Fire Bolt', school: 'Fire', tier: 'basic', tierRung: 1,
            cost: 2, targeting: 'foe', effect: 'damage',
          },
          {
            spell: '1', name: 'Torch Light', school: 'Fire', tier: 'basic', tierRung: 1,
            cost: 1, targeting: 'party', effect: 'light',
          },
        ],
      },
    ],
    targets: [
      { target: 'member:1', name: 'Aelina', side: 'party' },
      { target: 'actor:9', name: 'A beast', side: 'opposition' },
    ],
    outcome: 'none',
    member: 0,
    caster: '',
    spell: '',
    cost: 0,
    target: '',
    effect: '',
    code: '',
    message: '',
    ...overrides,
  };
}

function magicPanel(h) {
  const panel = h.panel();
  const section = panel?.querySelector('.crawler-magic');
  const result = section?.querySelector('.crawler-magic-result');
  return {
    section,
    hidden: section?.hidden,
    state: section?.querySelector('.crawler-magic-state')?.textContent ?? null,
    outcome: result?.getAttribute('data-outcome') ?? null,
    code: result?.getAttribute('data-code') ?? null,
    message: result?.textContent ?? null,
    members: [...(section?.querySelectorAll('.crawler-magic-member') ?? [])].map((row) => ({
      label: row.querySelector('.crawler-row-label')?.textContent ?? null,
      id: row.getAttribute('data-member'),
      spells: [...row.querySelectorAll('.crawler-spell')].map((line) => ({
        text: line.querySelector('span')?.textContent ?? null,
        spell: line.getAttribute('data-spell'),
        school: line.getAttribute('data-school'),
        targeting: line.getAttribute('data-targeting'),
        targets: [...line.querySelectorAll('.crawler-target option')].map((option) => option.value),
        cast: line.querySelector('.crawler-cast')?.textContent ?? null,
        castDisabled: line.querySelector('.crawler-cast')?.disabled ?? null,
        quick: line.querySelector('.crawler-quick')?.textContent ?? null,
      })),
    })),
  };
}

function skillsPanel(h) {
  const panel = h.panel();
  const section = panel?.querySelector('.crawler-skills');
  const result = section?.querySelector('.crawler-skills-result');
  return {
    section,
    hidden: section?.hidden,
    state: section?.querySelector('.crawler-skills-state')?.textContent ?? null,
    outcome: result?.getAttribute('data-outcome') ?? null,
    code: result?.getAttribute('data-code') ?? null,
    message: result?.textContent ?? null,
    members: [...(section?.querySelectorAll('.crawler-skills-member') ?? [])].map((row) => ({
      label: row.querySelector('.crawler-row-label')?.textContent ?? null,
      id: row.getAttribute('data-member'),
      skills: [...row.querySelectorAll('.crawler-skill')].map((line) => ({
        text: line.querySelector('span')?.textContent ?? null,
        skill: line.getAttribute('data-skill'),
        block: line.getAttribute('data-block'),
        tier: line.getAttribute('data-tier'),
        refusal: line.getAttribute('data-refusal'),
        raise: line.querySelector('.crawler-raise')?.textContent ?? null,
      })),
    })),
  };
}

function combatPanel(h) {
  const panel = h.panel();
  const section = panel?.querySelector('.crawler-combat');
  const attack = section?.querySelector('.crawler-attack');
  const result = section?.querySelector('.crawler-combat-result');
  const fighters = (list) =>
    [...(list?.querySelectorAll('.crawler-fighter') ?? [])].map((row) => ({
      name: row.textContent,
      ready: row.getAttribute('data-ready'),
      id: row.getAttribute('data-fighter'),
      health: row.getAttribute('data-health'),
      conditions: row.getAttribute('data-conditions'),
      down: row.getAttribute('data-down'),
      activity: row.getAttribute('data-activity'),
    }));
  const pace = section?.querySelector('.crawler-pace');
  const skip = section?.querySelector('.crawler-skip');
  const wait = section?.querySelector('.crawler-wait');
  const order = (list) =>
    [...(list?.querySelectorAll('.crawler-turn-actor') ?? [])].map((row) => ({
      name: row.textContent,
      id: row.getAttribute('data-turn-actor'),
      side: row.getAttribute('data-side'),
      ready: row.getAttribute('data-ready'),
      current: row.getAttribute('data-current'),
      waiting: row.getAttribute('data-waiting'),
      remaining: row.getAttribute('data-remaining'),
    }));
  return {
    state: panel?.getAttribute('data-combat'),
    ready: panel?.getAttribute('data-combat-ready'),
    opposition: panel?.getAttribute('data-combat-opposition'),
    outcome: panel?.getAttribute('data-combat-outcome'),
    hidden: section?.hidden,
    status: section?.querySelector('.crawler-combat-state')?.textContent,
    pacing: panel?.getAttribute('data-pacing'),
    phase: panel?.getAttribute('data-turn-phase'),
    round: panel?.getAttribute('data-turn-round'),
    turnActor: panel?.getAttribute('data-turn-actor'),
    playerTurn: panel?.getAttribute('data-turn-player'),
    last: panel?.getAttribute('data-turn-last'),
    turn: section?.querySelector('.crawler-combat-turn')?.textContent,
    order: order(section?.querySelector('.crawler-turn-order')),
    attack: { disabled: attack?.disabled, text: attack?.textContent },
    pace: { disabled: pace?.disabled, text: pace?.textContent },
    skip: { disabled: skip?.disabled, text: skip?.textContent },
    wait: { disabled: wait?.disabled, text: wait?.textContent },
    members: fighters(section?.querySelector('.crawler-combat-members')),
    enemies: fighters(section?.querySelector('.crawler-combat-enemies')),
    message: result?.hidden ? '' : result?.textContent,
    messageOutcome: result?.getAttribute('data-outcome'),
    messageCode: result?.getAttribute('data-code'),
    resolved: result?.getAttribute('data-resolved'),
    hit: result?.getAttribute('data-hit'),
    chance: result?.getAttribute('data-chance'),
    damage: result?.getAttribute('data-damage'),
    damageRolled: result?.getAttribute('data-damage-rolled'),
    damageKind: result?.getAttribute('data-damage-kind'),
    resistance: result?.getAttribute('data-resistance'),
    condition: result?.getAttribute('data-condition'),
    targetDown: result?.getAttribute('data-target-down'),
    byParty: result?.getAttribute('data-by-party'),
  };
}

/** The conversation section as a person reads it: who is here, what may be asked, and what was said. */
function conversationPanel(h) {
  const panel = h.panel();
  const section = panel?.querySelector('.crawler-conversation');
  const result = section?.querySelector('.crawler-conversation-result');
  return {
    state: panel?.getAttribute('data-conversation'),
    action: panel?.getAttribute('data-conversation-action'),
    outcome: panel?.getAttribute('data-conversation-outcome'),
    hidden: section?.hidden,
    head: section?.querySelector('.crawler-step-head')?.textContent,
    greeting: section?.querySelector('.crawler-conversation-greeting')?.textContent,
    people: [...(section?.querySelectorAll('.crawler-row button') ?? [])].map((button) => ({
      id: button.dataset.person,
      text: button.textContent,
      disabled: button.disabled,
    })),
    topics: [...(section?.querySelectorAll('.crawler-options button') ?? [])].map((button) => ({
      id: button.dataset.id,
      text: button.textContent,
      disabled: button.disabled,
    })),
    withheld: [...(section?.querySelectorAll('.crawler-withheld li') ?? [])].map((item) => item.textContent),
    said: [...(section?.querySelectorAll('.crawler-said li') ?? [])].map((item) => item.textContent),
    leave: [...(section?.querySelectorAll('.crawler-actions button') ?? [])].map((button) => button.textContent),
    message: result?.hidden ? '' : result?.textContent,
    messageOutcome: result?.getAttribute('data-outcome'),
    messageCode: result?.getAttribute('data-code'),
    residue: section?.querySelector('.crawler-conversation-residue')?.hidden
      ? ''
      : section?.querySelector('.crawler-conversation-residue')?.textContent,
  };
}

/** Clicks the topic a conversation offers, as a person chooses it. */
function clickTopic(h, id) {
  const button = [...h.panel().querySelectorAll('.crawler-conversation .crawler-options button')].find(
    (entry) => entry.dataset.id === id,
  );
  assert.ok(button, `the conversation offers no topic '${id}'`);
  button.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
}

/** Clicks the person a conversation offers to turn to. */
function clickPerson(h, id) {
  const button = [...h.panel().querySelectorAll('.crawler-conversation .crawler-row button')].find(
    (entry) => entry.dataset.person === id,
  );
  assert.ok(button, `the conversation offers no person '${id}'`);
  button.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
}

/** Clicks the stop control a case names, as a person presses it. */
function clickStop(h, action) {
  const button = [...h.panel().querySelectorAll('.crawler-rest .crawler-actions button')].find(
    (entry) => entry.dataset.id === action,
  );
  assert.ok(button, `the stop controls offer no '${action}'`);
  button.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
}

/** Clicks the service button a person reads by its id, which is the lot, item, or lesson it names. */
function clickService(h, id) {
  const button = [...h.panel().querySelectorAll('.crawler-service .crawler-options button')].find(
    (entry) => entry.dataset.id === id,
  );
  assert.ok(button, `the service screen offers no '${id}'`);
  button.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
}

/** Clicks the option button whose id and visible text the case names. */
function clickOption(h, id, text = undefined) {
  const button = [...h.panel().querySelectorAll('.crawler-options button')].find(
    (entry) => entry.dataset.id === id && (text === undefined || entry.textContent === text),
  );
  assert.ok(button, `the creation screen offers no option '${id}'`);
  button.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
}

/** Clicks the creation button a person reads by its label, such as Confirm step or Accept party. */
function clickFlow(h, label) {
  const button = [...h.panel().querySelectorAll('.crawler-actions button')].find(
    (entry) => entry.textContent === label,
  );
  assert.ok(button, `the creation screen offers no '${label}' button`);
  button.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
}

test('renders nothing until the product publishes, then renders what it published', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    assert.deepEqual(readPanel(h), {
      mode: 'starting',
      ruleset: '',
      bundle: '',
      title: '',
      button: 'Starting…',
      disabled: true,
      values: Array(33).fill('—'),
      place: '',
    });

    h.emit(snapshot('running', 12.34, 740, 741, movement()));
    assert.deepEqual(readPanel(h), {
      mode: 'running',
      ruleset: 'Might and Magic VII: For Blood and Honor',
      bundle: 'partyrpg-default · 2 packs',
      title: 'Rusty Crawler',
      button: 'Pause session',
      disabled: false,
      values: [
        'running', '12.3 s', '740', '741', '2',
        // The date, the time, the days, the party, what it carries, the purse, the food, the standing, and
        // the conditions, then what the party has left to lose and to cast with: a projection that carries
        // no party block shows all eleven as not known.
        '—', '—', '—', '—', '—', '—', '—', '—', '—', '—', '—',
        '1', '—', '1234, 5678, 0 @ 512', '1 / 76', 'grounded', '—', '—', '—',
        '—', '—', '—',
        // A projection that carries no save block is a session this companion cannot read as saveable, and
        // the panel says so on the Save row rather than offering a save it cannot make. A projection that
        // carries no interaction block is a session with nothing to use, and its four rows say the same
        // thing rather than showing an empty reticle that looks like an empty room.
        'unavailable', 'fresh', '—', '—', '—', '—',
      ],
      place: 'Emerald Island · region',
    });

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('reports an unselected bundle and counts a single pack in the singular', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    h.emit({
      composition: {
        ruleset: 'mightandmagic7',
        title: 'Might and Magic VII: For Blood and Honor',
        bundle: '',
        contentPacks: 0,
      },
      session: { mode: 'running', simulationSeconds: 0, admittedSteps: 0, updates: 1 },
      world: world(),
    });
    assert.equal(readPanel(h).bundle, 'No game bundle selected');

    h.emit({
      composition: {
        ruleset: 'mightandmagic7',
        title: 'Might and Magic VII: For Blood and Honor',
        bundle: 'partyrpg-default',
        contentPacks: 1,
      },
      session: { mode: 'running', simulationSeconds: 0, admittedSteps: 0, updates: 2 },
      world: world(),
    });
    assert.equal(readPanel(h).bundle, 'partyrpg-default · 1 pack');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a projection on another contract is not interpreted', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(snapshot('running', 5, 300, 300));
    h.emit(snapshot('paused', 99, 999, 999), 'something.else.v1');
    h.emit({ composition: { ruleset: 1 }, session: null });

    assert.equal(readPanel(h).mode, 'running');
    assert.equal(readPanel(h).values[1], '5.0 s');
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the button asks for the session action that fits the mode it was shown', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(snapshot('running', 1, 60, 60));
    h.button().dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));

    h.emit(snapshot('paused', 1, 60, 61));
    assert.equal(readPanel(h).button, 'Resume session');
    h.button().dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));

    assert.deepEqual(h.claims, [
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'session.pause' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'session.resume' } } },
    ]);

    // A disabled button cannot ask for anything: stopping is not something this panel may request.
    h.emit(snapshot('stopped', 1, 60, 62));
    assert.equal(readPanel(h).button, 'Session stopped');
    assert.equal(h.claims.length, 2);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the companion holds no state and starts no timer', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    // None of these projections carries a movement block, which is what a session without movement looks
    // like: the panel still renders every other value and each movement row shows that it has no facts.
    h.emit(snapshot('running', 1, 60, 60));
    h.emit(snapshot('paused', 2, 120, 121));
    h.emit(snapshot('running', 3, 180, 182));

    assert.deepEqual(h.timers, { setTimeout: 0, setInterval: 0, requestAnimationFrame: 0 });
    // Rendering the newest projection replaces the previous values rather than accumulating them.
    assert.deepEqual(readPanel(h).values, [
      'running', '3.0 s', '180', '182', '2',
      '—', '—', '—', '—', '—', '—', '—', '—', '—', '—', '—',
      '1', '—', '1234, 5678, 0 @ 512', '1 / 76', '—', '—', '—', '—',
      '—', '—', '—',
      'unavailable', 'fresh', '—', '—', '—', '—',
    ]);
    assert.equal(h.root.querySelectorAll('.crawler-session').length, 1);

    ui.dispose();
    assert.equal(h.unsubscribed, true);
    assert.equal(h.panel(), null);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel reports what the last movement step did', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A step the engine resolved with the party on its feet and nothing in the way.
    h.emit(snapshot('running', 1, 60, 60, movement()));
    assert.deepEqual(movementRows(h), { motion: 'grounded', blocked: '—', step: '—', fall: '—' });
    assert.equal(h.panel().getAttribute('data-motion'), 'grounded');
    assert.equal(h.panel().getAttribute('data-blocked'), 'none');

    // The direction the walk found blocked: the party is still standing, held where it is by a wall. This
    // is the reading a person needs to tell a blocked key from a key that never arrived.
    h.emit(snapshot('running', 2, 120, 121, movement({ blocked: 'wall' })));
    assert.deepEqual(movementRows(h), { motion: 'grounded', blocked: 'wall', step: '—', fall: '—' });
    assert.equal(h.panel().getAttribute('data-blocked'), 'wall');

    // A step-up the engine accepted, carrying the height it raised the party by.
    h.emit(snapshot('running', 3, 180, 182, movement({ stepRise: 0.4 })));
    assert.deepEqual(movementRows(h), { motion: 'grounded', blocked: '—', step: '+0.4', fall: '—' });

    // A landing in the air, with the drop and what the tuning priced it at.
    h.emit(snapshot('running', 4, 240, 243, movement({ motion: 'airborne', fallDistance: 28, fallDamage: 27 })));
    assert.deepEqual(movementRows(h), { motion: 'airborne', blocked: '—', step: '—', fall: '28 · 27 damage' });

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a session without movement facts shows that it does not know, and still renders', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // What the product publishes while no mover feeds the panel: the movement block is present and says
    // the party has not stepped, so the panel does not claim the way is clear.
    h.emit(snapshot('running', 1, 60, 60, movement({ motion: 'none' })));
    assert.deepEqual(movementRows(h), { motion: '—', blocked: '—', step: '—', fall: '—' });
    assert.equal(h.panel().getAttribute('data-motion'), 'none');
    assert.equal(readPanel(h).place, 'Emerald Island · region');

    // A projection that carries no movement block at all is not one this companion rejects either: the
    // session it describes has no movement, and the panel shows everything else it published.
    h.emit(snapshot('paused', 2, 120, 121));
    assert.deepEqual(movementRows(h), { motion: '—', blocked: '—', step: '—', fall: '—' });
    assert.equal(readPanel(h).mode, 'paused');
    assert.equal(readPanel(h).place, 'Emerald Island · region');
    assert.equal(readPanel(h).values[1], '2.0 s');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel renders the clock and the party the product published', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A session whose ruleset composed a clock and whose content supplied a party: every value below
    // arrived in the projection, and the panel prints it unchanged.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      clock: clock({ elapsedDays: 12, time: '21:30', daylight: 'night' }),
      party: party({ members: 4, coins: 200, provisions: 3, reputation: 3, fame: 1, conditions: 'weak (1)' }),
    }));

    assert.deepEqual(rows(h, 'Date', 'Time', 'Days'), {
      Date: '1168-01-01',
      Time: '21:30 · night',
      Days: '12',
    });
    assert.deepEqual(rows(h, 'Party', 'Coins', 'Food', 'Standing', 'Condition'), {
      Party: '4',
      Coins: '200',
      Food: '3 portions',
      Standing: '3 / 1',
      Condition: 'weak (1)',
    });
    assert.equal(h.panel().getAttribute('data-clock'), 'present');
    assert.equal(h.panel().getAttribute('data-party'), 'present');
    assert.equal(h.panel().getAttribute('data-light'), 'night');

    // A party that is fed and carrying nothing shows its numbers and that no condition acts, which is a
    // different reading from a party the panel knows nothing about.
    h.emit(snapshot('running', 2, 120, 121, movement(), { clock: clock(), party: party({ provisions: 0 }) }));
    assert.equal(rows(h, 'Food').Food, '0 portions');
    assert.equal(rows(h, 'Condition').Condition, '—');
    assert.equal(h.panel().getAttribute('data-light'), 'day');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a projection without a clock or a party shows that it does not know, and still renders', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A session whose ruleset composed neither publishes blocks that say so.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      clock: clock({ present: false }),
      party: party({ present: false }),
    }));
    assert.deepEqual(rows(h, 'Date', 'Time', 'Days', 'Party', 'Coins', 'Food', 'Standing', 'Condition'), {
      Date: '—',
      Time: '—',
      Days: '—',
      Party: '—',
      Coins: '—',
      Food: '—',
      Standing: '—',
      Condition: '—',
    });
    assert.equal(h.panel().getAttribute('data-clock'), 'none');
    assert.equal(h.panel().getAttribute('data-party'), 'none');
    assert.equal(h.panel().getAttribute('data-light'), 'unknown');

    // A projection that carries neither block at all describes the same session, and rendering half of it
    // would show a panel that disagrees with the product.
    h.emit(snapshot('paused', 2, 120, 121, movement({ blocked: 'wall' })));
    assert.equal(rows(h, 'Date').Date, '—');
    assert.equal(rows(h, 'Condition').Condition, '—');
    assert.deepEqual(rows(h, 'Session', 'Place', 'Blocked'), {
      Session: 'paused',
      Place: '1',
      Blocked: 'wall',
    });

    // A block that is present but not the shape this companion reads is treated the same way: the session
    // has a clock this panel cannot read, and saying so beats refusing every other row.
    h.emit(snapshot('running', 3, 180, 182, movement(), {
      clock: { present: true, date: 7, time: '09:00', daylight: 'day', elapsedDays: 0 },
      party: 'not a party',
    }));
    assert.equal(rows(h, 'Date').Date, '—');
    assert.equal(rows(h, 'Coins').Coins, '—');
    assert.equal(rows(h, 'Motion').Motion, 'grounded');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel renders the creation steps and the choices the product published', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    h.emit(snapshot('creating', 0, 0, 7, movement({ motion: 'none' }), { creation: creation() }));

    // The session's own rows say which member is being made, where it stands, and what the pool holds.
    assert.deepEqual(rows(h, 'Session', 'Member', 'Creation step', 'Pool'), {
      Session: 'creating',
      Member: '1 / 2',
      'Creation step': 'portrait',
      Pool: '50',
    });
    assert.equal(h.panel().getAttribute('data-creation'), 'active');
    assert.equal(readPanel(h).button, 'Creating a party…');
    assert.equal(readPanel(h).disabled, true);

    const shown = creationPanel(h);
    assert.equal(shown.hidden, false);
    assert.match(shown.head, /Creating member 1 of 2 · portrait/);
    // A flow whose members are all finished says so, which is the reading a player accepting the default
    // party needs: there is nothing left to choose, and every member can still be reopened.
    assert.equal(shown.head.includes('accept the party'), false);
    // The members are read from the flow's own roster, step included, so a finished member and one still
    // at its portrait are two different readings.
    assert.deepEqual(shown.members, [
      { text: '1. unnamed · no class · portrait', index: '0', step: 'portrait' },
      { text: '2. Aelina · Sorcerer · complete', index: '1', step: 'complete' },
    ]);
    // Every choice the projection listed is offered; a fixed skill is shown as fixed and cannot be chosen.
    assert.deepEqual(shown.options, [
      { id: 'human-woman', text: 'Human woman', disabled: false },
      { id: 'elf-woman', text: 'Elf woman', disabled: false },
      { id: 'Knight', text: 'Knight', disabled: false },
      { id: 'Sorcerer', text: 'Sorcerer', disabled: false },
      { id: 'Staff', text: 'Staff (fixed)', disabled: true },
      { id: 'Air', text: 'Air', disabled: false },
      { id: 'Water', text: 'Water', disabled: false },
    ]);
    // The attributes carry the race's own bounds and what the pool may do to each, and the panel decides
    // none of it: the two flags arrived in the projection.
    assert.deepEqual(shown.attributes, [
      { text: 'Might 11 (9–25)', lower: 'true', raise: 'true' },
      { text: 'Intellect 14 (12–30)', lower: 'true', raise: 'true' },
    ]);
    assert.equal(shown.refusal, '');
    assert.equal(shown.hidden, false);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('every creation control asks for the choice it was shown', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(snapshot('creating', 0, 0, 7, movement({ motion: 'none' }), { creation: creation() }));

    clickOption(h, 'elf-woman');
    clickOption(h, 'Knight');
    clickOption(h, 'Water');
    clickOption(h, 'Air');
    // A member button moves creation onto that member without the screen deciding anything about it.
    const member = h.panel().querySelector('.crawler-options button[data-member="1"]');
    member.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    // The two attribute moves are reported by id, one action each.
    const attribute = h.panel().querySelectorAll('.crawler-attribute')[0];
    attribute.querySelectorAll('button')[1].dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    attribute.querySelectorAll('button')[0].dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    // The name is sent as the player typed it, and confirmed and accepted through the two flow controls.
    const input = h.panel().querySelector('.crawler-name input');
    input.value = 'Roderick';
    h.panel().querySelector('.crawler-name button').dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    clickFlow(h, 'Confirm step');
    clickFlow(h, 'Accept party');

    assert.deepEqual(h.claims, [
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.select-portrait', portrait: 'elf-woman' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.select-class', class: 'Knight' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.choose-skill', skill: 'Water' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.remove-skill', skill: 'Air' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.select-member', member: 1 } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.raise-attribute', attribute: 'Might' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.lower-attribute', attribute: 'Might' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.set-name', name: 'Roderick' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.advance' } } },
      { intent: ACTION_INTENT, value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'creation.accept' } } },
    ]);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel shows the rule a refused creation choice broke', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // An illegal choice is the flow's answer, published as the code a caller branches on and the message a
    // person reads. The panel shows both and changes nothing else about the screen.
    h.emit(snapshot('creating', 0, 0, 8, movement({ motion: 'none' }), {
      creation: creation({
        step: 'attributes',
        pool: 0,
        refusalCode: 'attribute-ceiling',
        refusalMessage: 'Might is already 25 and creation raises it at most to 25 for this race.',
      }),
    }));

    const shown = creationPanel(h);
    assert.equal(shown.refusalCode, 'attribute-ceiling');
    assert.equal(shown.refusal, 'Might is already 25 and creation raises it at most to 25 for this race.');
    assert.deepEqual(rows(h, 'Creation step'), { 'Creation step': 'attributes' });
    // The choices are still the ones the projection carried: a refusal is not a reason to take the screen
    // away, because the player changes the choice that was refused next.
    assert.ok(shown.options.length > 0);

    // A projection whose refusal is empty shows none, which is a different reading from a refusal whose
    // message this companion cannot read.
    h.emit(snapshot('creating', 0, 0, 9, movement({ motion: 'none' }), {
      creation: creation({ refusalCode: '', refusalMessage: '' }),
    }));
    assert.equal(creationPanel(h).refusal, '');
    assert.equal(h.panel().querySelector('.crawler-refusal').hidden, true);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel shows the party a session accepted once it is playing it', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // The accepted state: the members are the party's own, read from the party the session plays, and the
    // creation choices are gone because there is no draft left to choose from.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      clock: clock(),
      party: party({ members: 2 }),
      creation: acceptedParty(),
    }));

    const shown = creationPanel(h);
    assert.equal(h.panel().getAttribute('data-creation'), 'accepted');
    assert.equal(shown.accepted.length, 2);
    assert.match(shown.accepted[0], /Roderick · Human · Knight · human-man/);
    assert.match(shown.accepted[1], /Nyx · Goblin · Thief · goblin-woman/);
    assert.deepEqual(shown.options, []);
    assert.deepEqual(shown.members, []);
    assert.equal(shown.head, 'Party accepted');
    // The screen marks the accepting player's name box and the flow's own controls hidden, and hidden means
    // hidden: a row that kept its own display rule would leave a name box and two dead buttons on screen,
    // and in the tab order, once the party it belonged to has been accepted.
    const creationSection = h.panel().querySelector('.crawler-creation');
    assert.equal(creationSection.querySelector('.crawler-name').hidden, true);
    assert.equal(creationSection.querySelector('.crawler-actions').hidden, true);
    assert.deepEqual(rows(h, 'Session', 'Party', 'Member', 'Pool'), {
      Session: 'running',
      Party: '2',
      Member: '1 / 2',
      Pool: '—',
    });

    // A session that is doing neither — a resumed one — publishes the empty creation block, and the screen
    // shows no creation section at all rather than a party nobody is making.
    h.emit(snapshot('running', 2, 120, 121, movement(), { party: party() }));
    assert.equal(h.panel().getAttribute('data-creation'), 'none');
    assert.equal(creationPanel(h).hidden, true);
    assert.equal(rows(h, 'Member', 'Creation step', 'Pool').Member, '—');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the save control asks for a save when there is something to save', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A running session with a store: the button offers the save, and clicking it asks the product for one
    // on the action name the product declares.
    h.emit(snapshot('running', 1, 60, 60, movement(), { save: save() }));
    assert.equal(h.saveButton().textContent, 'Save session');
    assert.equal(h.saveButton().disabled, false);
    h.saveButton().dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    assert.deepEqual(h.claims, [
      {
        intent: ACTION_INTENT,
        value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'session.save' } },
      },
    ]);

    // A party still being made has nothing to save, so the control is offered disabled rather than as a
    // button that would ask for a save of a session that does not exist yet.
    h.emit(snapshot('creating', 2, 60, 61, movement(), { save: save(), creation: creation() }));
    assert.equal(h.saveButton().disabled, true);

    // A session with no store says so on its Save row, and its control cannot work either.
    h.emit(snapshot('running', 3, 120, 122, movement(), { save: save({ available: false }) }));
    assert.equal(h.saveButton().disabled, true);
    assert.equal(rows(h, 'Save').Save, 'unavailable');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a landed save shows the moment and the slot, and a refused one shows why', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // What a save that landed looks like: the game moment the slot holds, the slot, and the product's own
    // account of what happened — all printed as they arrived.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      save: save({
        state: 'saved',
        at: '1168-01-01 09:30',
        message: "Saved the session to slot 'session' at 1168-01-01 09:30.",
      }),
    }));
    assert.equal(h.panel().getAttribute('data-save'), 'saved');
    assert.equal(rows(h, 'Save', 'Start').Save, '1168-01-01 09:30 · session');
    assert.equal(rows(h, 'Save', 'Start').Start, 'fresh');
    const result = h.panel().querySelector('.crawler-save-result');
    assert.equal(result.hidden, false);
    assert.equal(result.getAttribute('data-state'), 'saved');
    assert.match(result.textContent, /Saved the session to slot 'session'/);

    // What a save that did not looks like: the same row says failed, and the reason is on screen rather
    // than swallowed — a save that silently did nothing must not look like one that landed.
    h.emit(snapshot('running', 2, 120, 121, movement(), {
      save: save({ state: 'failed', code: 'save-refused', message: 'The session cannot be saved: the session holds no party.' }),
    }));
    assert.equal(h.panel().getAttribute('data-save'), 'failed');
    assert.equal(rows(h, 'Save').Save, 'failed');
    assert.equal(result.hidden, false);
    assert.equal(result.getAttribute('data-state'), 'failed');
    assert.equal(result.getAttribute('data-code'), 'save-refused');
    assert.match(result.textContent, /holds no party/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a resumed session says so, and shows the party it resumed', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // The host's composition decision, made visible: the start row and the creation head both name a
    // resumed session, and the roster below is the party the save holds.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      party: party({ members: 2 }),
      creation: acceptedParty(),
      save: save({ resumed: true }),
    }));

    assert.equal(rows(h, 'Start').Start, 'resumed');
    assert.equal(h.panel().getAttribute('data-creation'), 'resumed');
    assert.equal(creationPanel(h).head, 'Party resumed');
    assert.equal(creationPanel(h).accepted.length, 2);
    assert.match(creationPanel(h).accepted[0], /Roderick · Human · Knight · human-man/);
    assert.equal(rows(h, 'Save').Save, '—');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel shows the bodies lying in the place and what searching one gave', () => {
  const h = harness();
  try {
    mountProductUi(h.root, h.context);

    // Nothing killed yet: the place holds no body, and the row says so rather than leaving a reader to
    // guess whether the fact is zero or missing.
    h.emit(snapshot('running', 1, 60, 60, movement(), { interaction: interaction() }));
    assert.equal(h.panel().getAttribute('data-bodies'), '0');
    assert.equal(rows(h, 'Bodies here')['Bodies here'], '0');

    // A kill: the place holds one body, and the reticle holds it as a container the visit made.
    h.emit(snapshot('running', 2, 120, 122, movement(), {
      interaction: interaction({
        target: 'container',
        label: 'The body of A beast',
        verb: 'search',
        distance: 120,
        reason: 'selected',
        bodies: 1,
      }),
    }));
    assert.equal(h.panel().getAttribute('data-bodies'), '1');
    assert.equal(rows(h, 'Bodies here')['Bodies here'], '1');
    assert.equal(rows(h, 'Facing').Facing, 'The body of A beast · search · 120');

    // And what the search found is the product's own sentence, with the pack and the purse after it: the
    // panel prints what it was told rather than counting what it thinks was found.
    h.emit(snapshot('running', 3, 180, 184, movement(), {
      interaction: interaction({
        target: 'container',
        label: 'The body of A beast',
        verb: 'search',
        state: 'searched',
        distance: 120,
        reason: 'selected',
        bodies: 1,
        outcome: 'applied',
        message: 'The body of A beast holds Sword and 34 gold.',
      }),
      party: party({ coins: 34, pack: 1 }),
    }));
    assert.equal(h.panel().getAttribute('data-use'), 'applied');
    assert.equal(h.panel().querySelector('.crawler-use-result').textContent, 'The body of A beast holds Sword and 34 gold.');
    assert.equal(rows(h, 'Pack').Pack, '1');
    assert.equal(rows(h, 'Coins').Coins, '34');
  } finally {
    h.restore();
  }
});

test('the use control asks for a use when something is faced, and says why when nothing is', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A running session facing a door: the button offers the use, the panel says what is faced and what it
    // would do, and clicking it asks the product for a use on the action name the product declares.
    h.emit(snapshot('running', 1, 60, 60, movement(), { interaction: facedDoor() }));
    assert.equal(h.panel().getAttribute('data-interaction'), 'ready');
    assert.equal(h.useButton().textContent, 'Use');
    assert.equal(h.useButton().disabled, false);
    assert.equal(rows(h, 'Facing').Facing, 'A door · open · closed · 128');
    h.useButton().dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    assert.deepEqual(h.claims, [
      {
        intent: ACTION_INTENT,
        value: { kind: 'product-payload', contract: ACTION_CONTRACT, data: { action: 'party.use' } },
      },
    ]);

    // A session facing nothing: the row carries the product's own reason rather than an empty reticle, and
    // the control is offered disabled because a button that cannot work must not look like one that can.
    h.emit(snapshot('running', 2, 120, 121, movement(), { interaction: interaction() }));
    assert.equal(h.panel().getAttribute('data-interaction'), 'no-candidate');
    assert.equal(h.useButton().disabled, true);
    assert.equal(rows(h, 'Facing').Facing, 'no-candidate');

    // A session whose ruleset answered no interaction at all, and one still creating a party, offer no use
    // either; the Facing row says the mechanism is not there.
    h.emit(snapshot('running', 3, 180, 182, movement(), { interaction: interaction({ available: false }) }));
    assert.equal(h.panel().getAttribute('data-interaction'), 'none');
    assert.equal(rows(h, 'Facing').Facing, '—');
    h.emit(snapshot('creating', 4, 240, 244, movement(), { interaction: facedDoor(), creation: creation() }));
    assert.equal(h.useButton().disabled, true);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel shows what a use did, what a refusal said, and what it could not deliver', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A use that happened: the outcome word and what the door became, with the part this build cannot
    // deliver stated beside it rather than left out of the report.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      interaction: facedDoor({
        state: 'open',
        outcome: 'applied',
        message: 'A door swings open.',
        residue: 'Doors do not move in this build: its polygons are still admitted where they stood.',
      }),
    }));
    assert.equal(h.panel().getAttribute('data-use'), 'applied');
    assert.equal(rows(h, 'Use').Use, 'applied');
    const result = h.panel().querySelector('.crawler-use-result');
    assert.equal(result.hidden, false);
    assert.equal(result.getAttribute('data-outcome'), 'applied');
    assert.match(result.textContent, /swings open/);
    const residue = h.panel().querySelector('.crawler-use-residue');
    assert.equal(residue.hidden, false);
    assert.match(residue.textContent, /still admitted where they stood/);

    // A use that was refused: the same row says refused, the code is on the panel as data, and the product's
    // own sentence — what the door needs — is what a person reads. A use that silently did nothing must not
    // look like one that worked.
    h.emit(snapshot('running', 2, 120, 121, movement(), {
      interaction: facedDoor({
        verb: 'unlock',
        requires: ['the Iron Key'],
        outcome: 'refused',
        code: 'interaction-requirement-unmet',
        message: 'A door requires the Iron Key: it requires the Iron Key and the party carries 0 of it.',
      }),
    }));
    assert.equal(h.panel().getAttribute('data-use'), 'refused');
    assert.equal(rows(h, 'Use').Use, 'refused');
    assert.equal(rows(h, 'Requires').Requires, 'the Iron Key');
    assert.equal(result.getAttribute('data-outcome'), 'refused');
    assert.equal(result.getAttribute('data-code'), 'interaction-requirement-unmet');
    assert.match(result.textContent, /Iron Key/);
    assert.equal(residue.hidden, true);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel renders the counter the party stands at, with every price the product published', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    h.emit(snapshot('running', 1, 60, 60, movement(), { service: openShop() }));
    assert.equal(h.panel().getAttribute('data-service'), 'open');
    const shop = servicePanel(h);
    assert.equal(shop.hidden, false);
    assert.equal(shop.head, 'The Sword and Shield · Bertram');
    assert.equal(shop.status, 'Weapon Shop · open (06:00–18:00)');
    assert.equal(shop.access, '');
    assert.deepEqual(shop.options.map((entry) => entry.text), [
      'A fine sword ×2 — 110',
      'dagger ×1 — 22 (yours)',
      'potion — sold out',
      'Sell shield, damaged 3, unidentified — 12',
      'Identify shield',
      'Repair shield',
      'Sword to level 1 — 25',
      'Sword, expert — 1000',
    ]);
    // A line the counter has sold out of is shown disabled: the product would refuse the purchase by name,
    // and a button that cannot work must not look like one that can.
    assert.equal(shop.options.find((entry) => entry.id === 'stock:potion').disabled, true);
    assert.equal(shop.options.find((entry) => entry.id === 'stock:sword').disabled, false);
    assert.deepEqual(shop.members, [
      { index: '0', name: 'Roderick' },
      { index: '1', name: 'Nyx' },
    ]);
    assert.deepEqual(shop.leave, ['Leave the counter']);

    // A guild that gates its shelves: the membership the party carries is shown, and the counter's state is
    // the state the product published rather than one the screen worked out from the hours.
    h.emit(snapshot('running', 2, 120, 121, movement(), {
      service: openShop({
        kind: 'Fire Guild',
        memberships: ['Fire Guild membership'],
        state: 'closed',
        message: 'The Fire Guild is shut: it keeps 09:00–17:00 and the clock stands at 18:00.',
        action: 'buy',
        outcome: 'refused',
        code: 'service-closed',
      }),
    }));
    assert.equal(h.panel().getAttribute('data-service-state'), 'closed');
    assert.equal(servicePanel(h).access, 'Membership: Fire Guild membership');

    // A session whose ruleset answered no service policy shows no counter at all, which is a different fact
    // from a counter that is shut.
    h.emit(snapshot('running', 3, 180, 182, movement(), { service: service() }));
    assert.equal(h.panel().getAttribute('data-service'), 'away');
    assert.equal(servicePanel(h).hidden, true);

    // A session whose ruleset answered no policy at all is a different fact from one that merely stands
    // somewhere: the panel says the mechanism is not there.
    h.emit(snapshot('running', 4, 240, 244, movement(), { service: service({ available: false }) }));
    assert.equal(h.panel().getAttribute('data-service'), 'none');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('every service control asks for the command it was shown, on the product contract', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(snapshot('running', 1, 60, 60, movement(), { service: openShop() }));

    const claims = () => h.claims.slice(-1)[0];
    clickService(h, 'stock:sword');
    assert.deepEqual(claims().value.data, { action: 'service.buy', target: 'stock:sword', count: 1 });
    clickService(h, 'sold:7');
    assert.deepEqual(claims().value.data, { action: 'service.buy', target: 'sold:7', count: 1 });
    clickService(h, 'sell-3');
    assert.deepEqual(claims().value.data, { action: 'service.sell', target: '3' });
    clickService(h, 'identify-3');
    assert.deepEqual(claims().value.data, { action: 'service.identify', target: '3' });
    clickService(h, 'repair-3');
    assert.deepEqual(claims().value.data, { action: 'service.repair', target: '3' });
    clickService(h, 'Sword@1');
    assert.deepEqual(claims().value.data, { action: 'service.teach', target: 'Sword', tier: 1, member: 0 });

    // A lesson that grants a rung of a skill is its own row, and the rung the player pressed is the rung the
    // command names: two lessons of one skill are two rows rather than one ambiguous one.
    clickService(h, 'Sword@2');
    assert.deepEqual(claims().value.data, { action: 'service.teach', target: 'Sword', tier: 2, member: 0 });

    // The lesson goes to the member the picker shows, and the picker is offered only when there is a choice.
    const select = h.panel().querySelector('.crawler-service select');
    select.value = '1';
    clickService(h, 'Sword@1');
    assert.deepEqual(claims().value.data, { action: 'service.teach', target: 'Sword', tier: 1, member: 1 });

    const leave = [...h.panel().querySelectorAll('.crawler-service .crawler-actions button')].find(
      (button) => button.textContent === 'Leave the counter',
    );
    leave.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    assert.deepEqual(claims().value.data, { action: 'service.leave' });

    // Every claim went out on the intent and contract the product declares, exactly as the creation screen's
    // choices do: a command on any other channel is one the product never reads.
    for (const claim of h.claims) {
      assert.equal(claim.intent, ACTION_INTENT);
      assert.equal(claim.value.kind, 'product-payload');
      assert.equal(claim.value.contract, ACTION_CONTRACT);
    }

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel shows what a transaction did and why a refusal refused', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    h.emit(snapshot('running', 1, 60, 60, movement(), {
      service: openShop({
        action: 'buy',
        outcome: 'applied',
        message: 'The party buys 1 × A fine sword for 110 coin(s), and 1 are left.',
        paid: 110,
        coins: 90,
      }),
    }));
    assert.equal(h.panel().getAttribute('data-service-action'), 'buy');
    assert.equal(h.panel().getAttribute('data-service-outcome'), 'applied');
    const bought = servicePanel(h);
    assert.equal(bought.messageOutcome, 'applied');
    assert.equal(bought.message, 'The party buys 1 × A fine sword for 110 coin(s), and 1 are left. Paid 110; the purse holds 90.');

    // A refusal names what stopped it: the code and the product's own sentence reach the screen unchanged,
    // and the purse did not move.
    h.emit(snapshot('running', 2, 120, 121, movement(), {
      service: openShop({
        action: 'buy',
        outcome: 'refused',
        code: 'purse-short',
        message: 'The price is 110 coin(s) and the party holds 40 coin(s): 70 coin(s) short.',
        coins: 40,
      }),
    }));
    assert.equal(h.panel().getAttribute('data-service-outcome'), 'refused');
    const refused = servicePanel(h);
    assert.equal(refused.messageOutcome, 'refused');
    assert.equal(refused.messageCode, 'purse-short');
    assert.match(refused.message, /70 coin\(s\) short/);

    // A sale earns rather than pays, and the screen says which way the coin went.
    h.emit(snapshot('running', 3, 180, 182, movement(), {
      service: openShop({
        action: 'sell',
        outcome: 'applied',
        message: 'The party sells 1 × shield for 12 coin(s), and the counter will sell it back.',
        earned: 12,
        coins: 52,
      }),
    }));
    assert.match(servicePanel(h).message, /Received 12; the purse holds 52/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel renders the stop controls and every way a stop can end', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A session that holds a rest mechanism but has not stopped yet: the controls are there, and the panel
    // says which one would do what. A rest that healed, and what the night cost, are written out in full.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      clock: clock({ time: '18:00' }),
      party: party({ provisions: 4, hitPoints: 40, hitPointsMax: 40, spellPoints: 10, spellPointsMax: 10 }),
      rest: rest({
        kind: 'rest',
        outcome: 'applied',
        message: 'The party rests for 8 hour(s), to 1168-01-01 18:00, and spends 2 portions: every member is restored (1).',
        from: '1168-01-01 10:00',
        to: '1168-01-01 18:00',
        elapsedSeconds: 28800,
        charged: 2,
        covered: 2,
        unit: 'portions',
        recovered: true,
        restored: 1,
        cleared: 'weak',
      }),
    }));
    assert.equal(h.panel().getAttribute('data-rest'), 'applied');
    assert.equal(h.panel().getAttribute('data-rest-action'), 'rest');
    const rested = restPanel(h);
    assert.equal(rested.hidden, false);
    assert.equal(rested.messageOutcome, 'applied');
    assert.match(rested.message, /every member is restored/);
    assert.match(rested.message, /Cost 2 of 2 portions/);
    assert.match(rested.message, /Cleared: weak/);
    assert.deepEqual(
      rested.controls.map((entry) => entry.id),
      ['rest.rest', 'rest.camp', 'rest.wait-dawn', 'rest.wait-hour', 'rest.wait-five-minutes'],
    );
    assert.deepEqual(
      rested.controls.map((entry) => entry.text),
      ['Rest & heal 8 hours', 'Make camp', 'Wait until dawn', 'Wait an hour', 'Wait 5 minutes'],
    );

    // A wait moves the clock and restores nobody: the panel says so rather than leaving the two looking the
    // same, and it charges nothing.
    h.emit(snapshot('running', 2, 120, 121, movement(), {
      clock: clock({ time: '19:00' }),
      rest: rest({
        kind: 'wait-hour',
        outcome: 'applied',
        message: 'The party waits for 1 hour(s), to 1168-01-01 19:00. Waiting rests nobody: the clock moved and nothing was restored.',
        from: '1168-01-01 18:00',
        to: '1168-01-01 19:00',
        elapsedSeconds: 3600,
      }),
    }));
    assert.equal(h.panel().getAttribute('data-rest-action'), 'wait-hour');
    assert.match(restPanel(h).message, /Waiting rests nobody/);

    // A refusal keeps the product's own code and sentence, so a party that could not sleep reads why.
    h.emit(snapshot('running', 3, 180, 182, movement(), {
      rest: rest({
        kind: 'camp',
        outcome: 'refused',
        code: 'camp-hostiles-near',
        message: 'There are 3 hostile creature(s) within 5120 of the party, and it will not make camp with them near.',
      }),
    }));
    assert.equal(h.panel().getAttribute('data-rest-outcome'), 'refused');
    const refused = restPanel(h);
    assert.equal(refused.messageOutcome, 'refused');
    assert.equal(refused.messageCode, 'camp-hostiles-near');
    assert.match(refused.message, /will not make camp/);

    // A broken night is an applied stop with a shorter period and nothing spent, and the fatigue line is the
    // clock's own deadline — which is how a player sees when the party next needs to sleep.
    h.emit(snapshot('running', 4, 240, 243, movement(), {
      clock: clock({ time: '22:05' }),
      party: party({ conditions: 'weak (1)', provisions: 6 }),
      rest: rest({
        kind: 'camp',
        outcome: 'applied',
        interrupted: true,
        message: 'The party camps for 1 hour(s) 5 minute(s) and the night is broken at 1168-01-01 22:05: creatures find the camp. Nothing was restored and no provisions were spent.',
        from: '1168-01-01 21:00',
        to: '1168-01-01 22:05',
        elapsedSeconds: 3900,
        tired: true,
        fatigueDue: '1168-01-02 22:05',
        fatigueLanded: 1,
      }),
    }));
    const broken = restPanel(h);
    assert.equal(h.panel().getAttribute('data-tired'), 'yes');
    assert.match(broken.status, /Tired/);
    assert.match(broken.status, /next sleep due 1168-01-02 22:05/);
    assert.match(broken.status, /landed 1×/);
    assert.match(broken.message, /Nothing was restored and no provisions were spent/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('every stop control asks for the stop it was shown', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(snapshot('running', 1, 60, 60, movement(), { rest: rest() }));

    for (const action of ['rest.rest', 'rest.camp', 'rest.wait-dawn', 'rest.wait-hour', 'rest.wait-five-minutes']) {
      clickStop(h, action);
    }

    assert.deepEqual(
      h.claims.map((entry) => entry.value.data.action),
      ['rest.rest', 'rest.camp', 'rest.wait-dawn', 'rest.wait-hour', 'rest.wait-five-minutes'],
    );
    for (const claim of h.claims) {
      assert.equal(claim.intent, ACTION_INTENT);
      assert.equal(claim.value.kind, 'product-payload');
      assert.equal(claim.value.contract, ACTION_CONTRACT);
    }

    // A session with no mechanism offers no stop to ask for, and the section says the mechanism is not there
    // rather than showing five controls that would do nothing.
    h.emit(snapshot('running', 2, 120, 121, movement(), { rest: rest({ available: false }) }));
    assert.equal(h.panel().getAttribute('data-rest'), 'none');
    assert.equal(restPanel(h).hidden, true);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel shows the hours a place keeps, the hour it stands at, and what the party has left', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // The place's own hours are the product's clock read: the panel prints the window, whether the doors
    // stand open, and when they next change, and derives none of it.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      clock: clock({ time: '23:00', daylight: 'night' }),
      party: party({ hitPoints: 25, hitPointsMax: 40, spellPoints: 4, spellPointsMax: 10 }),
      rest: rest({ tired: true, fatigueDue: '', fatigueLanded: 1 }),
    }));
    const values = rows(h, 'Vitality', 'Magic', 'Hours');
    assert.equal(values.Vitality, '25 / 40');
    assert.equal(values.Magic, '4 / 10');
    assert.equal(values.Hours, '—');

    const withHours = snapshot('running', 2, 120, 121, movement(), {
      clock: clock({ time: '23:00', daylight: 'night' }),
      party: party({ hitPoints: 25, hitPointsMax: 40, spellPoints: 4, spellPointsMax: 10 }),
      rest: rest({ tired: true, fatigueDue: '', fatigueLanded: 1 }),
    });
    withHours.world = world({ open: false, hours: '06:00–18:00', nextChange: '1168-01-02 06:00' });
    h.emit(withHours);

    const shown = rows(h, 'Vitality', 'Magic', 'Hours');
    assert.equal(shown.Vitality, '25 / 40');
    assert.equal(shown.Magic, '4 / 10');
    assert.equal(shown.Hours, '06:00–18:00 · closed · next 1168-01-02 06:00');
    assert.equal(h.panel().getAttribute('data-tired'), 'yes');
    assert.match(restPanel(h).status, /^Tired · landed 1×$/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel renders the conversation the product published, with what it offers and withholds', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    ui; // The companion publishes nothing of its own; the projection below is the whole subject.

    // A session with no conversation mechanism, one that is speaking with nobody, and one that is talking
    // are three different facts, and the panel tells them apart before it renders anything.
    h.emit(snapshot('running', 1, 60, 60, movement(), { conversation: conversation({ available: false }) }));
    assert.equal(conversationPanel(h).state, 'none');
    assert.equal(conversationPanel(h).hidden, true);

    h.emit(snapshot('running', 1, 60, 60, movement(), { conversation: conversation() }));
    assert.equal(conversationPanel(h).state, 'available');
    assert.equal(conversationPanel(h).hidden, true);

    // What the person says, who is here, what may be brought up, and what the state withholds with the
    // reason: every word of it came from the projection and the panel spells none of it itself.
    h.emit(snapshot('running', 1, 60, 60, movement(), { conversation: talking() }));
    const shown = conversationPanel(h);
    assert.equal(shown.state, 'open');
    assert.equal(shown.hidden, false);
    assert.equal(shown.head, 'Speaking with Tester Two');
    assert.equal(shown.greeting, "'A fine day for it.'");
    assert.deepEqual(shown.people, [{ id: 'np-2', text: 'Tester Two', disabled: true }]);
    assert.deepEqual(shown.topics, [{ id: 'topic-1', text: 'The contest', disabled: false }]);
    assert.deepEqual(shown.withheld, ['The errand — the errand the table calls 7 is not finished']);
    assert.deepEqual(shown.said, [
      "'A fine day for it.' the event programs behind a reply are not run",
    ]);
    assert.deepEqual(shown.leave, ['Take your leave']);
    assert.equal(shown.message, "Tester Two: 'A fine day for it.'");
    assert.equal(shown.messageOutcome, 'applied');
  } finally {
    h.restore();
  }
});

test('a refusal in a conversation is shown with its code and what the answer could not deliver', () => {
  const h = harness();
  try {
    mountProductUi(h.root, h.context);

    // What was said, the residue beside it, and the refusal's own code: a topic the state withholds is an
    // answer a player acts on rather than a button that quietly did nothing.
    h.emit(
      snapshot('running', 1, 60, 60, movement(), {
        conversation: talking({
          action: 'say',
          topic: 'topic-2',
          outcome: 'refused',
          code: 'conversation-topic-withheld',
          message: 'Tester Two does not bring up The errand yet: the errand the table calls 7 is not finished.',
        }),
      }),
    );
    let shown = conversationPanel(h);
    assert.equal(shown.outcome, 'refused');
    assert.equal(shown.messageCode, 'conversation-topic-withheld');
    assert.equal(shown.messageOutcome, 'refused');

    h.emit(
      snapshot('running', 2, 120, 121, movement(), {
        conversation: talking({
          action: 'say',
          topic: 'topic-1',
          message: "Tester Two: 'The first to bring the items wins.'",
          residue: 'the event programs behind a reply are not run',
        }),
      }),
    );
    shown = conversationPanel(h);
    assert.equal(shown.residue, 'the event programs behind a reply are not run');
  } finally {
    h.restore();
  }
});

test('every conversation control asks for the choice it was shown, on the product contract', () => {
  const h = harness();
  try {
    mountProductUi(h.root, h.context);

    // A household of two: the person speaking is shown as chosen, the other is a button that turns the
    // conversation, and a topic is a button that takes it. Nothing here is decided by the screen.
    h.emit(
      snapshot('running', 1, 60, 60, movement(), {
        conversation: talking({
          speaker: 'Mira',
          people: [
            { id: 'mira', name: 'Mira', portrait: '709', speaking: true },
            { id: 'simon', name: 'Simon', portrait: '707', speaking: false },
          ],
          topics: [
            { id: 'topic-1', label: 'The contest', available: true, reason: '' },
            { id: 'topic-2', label: 'The errand', available: false, reason: 'the errand the table calls 7 is not finished' },
          ],
        }),
      }),
    );

    clickTopic(h, 'topic-1');
    clickPerson(h, 'simon');
    clickFlow(h, 'Take your leave');

    assert.deepEqual(
      h.claims.map((claim) => claim.value.data),
      [
        { action: 'conversation.topic', target: 'topic-1' },
        { action: 'conversation.person', target: 'simon' },
        { action: 'conversation.leave' },
      ],
    );
    for (const claim of h.claims) {
      assert.equal(claim.intent, ACTION_INTENT);
      assert.equal(claim.value.kind, 'product-payload');
      assert.equal(claim.value.contract, ACTION_CONTRACT);
    }

    // A topic the state withholds is shown disabled, so a choice that cannot be made cannot be pressed.
    const withheld = [...h.panel().querySelectorAll('.crawler-conversation .crawler-options button')].find(
      (button) => button.dataset.id === 'topic-2',
    );
    assert.equal(withheld.disabled, true);
  } finally {
    h.restore();
  }
});

test('the panel renders the fight, who may act, and what the party did', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A session with no fight mechanism at all says so by hiding the section: a quiet street and a product
    // that cannot fight are different facts, and the panel must not make them look alike.
    h.emit(snapshot('running', 0, 0, 1, movement()));
    assert.equal(combatPanel(h).hidden, true);

    // A fight that has been read but is not engaged: the party's readiness is shown and nothing is hostile.
    h.emit(snapshot('running', 1, 60, 60, movement(), { combat: combat() }));
    assert.equal(h.panel().getAttribute('data-combat'), 'quiet');
    assert.equal(h.panel().getAttribute('data-combat-opposition'), '0');
    const quiet = combatPanel(h);
    assert.equal(quiet.hidden, false);
    assert.match(quiet.status, /Nobody is hostile — 2 of 2 ready/);
    assert.deepEqual(quiet.members.map((member) => member.ready), ['yes', 'yes']);
    assert.equal(quiet.attack.disabled, false);
    assert.equal(quiet.enemies.length, 0);

    // A fight in progress: the creature is engaged, both members are recovering and say how long they owe,
    // and the panel reports the answer the product gave rather than one of its own.
    h.emit(snapshot('running', 2, 120, 121, movement(), { combat: fighting() }));
    assert.equal(h.panel().getAttribute('data-combat'), 'engaged');
    assert.equal(h.panel().getAttribute('data-combat-ready'), '0');
    assert.equal(h.panel().getAttribute('data-combat-outcome'), 'applied');
    const engaged = combatPanel(h);
    assert.match(engaged.status, /Engaged with 1 — 0 of 2 ready/);
    assert.deepEqual(engaged.members.map((member) => member.ready), ['no', 'no']);
    assert.match(engaged.members[0].name, /Roderick — recovering 23\.0s/);
    assert.match(engaged.enemies[0].name, /A beast — ready at 100/);
    assert.equal(engaged.messageOutcome, 'applied');
    assert.match(engaged.message, /attacks A beast/);

    // The enemy row says what the creature is doing, which is the product's own word for it rather than a
    // guess the screen makes from a position it saw change.
    assert.equal(engaged.enemies[0].activity, 'attacking');

    // A fight is two-sided: the panel says whether the last order was the party's own or a creature's, so a
    // wound the party took does not read as the party's own doing.
    assert.equal(engaged.byParty, 'yes');
    h.emit(snapshot('running', 4, 240, 245, movement(), { combat: struck() }));
    const incoming = combatPanel(h);
    assert.equal(incoming.byParty, 'no');
    assert.match(incoming.message, /A beast attacks Roderick/);
    assert.match(incoming.message, /Roderick is at 33\/40/);

    // A recovering party cannot act from the panel: the control is disabled while the product says no member
    // may act, which is the fight's own readiness rather than a countdown this screen runs.
    assert.equal(engaged.attack.disabled, true);

    // An order refused while everybody recovers keeps the product's code and sentence.
    h.emit(snapshot('running', 3, 180, 182, movement(), {
      combat: fighting({
        outcome: 'refused',
        code: 'recovering',
        message: 'Roderick is still recovering: 21969ms of game time must pass before it can act again.',
        recoverySeconds: 0,
      }),
    }));
    const refused = combatPanel(h);
    assert.equal(h.panel().getAttribute('data-combat-outcome'), 'refused');
    assert.equal(refused.messageOutcome, 'refused');
    assert.equal(refused.messageCode, 'recovering');
    assert.match(refused.message, /still recovering/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel shows the pools, the conditions, and the death the product published', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // What a landed attack came to, in the product's own numbers: the chance it stated, the dice it rolled,
    // what the resistance took off, and what was left. Nothing here is derived by the panel.
    h.emit(snapshot('running', 1, 60, 60, movement(), { combat: fighting() }));
    const hit = combatPanel(h);
    assert.equal(hit.resolved, 'yes');
    assert.equal(hit.hit, 'yes');
    assert.equal(hit.chance, '4595');
    assert.equal(hit.damageRolled, '5');
    assert.equal(hit.damage, '5');
    assert.equal(hit.damageKind, 'Phys');
    assert.equal(hit.resistance, '0');
    assert.equal(hit.targetDown, 'no');
    assert.match(hit.message, /A beast is at 14\/40/);

    // Every actor carries what it has left and what is acting on it, and an immune target reads as one:
    // the panel's row is the product's numbers rather than a bar this screen filled in for itself.
    assert.equal(hit.enemies[0].health, '14/40');
    assert.equal(hit.members[0].health, '40/40');
    assert.equal(hit.members[0].conditions, '');
    assert.equal(hit.members[0].down, 'no');

    // A member who is unconscious, poisoned, and down is shown as that, and a death is a row that says so
    // rather than a row that disappears.
    h.emit(snapshot('running', 2, 120, 121, movement(), {
      combat: fighting({
        members: [
          {
            id: 'member:1', name: 'Roderick', ready: false, recoverySeconds: 22.969, distance: 0,
            hitPoints: 0, hitPointsMax: 40, conditions: 'Unconscious', down: true,
          },
          {
            id: 'member:2', name: 'Aelina', ready: false, recoverySeconds: 22.266, distance: 0,
            hitPoints: 0, hitPointsMax: 24, conditions: 'Dead', down: true,
          },
        ],
        enemies: [],
        opposition: 0,
        engaged: false,
        condition: 'poisoned (1)',
        damage: 0,
        damageRolled: 12,
        resistance: 'immune',
        targetDown: true,
        message: 'A beast hits Roderick (melee): Roderick is immune to Poison, so the 12 rolled lands for nothing; Roderick is at 0/40 and is left poisoned (the bite of a test creature), and is down.',
      }),
    }));
    const hurt = combatPanel(h);
    assert.equal(hurt.members[0].health, '0/40');
    assert.equal(hurt.members[0].conditions, 'Unconscious');
    assert.equal(hurt.members[0].down, 'yes');
    // A member the fight has laid out reads as down rather than as recovering toward an act: the row's own
    // word is the product's readiness, and being out of the fight outranks the recovery it still owes, so a
    // row for somebody who cannot act never reads as a light that is about to come on.
    assert.match(hurt.members[0].name, /Roderick — down — 0\/40 hp — Unconscious/);
    assert.equal(hurt.members[1].conditions, 'Dead');
    assert.equal(hurt.members[1].down, 'yes');
    assert.match(hurt.members[1].name, /Aelina .*Dead/);
    assert.equal(hurt.enemies.length, 0);
    assert.equal(hurt.resistance, 'immune');
    assert.equal(hurt.condition, 'poisoned (1)');
    assert.equal(hurt.targetDown, 'yes');
    assert.match(hurt.message, /is immune to Poison/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the fight control asks for the attack it was shown, on the product contract', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(snapshot('running', 1, 60, 60, movement(), { combat: combat() }));

    combatPanel(h);
    h.panel().querySelector('.crawler-attack').click();

    assert.deepEqual(
      h.claims.map((claim) => claim.value.data),
      [{ action: 'party.attack' }],
    );
    assert.equal(h.claims[0].intent, ACTION_INTENT);
    assert.equal(h.claims[0].value.kind, 'product-payload');
    assert.equal(h.claims[0].value.contract, ACTION_CONTRACT);

    // The keyboard hint names the key the product declared, so a player who never presses the button knows
    // the control exists.
    assert.match(h.panel().textContent, /Attack with the button or the B key/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel renders the pacing, whose turn it is, and what is left to act', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    // Real time first: there is a fight and no round, which the panel says in the product's own words — and
    // the turn controls are there but not offered, because there is no turn of the player's to pass.
    h.emit(snapshot('running', 1, 60, 60, movement(), { combat: fighting() }));
    const real = combatPanel(h);
    assert.equal(real.pacing, 'realtime');
    assert.equal(real.phase, 'none');
    assert.match(real.turn, /Real time/);
    assert.equal(real.skip.disabled, true);
    assert.equal(real.wait.disabled, true);
    assert.equal(real.pace.text, 'Turn-based');

    // A paced fight: the round, the actor whose turn it is, and what each actor in the order still owes —
    // every number the product published, and none of it counted down by this screen.
    h.emit(snapshot('turnbased', 2, 120, 121, movement(), { combat: paced() }));
    assert.equal(h.panel().getAttribute('data-mode'), 'turnbased');
    const pacedPanel = combatPanel(h);
    const turn = pacedPanel;
    assert.equal(turn.pacing, 'turnbased');
    assert.equal(turn.phase, 'action');
    assert.equal(turn.round, '2');
    assert.equal(turn.turnActor, 'Roderick');
    assert.equal(turn.playerTurn, 'yes');
    assert.equal(turn.last, 'skip');
    assert.match(turn.turn, /Round 2 — Roderick's turn \(yours\)/);
    assert.match(turn.turn, /23\.4s round/);
    assert.match(turn.turn, /last turn skip/);
    assert.equal(turn.pace.text, 'Real-time');
    assert.equal(turn.skip.disabled, false);
    assert.equal(turn.wait.disabled, false);

    // The order is rendered as the fight read it, with the amount each actor still owes and the row whose
    // turn it is saying so.
    assert.equal(turn.order.length, 3);
    assert.deepEqual(
      turn.order.map((actor) => [actor.name, actor.side, actor.ready, actor.current]),
      [
        ['Roderick — ready — this turn', 'party', 'yes', 'yes'],
        ['Aelina — 1.5s', 'party', 'no', 'no'],
        ['A beast — 1.0s', 'opposition', 'no', 'no'],
      ],
    );
    assert.equal(turn.order[0].remaining, '0.000');
    assert.equal(turn.order[2].remaining, '1.000');

    // A deferred turn says so, and the movement phase says what is left of it: the panel prints the round
    // the product published rather than working out a phase of its own.
    h.emit(snapshot('running', 3, 180, 182, movement(), {
      combat: paced({
        turn: round({
          phase: 'movement',
          round: 2,
          movementSeconds: 6.5,
          order: [ordered({ waiting: true })],
        }),
      }),
    }));
    const moving = combatPanel(h);
    assert.equal(moving.phase, 'movement');
    assert.match(moving.turn, /round 2, 6\.5s of movement left/);
    assert.equal(moving.order[0].waiting, 'yes');

    // A fight that is paced with nothing hostile says exactly that, rather than showing a round that is not
    // under way.
    h.emit(snapshot('running', 4, 240, 245, movement(), { combat: combat({ pacing: 'turnbased' }) }));
    assert.match(combatPanel(h).turn, /nothing is being fought/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('every pace control asks for the act it was shown, on the product contract', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(snapshot('turnbased', 1, 60, 60, movement(), { combat: paced() }));
    const section = h.panel().querySelector('.crawler-combat');
    section.querySelector('.crawler-attack').click();
    section.querySelector('.crawler-pace').click();
    section.querySelector('.crawler-skip').click();
    section.querySelector('.crawler-wait').click();

    // One control per act, and each asks for exactly the action the product declared: the act control is the
    // same control in both pacings, and the pacing and the two turn actions are the names the product
    // declared as intents and reads off this contract.
    assert.deepEqual(
      h.claims.map((claim) => claim.value.data),
      [
        { action: 'party.attack' },
        { action: 'combat.turn-based' },
        { action: 'combat.turn-skip' },
        { action: 'combat.turn-wait' },
      ],
    );
    assert.equal(h.claims[0].intent, ACTION_INTENT);
    assert.equal(h.claims[0].value.contract, ACTION_CONTRACT);

    // The hint names the keys the product declared, so a player who never presses a button knows the
    // controls exist.
    assert.match(h.panel().textContent, /Enter key/);
    assert.match(h.panel().textContent, /skip a turn with K/);
    assert.match(h.panel().textContent, /wait with Y/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a paced fight is a running session, so the pause control stays the one it has', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(snapshot('turnbased', 1, 60, 60, movement(), { combat: paced() }));
    const control = [...h.panel().querySelectorAll('button')].find(
      (button) => button.textContent === 'Pause session',
    );
    assert.notEqual(control, undefined);
    assert.equal(control.disabled, false);

    control.click();
    assert.deepEqual(h.claims.map((claim) => claim.value.data), [{ action: 'session.pause' }]);

    // And a held session offers the release instead, so the same control always says what it will do.
    h.emit(snapshot('paused', 2, 120, 121, movement(), { combat: paced() }));
    const released = [...h.panel().querySelectorAll('button')].find(
      (button) => button.textContent === 'Resume session',
    );
    assert.notEqual(released, undefined);
    assert.equal(released.disabled, false);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the fight controls are offered exactly when the product would take the order', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // Real time with everybody recovering: the product refuses an order now, so the panel does not offer one,
    // and there is no round for the two turn actions to pass a turn in.
    h.emit(snapshot('running', 1, 60, 60, movement(), { combat: fighting() }));
    const recovering = combatPanel(h);
    assert.equal(recovering.ready, '0');
    assert.equal(recovering.attack.disabled, true);
    assert.equal(recovering.skip.disabled, true);
    assert.equal(recovering.wait.disabled, true);

    // Real time with somebody able to act: the same control is offered, because the product would take it.
    h.emit(snapshot('running', 2, 120, 121, movement(), { combat: combat() }));
    assert.equal(combatPanel(h).attack.disabled, false);

    // A paced fight with a round under way: one press spends the turn, so the control follows the round the
    // product published rather than the members' readiness — and the two turn actions are offered with it.
    h.emit(snapshot('turnbased', 3, 180, 182, movement(), { combat: paced({ ready: 0 }) }));
    const holding = combatPanel(h);
    assert.equal(holding.phase, 'action');
    assert.equal(holding.attack.disabled, false);
    assert.equal(holding.skip.disabled, false);
    assert.equal(holding.wait.disabled, false);

    // The party's movement phase: the act control ends it, so it stays offered with nobody ready.
    h.emit(snapshot('turnbased', 4, 240, 245, movement(), {
      combat: paced({
        ready: 0,
        turn: round({ phase: 'movement', round: 2, movementSeconds: 6.5, order: [ordered({ waiting: true })] }),
      }),
    }));
    const moving = combatPanel(h);
    assert.equal(moving.attack.disabled, false);
    assert.equal(moving.skip.disabled, false);
    assert.equal(moving.wait.disabled, false);

    // Turn-based pacing with nothing to pace: no round is under way, so the world steps as it does in real
    // time and every control follows readiness again. This is the state a panel that read the phase as an
    // empty string would offer an act in — and the product's answer there is a refusal reported where no
    // player can see it, which is exactly the silent control this panel exists to prevent.
    h.emit(snapshot('running', 5, 300, 306, movement(), { combat: fighting({ pacing: 'turnbased' }) }));
    const idle = combatPanel(h);
    assert.equal(idle.phase, 'none');
    assert.equal(idle.attack.disabled, true);
    assert.equal(idle.skip.disabled, true);
    assert.equal(idle.wait.disabled, true);
    assert.match(idle.turn, /Turn-based: nothing is being fought/);

    // The pacing toggle is the one control always offered while the mechanism is there: it asks for a pacing
    // rather than for an act, and the product can answer it whatever the fight is doing.
    assert.equal(idle.pace.disabled, false);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel echoes the fight it was published rather than working the fight out', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // Two members contradicting the arithmetic a screen might do: one that still owes game time and is
    // published as ready, and one that owes none and is published as unable to act. A panel that derived
    // readiness from the seconds — or ran a countdown of its own toward zero — would show the opposite of
    // both, which is exactly the second opinion the projection exists to prevent.
    h.emit(snapshot('running', 1, 60, 60, movement(), {
      combat: combat({
        ready: 1,
        members: [
          { ...combat().members[0], ready: true, recoverySeconds: 12.5 },
          { ...combat().members[1], ready: false, recoverySeconds: 0 },
        ],
      }),
    }));
    const readied = combatPanel(h);
    assert.equal(readied.ready, '1');
    assert.equal(readied.members[0].ready, 'yes');
    assert.match(readied.members[0].name, /Roderick — ready/);
    assert.equal(readied.members[1].ready, 'no');
    assert.match(readied.members[1].name, /Aelina — recovering 0\.0s/);
    // One member may act, so the act control is offered: the count, the rows, and the control are one fact.
    assert.equal(readied.attack.disabled, false);

    // The same for the aggro half of the ready light: what is hostile, and how much of it, is the product's
    // own count rather than the length of the list the panel happens to have been sent.
    h.emit(snapshot('running', 2, 120, 121, movement(), {
      combat: combat({ engaged: true, opposition: 3, enemies: [] }),
    }));
    const engaged = combatPanel(h);
    assert.equal(engaged.state, 'engaged');
    assert.equal(engaged.opposition, '3');
    assert.match(engaged.status, /Engaged with 3 — 2 of 2 ready/);
    assert.equal(engaged.enemies.length, 0);

    h.emit(snapshot('running', 3, 180, 182, movement(), {
      combat: combat({ engaged: false, opposition: 2, enemies: [combat().members[0]] }),
    }));
    const quiet = combatPanel(h);
    assert.equal(quiet.state, 'quiet');
    assert.match(quiet.status, /Nobody is hostile — 2 of 2 ready/);
    assert.equal(quiet.enemies.length, 1);

    // A creature that is down says so once: the fight's own state word and the word its driver reports for
    // what it is doing are the same fact there, and a row that printed both would repeat itself.
    h.emit(snapshot('running', 4, 240, 245, movement(), {
      combat: combat({
        enemies: [
          {
            id: 'actor:1', name: 'A beast', ready: false, recoverySeconds: 0, distance: 100,
            hitPoints: 0, hitPointsMax: 40, conditions: '', down: true, activity: 'down',
          },
        ],
      }),
    }));
    assert.equal(combatPanel(h).enemies[0].name, 'A beast — down at 100 — 0/40 hp');

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the companion reaches for no clock and computes no combat quantity', async () => {
  const source = await readFile(new URL('../../src/ui/main.ts', import.meta.url), 'utf8');

  // The fight is paced by game time the product measures. A screen that reached for a clock or a timer would
  // be running a second pacing — and would show a character ready before the fight agreed, which is the
  // failure publishing the recovery rather than counting it down exists to prevent.
  for (const forbidden of [
    'setTimeout(',
    'setInterval(',
    'requestAnimationFrame(',
    'requestIdleCallback(',
    'queueMicrotask(',
    'Date.now',
    'new Date',
    'performance.now',
  ]) {
    assert.equal(source.includes(forbidden), false, `the companion reaches for ${forbidden}`);
  }

  // The fight's own rendering is the only place a combat value is touched, and there each is read, compared,
  // and printed — never combined. A rule appearing there looks like an operator between a combat quantity and
  // anything else, which is what this scan fails on. Formatting a value (`toFixed`), reading the length of a
  // published list, and comparing a value (`> 0`, `=== 'action'`) are all presentation, and stay allowed.
  const fight = source.slice(source.indexOf('const renderCombat'), source.indexOf('const render = ('));
  assert.notEqual(fight.length, 0, 'the companion no longer renders a fight at all');
  // A quantity is named as the projection spells it, however it was reached — `view.ready`, `actor.hitPoints`.
  const quantity =
    '(?:[A-Za-z_$][\\w$]*\\.)*(?:recoverySeconds|remainingSeconds|roundSeconds|movementSeconds|dueSeconds|elapsedSeconds|hitPoints|hitPointsMax|distance|damage|damageRolled|chance|opposition|ready|down|engaged|playerTurn)';
  const patterns = [
    new RegExp(`${quantity}\\s*[-+*/%]`),
    new RegExp(`[-+*/%]\\s*${quantity}`),
    new RegExp(`Math\\.[a-z]+\\([^)]*${quantity}`),
  ];
  // A template's `${...}` placeholders hold the reads themselves, and the separator between two of them —
  // `${hitPoints}/${hitPointsMax}` — is a slash this scan would otherwise read as division. They are replaced
  // by a single marker first, so what is left is the code around them; each placeholder's own body is then
  // scanned the same way, so a rule cannot hide inside an interpolation either.
  const code = fight.replaceAll(/\$\{[^}]*\}/g, '_');
  for (const pattern of patterns) {
    const match = pattern.exec(code);
    assert.equal(match, null, `the fight rendering computes a combat quantity: ${match?.[0] ?? ''}`);
    for (const placeholder of fight.matchAll(/\$\{([^}]*)\}/g)) {
      const inside = pattern.exec(placeholder[1]);
      assert.equal(inside, null, `a placeholder computes a combat quantity: ${inside?.[0] ?? ''}`);
    }
  }
});

test('the panel renders what each member earned and what a level would cost', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A session with no progression owner says so by hiding the section: a party that has earned nothing
    // and a product that owns no experience at all are different facts.
    h.emit(snapshot('running', 0, 0, 1, movement()));
    assert.equal(progressionPanel(h).hidden, true);

    // A party that has earned nothing: the level, the experience against the curve, the points, and no
    // train control, because no counter here trains anybody.
    h.emit(snapshot('running', 1, 60, 60, movement(), { progression: progression() }));
    assert.equal(h.panel().getAttribute('data-progression'), 'present');
    assert.equal(h.panel().getAttribute('data-progression-outcome'), 'none');
    const fresh = progressionPanel(h);
    assert.equal(fresh.hidden, false);
    assert.deepEqual(fresh.members.map((member) => member.label), ['Roderick — level 1 — 0/1000 xp — 0 skill points']);
    assert.equal(fresh.members[0].train, null);

    // A member who has banked the experience stands at a hall that trains to five: the fee the counter
    // quoted and the ceiling content states are both printed, and the control names the level it would buy.
    h.emit(snapshot('running', 2, 120, 121, movement(), {
      progression: progression({
        outcome: 'awarded',
        source: 'kill',
        earned: 7000,
        message: 'The party earned 7000 experience from kill.',
        members: [
          {
            index: 0, member: '1', name: 'Roderick', level: 1, experience: 7000, skillPoints: 0,
            nextLevel: 1000, fee: 10, cap: 5,
          },
        ],
      }),
    }));
    assert.equal(h.panel().getAttribute('data-progression-outcome'), 'awarded');
    const earned = progressionPanel(h);
    assert.deepEqual(earned.members.map((member) => member.label), ['Roderick — level 1 — 7000/1000 xp — 0 skill points']);
    assert.match(earned.members[0].train, /Train to level 2 for 10 gold \(up to 5\)/);
    assert.match(earned.message, /earned 7000 experience from kill/);

    // The control asks for the training step it was shown, naming the member it belongs to, on the
    // product's own service contract.
    const train = progressionPanel(h).section.querySelector('.crawler-train');
    train.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    assert.deepEqual(h.claims.at(-1), {
      intent: 'crawler.ui',
      value: {
        kind: 'product-payload',
        contract: 'crawler.ui.action.v1',
        data: { action: 'service.train', member: 0 },
      },
    });

    // What a training step came to is the product's own report, including what the level granted, and a
    // refusal keeps its own code and sentence.
    h.emit(snapshot('running', 3, 180, 181, movement(), {
      progression: progression({
        outcome: 'trained',
        source: 'The Emerald Island Training Hall',
        message: 'Roderick reached level 2, gaining 5 hit point(s), 0 spell point(s), and 5 skill point(s).',
        members: [
          {
            index: 0, member: '1', name: 'Roderick', level: 2, experience: 7000, skillPoints: 5,
            nextLevel: 3000, fee: 20, cap: 5,
          },
        ],
      }),
    }));
    const trained = progressionPanel(h);
    assert.equal(trained.outcome, 'trained');
    assert.match(trained.message, /reached level 2/);
    assert.deepEqual(trained.members.map((member) => member.label), ['Roderick — level 2 — 7000/3000 xp — 5 skill points']);

    h.emit(snapshot('running', 4, 240, 241, movement(), {
      progression: progression({
        outcome: 'refused',
        code: 'progression-experience-short',
        message: 'Roderick needs 2000 more experience to train to level 3.',
        members: [
          {
            index: 0, member: '1', name: 'Roderick', level: 2, experience: 1000, skillPoints: 5,
            nextLevel: 3000, fee: 20, cap: 5,
          },
        ],
      }),
    }));
    const refused = progressionPanel(h);
    assert.equal(refused.outcome, 'refused');
    assert.equal(refused.code, 'progression-experience-short');
    assert.match(refused.message, /needs 2000 more experience/);

    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the panel renders every member\'s skills with their ceilings and what a raise would cost', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A session whose ruleset stated no skill policy shows nothing to raise — which is a different fact from
    // a party that has spent every point — and the panel says which of the two it is looking at.
    h.emit(snapshot('running', 1, 60, 60, movement()));
    assert.equal(h.panel().getAttribute('data-skills'), 'none');
    assert.equal(skillsPanel(h).hidden, true);

    h.emit(snapshot('running', 2, 120, 121, movement(), { skills: skills() }));
    assert.equal(h.panel().getAttribute('data-skills'), 'present');
    const one = skillsPanel(h);
    assert.equal(one.hidden, false);
    assert.deepEqual(one.members.map((member) => member.label), ['Roderick — Knight of rank 1']);
    // Everything the row reads came from the product: the level, the rung's own word, the ceiling the class
    // and rank impose, and the price the owner quoted for the next point.
    assert.deepEqual(one.members[0].skills, [
      {
        text: 'Sword · weapon · basic · 3/12 (master)',
        skill: 'Sword',
        block: 'weapon',
        tier: 'basic',
        refusal: '',
        raise: 'Raise to 4 for 4 points',
      },
    ]);

    // The control asks for exactly the raise it was shown, naming the member and the skill on that row, on
    // the product's own action contract: the product judges the ceiling and the price, the screen names the
    // row.
    const raise = one.section.querySelector('.crawler-raise');
    raise.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    assert.deepEqual(h.claims.at(-1), {
      intent: 'crawler.ui',
      value: {
        kind: 'product-payload',
        contract: 'crawler.ui.action.v1',
        data: { action: 'party.raise-skill', member: 0, skill: 'Sword' },
      },
    });

    // A row the product would refuse carries the refusal and no control at all: a button this panel offered
    // there would be one the product refuses, which is the shape the product's own plan exists to prevent.
    h.emit(snapshot('running', 3, 180, 181, movement(), {
      skills: skills({
        members: [
          {
            index: 0, member: '1', name: 'Roderick', class: 'Knight', rank: 1,
            skills: [
              {
                skill: 'Sword', block: 'weapon', level: 12, tier: 'master', ceilingLevel: 12,
                ceilingTier: 'master', pointsSpent: 78, reached: 13, cost: 0,
                refusal: 'Sword stands at level 12 and Roderick, a Knight of rank 1, may raise it to 12 and no further; a promotion raises the ceiling.',
              },
            ],
          },
        ],
      }),
    }));
    const capped = skillsPanel(h);
    assert.equal(capped.members[0].skills[0].raise, null);
    assert.match(capped.members[0].skills[0].text, /may raise it to 12 and no further/);
    assert.match(capped.members[0].skills[0].refusal, /a promotion raises the ceiling/);

    // What a raise did is the product's own report, and a refusal keeps its code and sentence.
    h.emit(snapshot('running', 4, 240, 241, movement(), {
      skills: skills({
        outcome: 'raised',
        member: 'Roderick',
        skill: 'Sword',
        level: 4,
        cost: 4,
        message: 'Roderick raised Sword to level 4 for 4 skill point(s), leaving 1.',
      }),
    }));
    const raised = skillsPanel(h);
    assert.equal(raised.outcome, 'raised');
    assert.equal(h.panel().getAttribute('data-skills-outcome'), 'raised');
    assert.match(raised.message, /raised Sword to level 4 for 4 skill point\(s\), leaving 1/);

    h.emit(snapshot('running', 5, 300, 301, movement(), {
      skills: skills({
        outcome: 'refused',
        code: 'insufficient-skill-points',
        message: 'Raising Sword to level 4 costs 4 skill point(s) and 1 remain unspent.',
      }),
    }));
    const refused = skillsPanel(h);
    assert.equal(refused.outcome, 'refused');
    assert.equal(refused.code, 'insufficient-skill-points');
    assert.match(refused.message, /1 remain unspent/);

    ui.dispose();
  } finally {
    h.restore();
  }
});


test('the panel renders every member\'s spellbook, casts the row a player pressed, and sets the quick spell', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);

    // A session whose ruleset stated no spell policy shows no spellbook at all — a different fact from a
    // party that has learned nothing — and the panel says which of the two it is looking at.
    h.emit(snapshot('running', 1, 60, 60, movement()));
    assert.equal(h.panel().getAttribute('data-magic'), 'none');
    assert.equal(magicPanel(h).hidden, true);

    h.emit(snapshot('running', 2, 120, 121, movement(), { magic: magic() }));
    assert.equal(h.panel().getAttribute('data-magic'), 'present');
    const one = magicPanel(h);
    assert.equal(one.hidden, false);
    assert.deepEqual(one.members.map((member) => member.label), [
      'Aelina — Sorcerer · 33/36 spell points · quick none',
    ]);

    // Everything a row reads came from the product: the spell, its school, the rung it asks for, what
    // casting it costs this caster, and what it is aimed at.
    assert.deepEqual(
      one.members[0].spells.map((spell) => spell.text),
      [
        'Fire Bolt · Fire · basic · 2 points · foe · damage',
        'Torch Light · Fire · basic · 1 point · party · light',
      ],
    );

    // The target list a row offers is the product's own: a spell aimed at an opponent offers the
    // opposition's actors and nothing else, and a spell that names nobody offers no target at all.
    assert.deepEqual(one.members[0].spells[0].targets, ['actor:9']);
    assert.deepEqual(one.members[0].spells[1].targets, []);

    // The cast control names the member, the spell, and the target the row was shown, on the product's own
    // action contract: the product resolves and judges the casting, the screen names the row.
    const cast = one.section.querySelector('.crawler-cast');
    cast.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    assert.deepEqual(h.claims.at(-1), {
      intent: 'crawler.ui',
      value: {
        kind: 'product-payload',
        contract: 'crawler.ui.action.v1',
        data: { action: 'party.cast', member: 0, spell: '2', target: 'actor:9' },
      },
    });

    // The quick-slot control sends the member and the spell its slot should hold, and the button reads as
    // clearing the slot when the row is the spell already in it.
    const quick = one.section.querySelector('.crawler-quick');
    quick.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
    assert.deepEqual(h.claims.at(-1).value.data, {
      action: 'party.quick-spell',
      member: 0,
      spell: '2',
    });

    // What a casting did is the product's own report, and a refusal keeps its code and sentence.
    h.emit(snapshot('running', 3, 180, 181, movement(), {
      magic: magic({
        outcome: 'cast',
        caster: 'Aelina',
        spell: '2',
        cost: 2,
        target: 'A beast',
        effect: 'damage',
        code: 'spell-effect-applied',
        message: 'Aelina casts Fire Bolt for 2 spell point(s): Aelina attacks A beast.',
      }),
    }));
    const cast2 = magicPanel(h);
    assert.equal(cast2.outcome, 'cast');
    assert.equal(h.panel().getAttribute('data-magic-outcome'), 'cast');
    assert.match(cast2.message, /casts Fire Bolt for 2 spell point\(s\)/);

    h.emit(snapshot('running', 4, 240, 241, movement(), {
      magic: magic({
        outcome: 'refused',
        code: 'spell-mastery-too-low',
        message: 'Fireball asks for a expert mastery of its school, and Aelina stands at basic.',
      }),
    }));
    const refused = magicPanel(h);
    assert.equal(refused.outcome, 'refused');
    assert.equal(refused.code, 'spell-mastery-too-low');
    assert.match(refused.message, /Aelina stands at basic/);

    ui.dispose();
  } finally {
    h.restore();
  }
});
