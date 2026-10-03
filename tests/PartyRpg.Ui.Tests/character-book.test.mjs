/**
 * The character book: the selected member's sheet, growth and ranks, their skills, what they wear beside the party's
 * one pack with an inspector for the picked thing, and the party's awards.
 *
 * Every case mounts the product's own running fixture, so what is drawn is what the product published.
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';

import { ACTIONS } from '../../src/ui/generated/actions.js';
import { mountProductUi } from '../../src/ui/generated/main.js';
import { fixture, harness } from './harness.mjs';

const published = await fixture('session-running.json');

/** The running fixture with nothing in front of the party, so the book can be opened. */
const running = {
  ...structuredClone(published),
  conversation: { ...structuredClone(published.conversation), open: false },
  service: { ...structuredClone(published.service), open: false },
};

/** Opens the character book on the page the case names. */
function open(h, page) {
  h.dom.window.document.dispatchEvent(new h.dom.window.KeyboardEvent('keydown', { key: 'i', bubbles: true }));
  const book = h.panel().querySelector('.crawler-character');
  book.querySelector(`.crawler-character-tab[data-page="${page}"]`).click();
  return book;
}

test('the stats page is the selected member’s sheet, growth and ranks, as the product read them', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running);
    const book = open(h, 'stats');
    assert.equal(h.panel().dataset.screen, 'character');
    // The member shown is the one the party has selected, and the faces at the top choose another through the product.
    assert.match(book.querySelector('.crawler-character-title').textContent, /^Roderick · Knight/);
    const faces = [...book.querySelectorAll('.crawler-character-face')];
    assert.deepEqual(faces.map((face) => face.dataset.selected), ['yes', 'no']);
    faces[1].click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.partySelectMember, member: '2' });

    const boxes = [...book.querySelectorAll('.crawler-sheet .crawler-sheet-box')];
    assert.deepEqual(boxes.map((box) => box.querySelector('.crawler-sheet-title').textContent), ['Scores', 'Vitals', 'Resistances']);
    const might = boxes[0].querySelector('dd');
    assert.equal(might.textContent, '16');
    assert.equal(might.dataset.detail, 'carried 14');

    // Growth hands a level to the training hall the party stands at, through its own action.
    const growth = published.progression.members[0];
    const train = book.querySelector('.crawler-character-train');
    assert.equal(train.textContent, `Train to level ${growth.nextLevel} for ${growth.fee} gold`);
    train.click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.serviceTrain, member: 0 });
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the skills page offers the next point the product prices and says why there is none', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running);
    const book = open(h, 'skills');
    const rows = [...book.querySelectorAll('.crawler-character-skill')];
    assert.deepEqual(rows.map((row) => row.dataset.skill), published.skills.members[0].skills.map((skill) => skill.skill));
    rows[0].querySelector('.crawler-character-raise').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.raiseSkill, member: 0, skill: 'Sword' });
    assert.equal(rows[1].querySelector('.crawler-character-raise'), null);
    assert.equal(rows[1].querySelector('.crawler-character-skill-refusal').textContent, published.skills.members[0].skills[1].refusal);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the inventory page shows the member’s figure beside the party pack, and the picked thing is inspected and acted on', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running);
    const book = open(h, 'inventory');
    // Every slot of the figure is drawn, the worn sword in its own with its picture.
    const slots = [...book.querySelectorAll('.crawler-inventory-slot')];
    assert.deepEqual(slots.map((slot) => slot.dataset.slot), published.equipment.slots);
    const sword = book.querySelector('.crawler-inventory-slot[data-slot="main hand"] .crawler-item-tile');
    assert.equal(sword.querySelector('img').getAttribute('src'), '/__rusty/product/runtime/ui-images/4');

    // The shared pack holds everything, wearable or not; a thing with no picture shows its name.
    const tiles = [...book.querySelectorAll('.crawler-inventory-grid .crawler-item-tile')];
    assert.deepEqual(tiles.map((tile) => tile.dataset.item), published.equipment.pack.map((row) => row.item));
    assert.equal(tiles[1].querySelector('.crawler-item-words').textContent, 'Dagger');

    // Picking the armour inspects it, and Equip puts it on the member shown.
    tiles[0].click();
    const inspector = () => book.querySelector('.crawler-inventory-inspect');
    assert.equal(inspector().querySelector('.crawler-inspect-name').textContent, 'Leather Armor');
    assert.deepEqual([...inspector().querySelectorAll('.crawler-inspect-facts li')].map((li) => li.textContent), published.equipment.pack[0].facts);
    inspector().querySelector('.crawler-inventory-equip').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.equip, member: 0, item: '22' });

    // The worn sword comes off from its slot.
    book.querySelector('.crawler-inventory-slot[data-slot="main hand"] .crawler-item-tile').click();
    inspector().querySelector('.crawler-inventory-unequip').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.unequip, member: 0, slot: 'main hand' });

    // The lamp is used through the game's own item action, the scroll read through the spellbook's casting.
    book.querySelector('.crawler-item-tile[data-item="24"]').click();
    inspector().querySelector('.crawler-inventory-use').click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.useItem, member: 0, item: '24' });
    book.querySelector('.crawler-item-tile[data-item="8"]').click();
    const read = inspector().querySelector('.crawler-inventory-cast');
    assert.equal(read.textContent, 'Use: Torch Light');
    read.click();
    assert.deepEqual(h.claims.at(-1).value.data, { action: ACTIONS.cast, member: 0, spell: '1', target: '', item: '8' });

    // A thing an errand needs says so, and offers nothing that would part with it.
    book.querySelector('.crawler-item-tile[data-item="639"]').click();
    assert.match(inspector().querySelector('.crawler-inspect-retained').textContent, /cannot part with it/);
    assert.equal(inspector().querySelector('.crawler-inventory-use'), null);

    // The product's answer to the last change is printed as it arrived.
    assert.equal(book.querySelector('.crawler-inventory-outcome').textContent, published.equipment.outcome.message);

    // A picked thing that leaves the pack is forgotten rather than inspected stale.
    const emptied = structuredClone(running);
    emptied.equipment.pack = [];
    h.emit(emptied);
    assert.equal(book.querySelector('.crawler-inspect-hint').textContent, 'Pick something worn or in the pack to inspect it.');
    assert.match(book.querySelector('.crawler-inventory-pack .crawler-row-label').textContent, /pack is empty/);
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the awards page lists what the party has accomplished in the game’s words', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running);
    const book = open(h, 'awards');
    // The accomplishments section is the one the product's awards are drawn by, shown on this page.
    assert.equal(book.querySelector('.crawler-character-awards').hidden, false);
    assert.match(book.querySelector('.crawler-awards-list').textContent, /Rogue/);
    ui.dispose();
  } finally {
    h.restore();
  }
});
