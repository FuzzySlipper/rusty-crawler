/**
 * The fight beside the world, the rest screen's judged stops, and the one line that answers the party's latest act.
 *
 * Every case mounts the product's own running fixture, so what is drawn is what the product published.
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';

import { mountProductUi } from '../../src/ui/generated/main.js';
import { fixture, harness } from './harness.mjs';

const published = await fixture('session-running.json');
const paced = await fixture('session-turn-based.json');

/** The running fixture with nothing in front of the party. */
function world(from = published) {
  const value = structuredClone(from);
  value.conversation.open = false;
  value.service.open = false;
  return value;
}

test('the fight beside the world names its pacing, the acting member, what their attack would strike and the nearest foes', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(world());
    const fight = h.panel().querySelector('.crawler-fight .crawler-fight-panel');
    assert.equal(fight.hidden, false);
    assert.match(fight.querySelector('.crawler-fight-pace').textContent, /^Real time/);
    assert.equal(fight.querySelector('.crawler-fight-actor').textContent, 'Acting: Roderick — ready');
    assert.equal(fight.querySelector('.crawler-fight-target').textContent, 'Target: A beast · 100 away · 14/40 hp');
    assert.equal(fight.querySelector('.crawler-fight-foe[data-aim="yes"]').dataset.fighter, 'actor:1');
    // The pacing control says what pressing it does; skipping and waiting belong to a paced round only.
    assert.match(fight.querySelector('.crawler-fight-controls .crawler-fight-pace').textContent, /^Turn-based/);
    assert.equal(fight.querySelector('.crawler-fight-controls .crawler-fight-skip').hidden, true);
    // The fight's last blow is labelled as the party's own order.
    assert.equal(fight.querySelector('.crawler-fight-last').dataset.byParty, 'yes');
    assert.match(fight.querySelector('.crawler-fight-last').textContent, /^Last order: Roderick hits A beast/);

    // An acting member still recovering says so, and a refused selection is shown under them.
    const recovering = world();
    recovering.combat.members[0].ready = false;
    recovering.combat.members[0].recoverySeconds = 12.25;
    recovering.combat.selectionMessage = 'Aelina cannot act; Roderick is now selected.';
    recovering.combat.selectionCode = 'member-incapable';
    h.emit(recovering);
    assert.equal(fight.querySelector('.crawler-fight-actor').textContent, 'Acting: Roderick — recovering 12.3s');
    assert.equal(fight.querySelector('.crawler-fight-actor').dataset.state, 'recovering');
    assert.equal(fight.querySelector('.crawler-fight-selection').textContent, 'Aelina cannot act; Roderick is now selected.');

    // With nothing within reach the target line says so; a crowd shows its nearest few and counts the rest.
    const crowd = world();
    crowd.combat.aim = '';
    crowd.combat.aimName = '';
    crowd.combat.enemies = Array.from({ length: 8 }, (_, index) => ({ ...published.combat.enemies[0], id: `actor:${index + 1}`, distance: 600 + index }));
    h.emit(crowd);
    assert.equal(fight.querySelector('.crawler-fight-target').textContent, "Target: nothing within Roderick's reach");
    assert.equal(fight.querySelectorAll('.crawler-fight-foe').length, 5);
    assert.equal(fight.querySelector('.crawler-fight-more').textContent, 'and 3 more');

    // Nothing hostile: the fight steps aside.
    const quiet = world();
    quiet.combat.engaged = false;
    h.emit(quiet);
    assert.equal(h.panel().querySelector('.crawler-fight').hidden, true);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a paced fight names whose turn it is and offers skipping and waiting', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(world(paced));
    const fight = h.panel().querySelector('.crawler-fight-panel');
    assert.match(fight.querySelector('.crawler-fight-pace').textContent, /^Turn-based · round \d+ · .+'s turn/);
    assert.equal(fight.querySelector('.crawler-fight-skip').hidden, false);
    assert.equal(fight.querySelector('.crawler-fight-wait').hidden, false);
    assert.match(fight.querySelector('.crawler-fight-controls .crawler-fight-pace').textContent, /^Real-time/);
    // The bar's own pacing control names the same switch.
    assert.match(h.panel().querySelector('.crawler-act-pace').textContent, /^Real-time/);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the line along the top is the answer to the latest act, under the act it answers', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(world());
    const said = h.panel().querySelector('.crawler-hud-said');
    assert.equal(said.textContent, 'Attack · Roderick → A beast: Roderick hits A beast (melee): 5 Phys damage landed.');
    assert.equal(said.dataset.serial, '7');

    // A later use replaces it, however recent the fight's own news is: the line never keeps an older answer.
    const used = world();
    used.feedback = { serial: 8, source: 'use', actor: '', subject: 'A sign', outcome: 'applied', code: '', message: 'It reads: Harmondale, 2 days.' };
    h.emit(used);
    assert.equal(said.textContent, 'Use · A sign: It reads: Harmondale, 2 days.');
    // A sentence that already names what it concerned is not headed by it twice.
    const named = world();
    named.feedback = { ...used.feedback, serial: 9, subject: 'A fixture', message: 'A fixture: nothing comes of it.' };
    h.emit(named);
    assert.equal(said.textContent, 'Use: A fixture: nothing comes of it.');

    // A refused stop reads as a refusal of that stop; a save names itself and not its slot.
    const refused = world();
    refused.feedback = { serial: 10, source: 'stop', actor: '', subject: 'camp', outcome: 'refused', code: 'camp-hostiles-near', message: 'A beast stands near enough that the party will not camp.' };
    h.emit(refused);
    assert.equal(said.textContent, 'Camp: A beast stands near enough that the party will not camp.');
    assert.equal(said.dataset.outcome, 'refused');
    const saved = world();
    saved.feedback = { serial: 11, source: 'save', actor: '', subject: 'session', outcome: 'applied', code: '', message: "Saved the session to slot 'session' at 1168-01-02 06:00." };
    h.emit(saved);
    assert.equal(said.textContent, "Save: Saved the session to slot 'session' at 1168-01-02 06:00.");

    // Before anything has been done there is no line at all.
    const fresh = world();
    fresh.feedback = { serial: 0, source: '', actor: '', subject: '', outcome: 'none', code: '', message: '' };
    h.emit(fresh);
    assert.equal(said.hidden, true);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the rest screen shows each stop with its length and cost, or why it would be refused, before it is pressed', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    const value = world();
    value.controls.camp.enabled = false;
    h.emit(value);
    h.panel().querySelector('.crawler-hud-book[data-screen="rest"]').click();
    const options = [...h.panel().querySelectorAll('[data-screen="rest"] .crawler-rest-option')];
    assert.equal(options.length, 5);
    const [night, camp, dawn] = options;
    assert.equal(night.querySelector('.crawler-rest-judgment').textContent, '8 hour(s) · costs 2 portions');
    assert.equal(night.querySelector('.crawler-rest-unrestored li').textContent, 'Not restored — Aelina: unconscious, which a night does not end');
    assert.equal(camp.dataset.offered, 'no');
    assert.equal(camp.querySelector('button').disabled, true);
    assert.equal(camp.querySelector('.crawler-rest-judgment').textContent, 'A beast stands near enough that the party will not camp.');
    assert.equal(dawn.querySelector('.crawler-rest-judgment').textContent, '7 hour(s) 30 minute(s) · costs nothing');
    assert.equal(night.querySelector('.crawler-rest-unrestored').hidden, false);
    assert.equal(dawn.querySelector('.crawler-rest-unrestored').hidden, true);
    ui.dispose();
  } finally {
    h.restore();
  }
});
