/**
 * The adventure frame: the party's portraits, purse and controls along the bottom, the books a player opens over the
 * world, the screens the product puts in front of the party, and the diagnostic panel kept out of the way.
 *
 * Every case mounts the product's own running fixture, so what is drawn is what the product published.
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';

import { ACTIONS } from '../../src/ui/generated/actions.js';
import { mountProductUi } from '../../src/ui/generated/main.js';
import { fixture, harness } from './harness.mjs';

const published = await fixture('session-running.json');

/** A copy of the running fixture with some blocks replaced. */
function variant(blocks, from = published) {
  return { ...structuredClone(from), ...blocks };
}

/** The running fixture with nothing in front of the party: no conversation and no counter held open. */
const running = variant({
  conversation: { ...structuredClone(published.conversation), open: false },
  service: { ...structuredClone(published.service), open: false },
});

/** Presses a key the way a player's keyboard does, on the page. */
function press(h, key) {
  h.dom.window.document.dispatchEvent(new h.dom.window.KeyboardEvent('keydown', { key, bubbles: true }));
}

test('the bar shows each member with the face, pools and state the product published, and a click selects them', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running);
    const cards = [...h.panel().querySelectorAll('.crawler-member')].filter((card) => !card.hidden);
    assert.equal(cards.length, 2);

    const [knight, sorcerer] = cards;
    assert.equal(knight.querySelector('.crawler-member-face').getAttribute('src'), '/__rusty/product/runtime/ui-images/1');
    assert.equal(knight.dataset.selected, 'yes');
    assert.equal(knight.querySelector('.crawler-bar-health .crawler-bar-fill').style.width, '100%');
    // A member with no spell points shows no spell bar, rather than an empty one that reads as drained.
    assert.equal(knight.querySelector('.crawler-bar-spell').hidden, true);

    // A face content gives no image is named by its initial; a down member says what laid them out.
    assert.equal(sorcerer.querySelector('.crawler-member-face').hidden, true);
    assert.equal(sorcerer.querySelector('.crawler-member-initial').textContent, 'A');
    assert.equal(sorcerer.dataset.state, 'down');
    assert.equal(sorcerer.querySelector('.crawler-bar-health .crawler-bar-fill').style.width, '0%');
    assert.equal(sorcerer.querySelector('.crawler-member-state').textContent, 'Unconscious');

    sorcerer.click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.partySelectMember, member: '2' });
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the purse, larder and clock are the product’s, and the adventure controls are offered when it would take them', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running);
    const panel = h.panel();
    assert.equal(panel.querySelector('.crawler-coins').textContent, `${running.party.coins} gold`);
    assert.equal(panel.querySelector('.crawler-food').textContent, `${running.party.provisions} ${running.party.unit}`);
    assert.equal(panel.querySelector('.crawler-date').textContent, running.clock.date);

    const use = panel.querySelector('.crawler-act-use');
    assert.equal(use.disabled, !running.controls.use.enabled);
    assert.equal(use.dataset.action, running.controls.use.action);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a book opens by its button or key over the world, Escape returns to the world, and the keyboard stays with the game', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running);
    const panel = h.panel();
    assert.equal(panel.dataset.screen, 'world');
    const character = panel.querySelector('.crawler-screen[data-screen="character"]');
    assert.equal(character.hidden, true);

    const before = h.focused;
    press(h, 'i');
    assert.equal(panel.dataset.screen, 'character');
    assert.equal(character.hidden, false);
    // The host's own keys — rest, leave, attack — keep working over a book, so the game keeps the keyboard.
    assert.equal(h.context.ui.interactionMode(), 'gameplay');
    assert.ok(h.focused > before, 'opening a book hands the keyboard back to the game view');
    assert.equal(panel.querySelector('.crawler-hud-book[data-screen="character"]').dataset.open, 'yes');
    // The selected member's character page is drawn inside the book.
    assert.ok(character.querySelector('.crawler-character .crawler-sheet'));

    // Another book replaces it; the same book again closes it; Escape closes whatever is open.
    panel.querySelector('.crawler-hud-book[data-screen="map"]').click();
    assert.equal(panel.dataset.screen, 'map');
    press(h, 'v');
    assert.equal(panel.dataset.screen, 'world');
    press(h, 'j');
    const focused = h.focused;
    press(h, 'Escape');
    assert.equal(panel.dataset.screen, 'world');
    assert.equal(h.context.ui.interactionMode(), 'gameplay');
    assert.ok(h.focused > focused, 'closing a book hands the keyboard back to the game view');
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a key typed into a field never opens a book, and the diagnostic panel shows and hides by its own key', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running);
    const panel = h.panel();
    const field = h.dom.window.document.createElement('input');
    panel.append(field);
    field.dispatchEvent(new h.dom.window.KeyboardEvent('keydown', { key: 'i', bubbles: true }));
    assert.equal(panel.dataset.screen, 'world');

    const diagnostics = panel.querySelector('.crawler-diagnostics');
    assert.equal(diagnostics.hidden, true);
    press(h, '`');
    assert.equal(diagnostics.hidden, false);
    // Every fact and session control is still published there, complete and current.
    assert.ok(diagnostics.querySelector('dl'));
    assert.ok(diagnostics.querySelector('.crawler-save'));
    press(h, '`');
    assert.equal(diagnostics.hidden, true);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a conversation or counter the product holds open is the screen, and books wait until it is left', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    assert.equal(published.conversation.open, true, 'the fixture holds a conversation open');
    h.emit(published);
    const panel = h.panel();
    assert.equal(panel.dataset.screen, 'conversation');
    assert.equal(panel.querySelector('.crawler-screen[data-screen="conversation"] .crawler-screen-close'), null);
    press(h, 'i');
    assert.equal(panel.dataset.screen, 'conversation');

    h.emit(running);
    assert.equal(panel.dataset.screen, 'world');

    // A conversation opened over a book closes the book: leaving it returns to the world, not the book.
    press(h, 'i');
    assert.equal(panel.dataset.screen, 'character');
    h.emit(published);
    assert.equal(panel.dataset.screen, 'conversation');
    h.emit(running);
    assert.equal(panel.dataset.screen, 'world');

    // A moment the product publishes nothing forgets what it held open.
    h.emit(published);
    h.emit(null);
    assert.equal(panel.dataset.screen, 'world');
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the fight stands beside the world only while something is fighting the party', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(variant({ combat: { ...structuredClone(running.combat), engaged: false } }, running));
    const fight = h.panel().querySelector('.crawler-fight');
    assert.equal(fight.hidden, true);
    h.emit(variant({ combat: { ...structuredClone(running.combat), engaged: true } }, running));
    assert.equal(fight.hidden, false);
    ui.dispose();
  } finally {
    h.restore();
  }
});
