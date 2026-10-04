/**
 * The dialogue and counter screens: the person spoken with beside their words, and the establishment with a page for
 * each thing it does, every row, price and refusal the product's own.
 *
 * Every case mounts the product's own running fixture, so what is drawn is what the product published.
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';

import { ACTIONS } from '../../src/ui/generated/actions.js';
import { mountProductUi } from '../../src/ui/generated/main.js';
import { fixture, harness } from './harness.mjs';

const published = await fixture('session-running.json');

/** The fixture with the counter closed, so the conversation the product holds open is the screen. */
function talking() {
  const value = structuredClone(published);
  value.service.open = false;
  value.conversation.people[0].portraitImage = '/__rusty/product/runtime/ui-images/7';
  return value;
}

/** The fixture with the conversation left, so the counter the product holds open is the screen. */
function shopping() {
  const value = structuredClone(published);
  value.conversation.open = false;
  value.service.stock[0].image = '/__rusty/product/runtime/ui-images/8';
  return value;
}

test('the dialogue shows the speaker beside their words, and every choice asks for what it was shown', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(talking());
    const panel = h.panel();
    assert.equal(panel.dataset.screen, 'conversation');
    assert.equal(panel.querySelector('.crawler-screen[data-screen="conversation"]').dataset.rustyUiInteractive, '',
      'the contextual screen is an Engine-admitted pointer surface');
    const dialogue = panel.querySelector('.crawler-dialogue');
    assert.equal(dialogue.querySelector('img.crawler-dialogue-face').getAttribute('src'), '/__rusty/product/runtime/ui-images/7');
    assert.equal(dialogue.querySelector('.crawler-dialogue-name').textContent, published.conversation.speaker);
    assert.equal(dialogue.querySelector('.crawler-dialogue-greeting').textContent, published.conversation.greeting);
    // A line keeps what its answer could not deliver.
    assert.equal(dialogue.querySelector('.crawler-dialogue-line').textContent, `Mira: ${published.conversation.said[0].text} ${published.conversation.said[0].residue}`);

    // A withheld topic says why; an offered one is brought up by its own id.
    assert.equal(dialogue.querySelector('.crawler-dialogue-withheld li').textContent, 'The errand — the errand is not finished');
    dialogue.querySelector('.crawler-dialogue-topic').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.conversationTopic, target: 'topic-1' });
    // Another person present is a face to turn to; a hand in a pocket names the member who tries.
    dialogue.querySelector('.crawler-dialogue-other').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.conversationPerson, target: 'simon' });
    dialogue.querySelector('.crawler-dialogue-steal').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.conversationSteal, member: 1 });
    dialogue.querySelector('.crawler-dialogue-leave').click();
    assert.equal(h.claims.at(-1).value.data.action, published.controls.conversationLeave.action);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the counter has a page for each thing it does, with its wares, prices and refusals before anything is settled', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(shopping());
    const panel = h.panel();
    assert.equal(panel.dataset.screen, 'service');
    const counter = panel.querySelector('.crawler-counter');
    assert.equal(counter.querySelector('.crawler-counter-title').textContent, 'The Sword and Shield · Bertram');
    assert.equal(counter.querySelector('.crawler-counter-purse').textContent, `${published.service.coins} gold`);
    const tabs = [...counter.querySelectorAll('.crawler-character-tab')].map((tab) => tab.dataset.page);
    assert.deepEqual(tabs, ['wares', 'sell', 'identify', 'repair', 'learn', 'fare', 'notice', 'debt']);
    // The last transaction's answer heads the screen.
    assert.equal(counter.querySelector('.crawler-counter-outcome').textContent, published.service.message);

    // Wares: a tile per lot with its picture and price; a sold-out lot says so and is refused before it is bought.
    const lots = [...counter.querySelectorAll('.crawler-counter-tile')];
    assert.equal(lots[0].querySelector('img').getAttribute('src'), '/__rusty/product/runtime/ui-images/8');
    assert.equal(lots[1].querySelector('.crawler-counter-caption').textContent, 'sold out');
    lots[0].click();
    counter.querySelector('.crawler-counter-buy').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.serviceBuy, target: 'stock:sword', count: 1 });
    counter.querySelector('.crawler-counter-steal').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.serviceSteal, target: 'stock:sword', member: 1 });
    counter.querySelectorAll('.crawler-counter-tile')[1].click();
    assert.equal(counter.querySelector('.crawler-counter-buy').disabled, true);

    // Sell: the party's own items with the price the counter would pay.
    counter.querySelector('.crawler-character-tab[data-page="sell"]').click();
    counter.querySelector('.crawler-counter-tile[data-item="3"]').click();
    counter.querySelector('.crawler-counter-work').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.serviceSell, target: '3' });

    // Identify and repair are priced on their rows before anything is settled.
    counter.querySelector('.crawler-character-tab[data-page="identify"]').click();
    assert.equal(counter.querySelector('.crawler-counter-tile .crawler-counter-caption').textContent, '25 gold');
    counter.querySelector('.crawler-character-tab[data-page="repair"]').click();
    counter.querySelector('.crawler-counter-tile').click();
    assert.equal(counter.querySelector('.crawler-counter-work').textContent, 'Repair · 40 gold');

    // Learn: a lesson taught to the member picked.
    counter.querySelector('.crawler-character-tab[data-page="learn"]').click();
    const learner = counter.querySelector('.crawler-counter-learner');
    learner.value = '1';
    learner.dispatchEvent(new h.dom.window.Event('change', { bubbles: true }));
    counter.querySelector('.crawler-counter-teach').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.serviceTeach, target: 'Sword', tier: 1, member: 1 });

    // Travel: the passage priced for the party, paid through the fare command.
    counter.querySelector('.crawler-character-tab[data-page="fare"]').click();
    counter.querySelector('.crawler-counter-offer').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.serviceFare, target: '4', member: -1, count: 1 });

    // Notices are read, not pressed; a debt is repaid by what the product says the purse would hand over.
    counter.querySelector('.crawler-character-tab[data-page="notice"]').click();
    assert.equal(counter.querySelector('.crawler-counter-notice').textContent, 'Travellers speak of the roads east.');
    counter.querySelector('.crawler-character-tab[data-page="debt"]').click();
    counter.querySelector('.crawler-counter-repay').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.serviceRepay, target: 'fine', count: 90 });
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a choice the mechanism would refuse is shown with its reason, and a closed counter says it is closed', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    const value = shopping();
    value.service.offers = [
      {
        kind: 'cure', subject: 'heal', name: 'Healing', amount: 1, price: 10,
        choices: [{ operation: 'cure', member: 0, name: 'Roderick', count: 1, price: 10, payment: 0, enabled: false, reason: 'Roderick is not hurt.' }],
      },
    ];
    h.emit(value);
    const counter = h.panel().querySelector('.crawler-counter');
    counter.querySelector('.crawler-character-tab[data-page="cure"]').click();
    assert.equal(counter.querySelector('.crawler-counter-offer').disabled, true);
    assert.equal(counter.querySelector('.crawler-counter-reason').textContent, 'Roderick is not hurt.');

    const closed = structuredClone(value);
    Object.assign(closed.service, { state: 'closed', stock: [], sales: [], identify: [], repair: [], lessons: [], offers: [], debts: [], canBuy: false, canSell: false, canSteal: false });
    h.emit(closed);
    assert.equal(counter.querySelector('.crawler-counter-state').dataset.state, 'closed');
    assert.equal(counter.querySelector('.crawler-counter-page .crawler-inspect-hint').textContent, 'The counter is closed.');
    ui.dispose();
  } finally {
    h.restore();
  }
});
