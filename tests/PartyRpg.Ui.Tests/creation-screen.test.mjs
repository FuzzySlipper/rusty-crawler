/**
 * The creation screen as a player meets it: the four members across the top with their faces, the faces creation
 * offers, the member's name in a box that is theirs while they type, and the flow's own controls beneath.
 *
 * Every case mounts the product's own creating fixture, so what is drawn is what the product published.
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';

import { ACTIONS } from '../../src/ui/generated/actions.js';
import { mountProductUi } from '../../src/ui/generated/main.js';
import { fixture, harness } from './harness.mjs';

const published = await fixture('session-creating.json');

/** The creating fixture with its creation block changed. */
function creating(change) {
  const value = structuredClone(published);
  change(value.creation);
  return value;
}

/** The fixture with the faces the Engine granted: the first member's and the first offered portrait's. */
const withFaces = creating((creation) => {
  creation.roster[0].portraitImage = '/__rusty/product/runtime/ui-images/1';
  creation.portraits[0].image = '/__rusty/product/runtime/ui-images/3';
});

test('creation is the screen, with every member a card that shows their face and moves creation onto them', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(withFaces);
    const panel = h.panel();
    assert.equal(panel.dataset.screen, 'creation');
    const cards = [...panel.querySelectorAll('.crawler-creation-member')];
    assert.equal(cards.length, published.creation.members);
    assert.equal(cards[0].dataset.selected, 'true');
    assert.equal(cards[0].querySelector('img.crawler-creation-face').getAttribute('src'), '/__rusty/product/runtime/ui-images/1');
    assert.equal(cards[0].querySelector('.crawler-creation-member-name').textContent, published.creation.roster[0].name);
    // A member whose face the Engine did not grant is named by their initial rather than a broken picture.
    const second = published.creation.roster[1];
    assert.equal(cards[1].querySelector('.crawler-creation-initial').textContent, second.name.charAt(0));
    assert.equal(cards[1].querySelector('.crawler-creation-member-kind').textContent, `${second.race} · ${second.class}`);

    cards[2].click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.selectMember, member: 2 });

    const offered = panel.querySelector('.crawler-creation-portraits button[data-id]');
    assert.equal(offered.querySelector('img').getAttribute('src'), '/__rusty/product/runtime/ui-images/3');
    offered.click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.selectPortrait, portrait: published.creation.portraits[0].id });
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the name box holds the member’s name, keeps what the player types, and Enter sends it', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(published);
    const input = h.panel().querySelector('.crawler-name input');
    assert.equal(input.value, published.creation.roster[0].name);

    // A projection that changes something else leaves a half-typed name alone.
    input.value = 'Rod';
    h.emit(creating((creation) => {
      creation.refusalCode = 'no-portrait';
      creation.refusalMessage = 'Choose a face first.';
    }));
    assert.equal(input.value, 'Rod');

    input.value = 'Roderick the Bold';
    h.panel().querySelector('.crawler-name').dispatchEvent(new h.dom.window.Event('submit', { bubbles: true, cancelable: true }));
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.setName, name: 'Roderick the Bold' });

    // Moving onto another member shows that member's name.
    h.emit(creating((creation) => {
      creation.member = 1;
    }));
    assert.equal(input.value, published.creation.roster[1].name);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a member can begin again, and the default party is offered only when the ruleset has one', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(published);
    const flow = (label) => [...h.panel().querySelectorAll('.crawler-creation .crawler-actions button')].find((b) => b.textContent === label);
    assert.equal(flow('Restore default party').hidden, !published.creation.hasDefault);
    flow('Reset member').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.resetMember });
    flow('Restore default party').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.applyDefault });

    h.emit(creating((creation) => {
      creation.hasDefault = false;
    }));
    assert.equal(flow('Restore default party').hidden, true);
    ui.dispose();
  } finally {
    h.restore();
  }
});
