/**
 * The contract between the product and its DOM companion, bound across the two languages.
 *
 * The fixtures under `fixtures/` are the product's own output: the host suite's ProjectionContractTests builds
 * them with `SessionProjection.Build` — and by creating and starting the product itself — and fails when the files
 * checked in here differ from what it builds (`CRAWLER_WRITE_UI_FIXTURES=1 dotnet test tests/PartyRpg.Host.Tests`
 * regenerates them). This suite mounts exactly those fixtures and fails when any reader meets a field that is
 * missing or of another type, so a field renamed on the C# side fails a test rather than quietly emptying a section;
 * it fails when the panel sends an action the product does not read; and it holds every companion module to the
 * rule that the panel prints what it was given and works none of it out.
 */
import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import { test } from 'node:test';

import { ACTIONS, UI_ACTION_CONTRACT, UI_ACTION_INTENT, UI_CONTRACT } from '../../src/ui/generated/actions.js';
import { mountCombat, readCombat } from '../../src/ui/generated/combat.js';
import { mountProductUi, readSnapshot } from '../../src/ui/generated/main.js';
import { fields } from '../../src/ui/generated/reader.js';
import { fixture, harness } from './harness.mjs';

const contract = await fixture('contract.json');
const SESSIONS = ['session-creating.json', 'session-running.json', 'session-turn-based.json', 'session-empty.json'];

/** Clicks every enabled button the panel holds, the way a player could, and returns what was claimed. */
function pressEverything(h) {
  for (const control of h.panel().querySelectorAll('button')) {
    if (!control.disabled) control.dispatchEvent(new h.dom.window.MouseEvent('click', { bubbles: true }));
  }

  return h.claims;
}

test('the fixtures directory holds exactly the files the host suite writes', async () => {
  const names = (await readdir(new URL('./fixtures/', import.meta.url))).sort();
  assert.deepEqual(names, ['contract.json', ...SESSIONS].sort());
});

for (const name of SESSIONS) {
  test(`every field of ${name} is one the panel reads, and the panel draws it without a problem`, async () => {
    const published = await fixture(name);
    assert.deepEqual(readSnapshot(published).problems, [], `${name} does not read cleanly`);

    const h = harness();
    try {
      const ui = mountProductUi(h.root, h.context);
      h.emit(published);
      assert.equal(h.panel().getAttribute('data-projection'), 'current');
      assert.equal(h.panel().getAttribute('data-problems'), '0');
      assert.deepEqual(h.problems(), []);
      assert.equal(h.panel().querySelector('.crawler-problems').hidden, true);
      assert.equal(h.panel().getAttribute('data-mode'), published.session.mode);

      // Every action a player can press on this projection is one the product reads, on the intent and payload
      // contract it declares.
      for (const claim of pressEverything(h)) {
        assert.equal(claim.intent, contract.actionIntent);
        assert.equal(claim.value.contract, contract.actionContract);
        assert.ok(contract.actions.includes(claim.value.data.action), `the panel sent '${claim.value.data.action}', which the product does not read`);
      }

      ui.dispose();
    } finally {
      h.restore();
    }
  });
}

test('the running fixture fills every section the product published, from its own numbers', async () => {
  const published = await fixture('session-running.json');
  const h = harness();
  try {
    mountProductUi(h.root, h.context);
    h.emit(published);
    const panel = h.panel();
    for (const section of ['conversation', 'service', 'rest', 'combat', 'progression', 'promotion', 'skills', 'magic', 'alchemy', 'equipment', 'map', 'journal', 'quests']) {
      assert.equal(panel.querySelector(`.crawler-${section}`).hidden, false, `the ${section} section is hidden`);
    }

    // What the product worked out is printed as it arrived: the level a training step reaches, the items a counter
    // would identify and mend, the actors a spell may name, and the marker's own corners.
    assert.match(panel.querySelector('.crawler-train').textContent, new RegExp(`Train to level ${published.progression.members[0].nextLevel} `));
    const offered = [...panel.querySelectorAll('.crawler-service .crawler-options button')].map((button) => button.dataset.id);
    for (const item of published.service.identify) assert.ok(offered.includes(`identify-${item.item}`));
    for (const item of published.service.repair) assert.ok(offered.includes(`repair-${item.item}`));
    for (const offer of published.service.offers.filter(offer => offer.kind !== 'debt')) {
      for (const choice of offer.choices) assert.ok(offered.includes(`${choice.operation}-${offer.subject}-${choice.member}`));
    }
    const bolt = panel.querySelector('.crawler-spell[data-spell="2"]');
    assert.deepEqual(
      [...bolt.querySelectorAll('.crawler-target option')].map((option) => option.value),
      published.magic.targets.filter((target) => target.side === 'opposition').map((target) => target.target),
    );
    assert.equal(panel.querySelector('.crawler-map-mark').getAttribute('r'), String(published.map.drawing.markRadius));
    assert.equal(
      panel.querySelector('.crawler-map-party').getAttribute('points'),
      '31.25,0 62.5,62.5 0,62.5',
    );

    // The hint names the keys the host's project binds, and no others.
    assert.match(panel.querySelector('.crawler-hint').textContent, new RegExp(`Save with the button, or with the ${published.controls.save.key} key`));
  } finally {
    h.restore();
  }
});

