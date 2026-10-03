/**
 * The spellbook: the selected member's book, a page per school they hold with every spell of it learned or not, the
 * picked spell's price, aim and what stands in the way, casting and the quick slot, and the Mixing page.
 *
 * Every case mounts the product's own running fixture, so what is drawn is what the product published.
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';

import { ACTIONS } from '../../src/ui/generated/actions.js';
import { mountProductUi } from '../../src/ui/generated/main.js';
import { fixture, harness } from './harness.mjs';

const published = await fixture('session-running.json');

/** The running fixture with nothing held open and Aelina, the fixture's caster, selected. */
function running() {
  const value = structuredClone(published);
  value.conversation.open = false;
  value.service.open = false;
  value.party.roster = value.party.roster.map((member) => ({ ...member, selected: member.member === '2' }));
  return value;
}

/** Opens the spellbook by its key. */
function open(h) {
  h.dom.window.document.dispatchEvent(new h.dom.window.KeyboardEvent('keydown', { key: 'l', bubbles: true }));
  return h.panel().querySelector('.crawler-magic-book');
}

test('the book is the selected member’s, a page per school with every spell of it learned or not', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running());
    const book = open(h);
    assert.equal(h.panel().dataset.screen, 'spellbook');
    assert.match(book.querySelector('.crawler-character-title').textContent, /^Aelina · 33 \/ 36 spell points · quick spell: Fire Bolt/);
    assert.deepEqual([...book.querySelectorAll('.crawler-character-tab')].map((tab) => tab.textContent), ['Fire', 'Water', 'Body', 'Mixing']);
    const spells = [...book.querySelectorAll('.crawler-magic-book-spell')];
    assert.deepEqual(spells.map((row) => [row.dataset.spell, row.dataset.known]), [['1', 'true'], ['2', 'true'], ['4', 'false'], ['11', 'false']]);
    assert.equal(spells[2].querySelector('.crawler-magic-book-spell-meta').textContent, 'not learned');

    // An unlearned spell says so in the casting workflow's own words, and offers no cast.
    spells[2].click();
    const fireAura = published.magic.members[0].pages[0].spells[2];
    assert.equal(book.querySelector('.crawler-spell-refusal').textContent, fireAura.refusal);
    assert.equal(book.querySelector('.crawler-spell-refusal').dataset.code, fireAura.refusalCode);
    assert.equal(book.querySelector('.crawler-magic-book-cast'), null);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('a picked spell is cast at the target the product offers for its side, and made the quick spell', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running());
    const book = open(h);
    book.querySelector('.crawler-magic-book-spell[data-spell="2"]').click();
    const detail = book.querySelector('.crawler-magic-book-detail');
    assert.equal(detail.querySelector('.crawler-spell-ready').textContent, 'Ready to cast.');
    // Fire Bolt names a foe, so the opposition's rows are offered and nothing else.
    const target = detail.querySelector('.crawler-magic-book-target');
    assert.deepEqual([...target.options].map((option) => option.value), ['actor:1']);
    detail.querySelector('.crawler-magic-book-cast').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.cast, member: 1, spell: '2', target: 'actor:1' });
    // Fire Bolt is the quick spell already, so its control clears it; Torch Light's makes it the quick spell.
    assert.equal(detail.querySelector('.crawler-magic-book-quick').textContent, 'Clear quick spell');
    book.querySelector('.crawler-magic-book-spell[data-spell="1"]').click();
    book.querySelector('.crawler-magic-book-quick').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.quickSpell, member: 1, spell: '1' });

    // A spell aimed at a place offers the places the product names.
    book.querySelector('.crawler-character-tab[data-page="Water"]').click();
    book.querySelector('.crawler-magic-book-spell[data-spell="31"]').click();
    assert.deepEqual([...book.querySelector('.crawler-magic-book-target').options].map((option) => option.value), ['place:1']);

    // A learned spell above the rung held is refused by name before anything is cast.
    book.querySelector('.crawler-character-tab[data-page="Body"]').click();
    book.querySelector('.crawler-magic-book-spell[data-spell="71"]').click();
    assert.match(book.querySelector('.crawler-spell-refusal').textContent, /asks for expert mastery/);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the last casting’s answer heads the book, and the Mixing page holds the party’s alchemy', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running());
    const book = open(h);
    assert.equal(book.querySelector('.crawler-magic-book-outcome').textContent, published.magic.message);
    const mixing = book.querySelector('.crawler-magic-book-mixing');
    assert.equal(mixing.hidden, true);
    book.querySelector('.crawler-character-tab[data-page="mixing"]').click();
    assert.equal(mixing.hidden, false);
    // The mixtures are the product's pairs, each mixed by the member chosen through the product's own action.
    const row = mixing.querySelector('.crawler-alchemy-mixture');
    row.querySelector('.crawler-mix').click();
    assert.equal(h.claims.at(-1).value.data.action, ACTIONS.mix);
    assert.equal(h.claims.at(-1).value.data.first, published.alchemy.mixtures[0].first);

    // A member who holds no school says so.
    const plain = running();
    plain.party.roster = plain.party.roster.map((member) => ({ ...member, selected: member.member === '1' }));
    h.emit(plain);
    assert.equal(book.querySelector('.crawler-magic-book-spells').textContent, 'Roderick holds no school of magic.');
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the target a player chose stays chosen when the fight redraws the book', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    const two = running();
    two.magic.targets = [...two.magic.targets, { target: 'actor:2', name: 'A second beast', side: 'opposition' }];
    h.emit(two);
    const book = open(h);
    book.querySelector('.crawler-magic-book-spell[data-spell="2"]').click();
    const target = book.querySelector('.crawler-magic-book-target');
    target.value = 'actor:2';
    target.dispatchEvent(new h.dom.window.Event('change', { bubbles: true }));

    // A blow lands on a member: the book redraws, and the cast still names the beast the player chose.
    const struck = structuredClone(two);
    struck.party.roster[0].hitPoints = 30;
    h.emit(struck);
    assert.equal(book.querySelector('.crawler-magic-book-target').value, 'actor:2');
    book.querySelector('.crawler-magic-book-cast').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.cast, member: 1, spell: '2', target: 'actor:2' });
    ui.dispose();
  } finally {
    h.restore();
  }
});
