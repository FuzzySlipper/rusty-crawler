/**
 * The DOM companion's contract: it renders what the projection carries, reports the two session
 * actions it offers, owns no state, and starts no timer. The product half of the same round trip is
 * covered by tests/PartyRpg.Kit.Tests/SessionInputRouterTests.cs.
 */
import assert from 'node:assert/strict';
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
    coins: 200,
    provisions: 6,
    unit: 'portions',
    reputation: 0,
    fame: 0,
    conditions: '',
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
      values: Array(25).fill('—'),
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
        '—', '—', '—', '—', '—', '—', '—', '—',
        '1', '1234, 5678, 0 @ 512', '1 / 76', 'grounded', '—', '—', '—',
        '—', '—', '—',
        // A projection that carries no save block is a session this companion cannot read as saveable, and
        // the panel says so on the Save row rather than offering a save it cannot make.
        'unavailable', 'fresh',
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
      '—', '—', '—', '—', '—', '—', '—', '—',
      '1', '1234, 5678, 0 @ 512', '1 / 76', '—', '—', '—', '—',
      '—', '—', '—',
      'unavailable', 'fresh',
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