test('a field the product renamed is named as a problem, and its block is read as unknown rather than half-read', async () => {
  const published = await fixture('session-running.json');
  const { ready, ...rest } = published.combat;
  const renamed = { ...published, combat: { ...rest, readyCount: ready } };

  assert.deepEqual(readSnapshot(renamed).problems, ['combat.ready: expected a finite number, found nothing']);

  const h = harness();
  try {
    mountProductUi(h.root, h.context);
    h.emit(renamed);
    assert.equal(h.panel().getAttribute('data-problems'), '1');
    assert.deepEqual(h.problems(), ['combat.ready: expected a finite number, found nothing']);
    assert.equal(h.panel().querySelector('.crawler-problems').hidden, false);
    // The fight is shown as a session that holds none rather than with a count of zero the product never sent.
    assert.equal(h.panel().getAttribute('data-combat'), 'none');
    assert.equal(h.panel().querySelector('.crawler-combat').hidden, true);
  } finally {
    h.restore();
  }
});

test('the names the panel shares with the product are the names the product declares', async () => {
  assert.equal(UI_CONTRACT, contract.projectionContract);
  assert.equal(UI_ACTION_INTENT, contract.actionIntent);
  assert.equal(UI_ACTION_CONTRACT, contract.actionContract);
  for (const [name, action] of Object.entries(ACTIONS)) {
    assert.ok(contract.actions.includes(action), `ACTIONS.${name} is '${action}', which the product does not read`);
  }

  // The stand-alone controls send the action the projection published for them, and each of those is one the
  // product reads too.
  for (const name of SESSIONS) {
    const published = await fixture(name);
    for (const [control, { action }] of Object.entries(published.controls)) {
      assert.ok(action === '' || contract.actions.includes(action), `${name} publishes '${action}' for ${control}`);
    }
  }
});

test('the panel reads the projection the way the Engine delivers it: at once, as null, and frozen', async () => {
  const published = await fixture('session-running.json');
  const h = harness();
  try {
    // Mounted before the product has published: the Engine delivers null at once, and the panel says it has no
    // projection rather than drawing one.
    const early = mountProductUi(h.root, h.context);
    assert.equal(h.panel().getAttribute('data-projection'), 'none');
    assert.equal(h.panel().getAttribute('data-mode'), 'starting');
    h.emit(published);
    assert.equal(h.panel().getAttribute('data-projection'), 'current');

    // A rebound runtime publishes null before its next value: the panel stops calling what it shows current.
    h.emit(null);
    assert.equal(h.panel().getAttribute('data-projection'), 'none');
    early.dispose();
    assert.equal(h.unsubscribed, true);

    // Mounted after the product has published: the current envelope is delivered before subscribe returns, so the
    // panel is drawn at once rather than at the next update.
    h.emit(published);
    const late = mountProductUi(h.root, h.context);
    assert.equal(h.panel().getAttribute('data-projection'), 'current');
    assert.equal(h.panel().getAttribute('data-mode'), 'running');
    assert.equal(h.context.projection.current().value.session.mode, 'running');
    assert.ok(Object.isFrozen(h.context.projection.current().value.combat.members));
    late.dispose();
  } finally {
    h.restore();
  }
});

