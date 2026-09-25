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
    button: () => root.querySelector('.crawler-session button'),
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
      values: Array(20).fill('—'),
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