test('each section is a named export that draws its block on its own', async () => {
  const published = await fixture('session-turn-based.json');
  const h = harness();
  try {
    const problems = [];
    const combat = readCombat(fields(published.combat, 'combat', problems));
    assert.deepEqual(problems, []);
    const claims = [];
    const section = mountCombat({ panel: h.document.createElement('section'), claim: (action) => claims.push(action) });
    section.render({
      combat,
      attack: published.controls.attack,
      pace: published.controls.turnBased,
      skip: published.controls.turnSkip,
      wait: published.controls.turnWait,
    });
    assert.equal(section.element.hidden, false);
    assert.match(section.element.querySelector('.crawler-combat-turn').textContent, /Round 2 — Roderick's turn \(yours\)/);
    section.element.querySelector('.crawler-skip').click();
    assert.deepEqual(claims, published.controls.turnSkip.enabled ? ['combat.turn-skip'] : []);
  } finally {
    h.restore();
  }
});

/** Every module of the companion, with its comments stripped: what is scanned is code, not the prose about it. */
async function sources() {
  const directory = new URL('../../src/ui/', import.meta.url);
  const names = (await readdir(directory)).filter((name) => name.endsWith('.ts')).sort();
  return Promise.all(
    names.map(async (name) => {
      const text = await readFile(new URL(name, directory), 'utf8');
      const code = text
        .replaceAll(/\/\*[\s\S]*?\*\//g, '')
        .replaceAll(/\/\/.*$/gm, '');
      return { name, text, code };
    }),
  );
}

test('no companion module is longer than a person reads in one sitting', async () => {
  for (const { name, text } of await sources()) {
    assert.ok(text.split('\n').length <= 600, `src/ui/${name} is ${text.split('\n').length} lines`);
  }
});

test('no companion module reaches for a clock or a timer', async () => {
  // The fight is paced by game time the product measures. A screen that reached for a clock or a timer would be
  // running a second pacing, and would show a character ready before the fight agreed.
  for (const { name, code } of await sources()) {
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
      assert.equal(code.includes(forbidden), false, `src/ui/${name} reaches for ${forbidden}`);
    }
  }
});

test('no companion module computes a gameplay quantity or decides whether a control would be taken', async () => {
  const modules = await sources();

  // Every number the projection publishes is named by the view that reads it. A position — which member, which
  // row — is printed counting from one and is not a quantity of the game's, so `index` and `member` are the two
  // numbers a screen may add one to; every other published number is read, compared, formatted, and printed, and
  // never combined with anything.
  const numbers = new Set();
  for (const { code } of modules) {
    for (const match of code.matchAll(/readonly (\w+): number;/g)) numbers.add(match[1]);
  }
  for (const position of ['index', 'member']) numbers.delete(position);
  assert.ok(numbers.has('hitPoints') && numbers.has('markRadius') && numbers.has('nextLevel'), 'the published numbers were not found');

  const quantity = `(?:[A-Za-z_$][\\w$]*\\.)+(?:${[...numbers].join('|')})\\b`;
  const patterns = [
    new RegExp(`${quantity}\\s*[-+*/%]`),
    new RegExp(`[-+*/%]\\s*${quantity}`),
    new RegExp(`Math\\.[a-z]+\\([^)]*${quantity}`),
  ];
  for (const { name, code } of modules) {
    // A template's `${...}` placeholders hold the reads themselves, and the separator between two of them —
    // `${hitPoints}/${hitPointsMax}` — is a slash this scan would otherwise read as division. They are replaced by a
    // marker first, and each placeholder's own body is scanned the same way, so a rule cannot hide inside one.
    const outside = code.replaceAll(/\$\{[^}]*\}/g, '_');
    for (const pattern of patterns) {
      const match = pattern.exec(outside);
      assert.equal(match, null, `src/ui/${name} computes a published quantity: ${match?.[0] ?? ''}`);
      for (const placeholder of code.matchAll(/\$\{([^}]*)\}/g)) {
        const inside = pattern.exec(placeholder[1]);
        assert.equal(inside, null, `src/ui/${name} computes a published quantity in a placeholder: ${inside?.[0] ?? ''}`);
      }
    }

    // Whether a control is offered is the product's answer: a control is disabled from a published verdict — its
    // `enabled`, a row's `can…`, a topic's `available`, the person already `speaking` — or outright, and never
    // from a rule the panel composed out of other facts.
    for (const statement of code.matchAll(/\.disabled\s*=\s*([^;]+);/g)) {
      const verdict = statement[1].trim();
      assert.match(
        verdict,
        /^(?:true|false|!?[\w$.[\]]+\.(?:enabled|available|speaking|can[A-Z]\w*))$/,
        `src/ui/${name} decides whether a control is offered: .disabled = ${verdict}`,
      );
    }
  }
});
