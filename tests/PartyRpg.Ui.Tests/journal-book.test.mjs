/**
 * The journal and the automap books: a page per journal book with the party's quests and what each asks, the places,
 * the calendar on the one clock and the history; and the automap drawn larger on request.
 *
 * Every case mounts the product's own running fixture, so what is drawn is what the product published.
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';

import { mountProductUi } from '../../src/ui/generated/main.js';
import { fixture, harness } from './harness.mjs';

const published = await fixture('session-running.json');

/** The running fixture with nothing held open, so a book can be opened. */
function running() {
  const value = structuredClone(published);
  value.conversation.open = false;
  value.service.open = false;
  return value;
}

/** Presses a key the way a player's keyboard does. */
function press(h, key) {
  h.dom.window.document.dispatchEvent(new h.dom.window.KeyboardEvent('keydown', { key, bubbles: true }));
}

test('the journal opens on the quests, each with its giver, what it asks and how far each part has come', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running());
    press(h, 'j');
    const book = h.panel().querySelector('.crawler-journal-book');
    assert.equal(h.panel().dataset.screen, 'journal');
    assert.deepEqual([...book.querySelectorAll('.crawler-character-tab')].map((tab) => tab.textContent), published.journal.books.map((entry) => entry.title));
    const quest = book.querySelector('.crawler-journal-quest');
    assert.equal(quest.querySelector('.crawler-inspect-name').textContent, 'The Elven Treasury');
    assert.equal(quest.querySelector('.crawler-inspect-kind').textContent, 'Given by Lord Godwinson');
    assert.deepEqual([...quest.querySelectorAll('.crawler-journal-objectives li')].map((li) => li.textContent), ['Reach Castle Navan', 'Slay the guards — 1 of 3']);
    // The last turn-in's refusal is the quest owner's answer, at the head of the page.
    assert.equal(book.querySelector('.crawler-journal-outcome').textContent, published.quests.message);

    // A later projection with the work done marks the objective, without the page keeping any count of its own.
    const progressed = running();
    progressed.quests.journal[0].objectives[1] = { ...progressed.quests.journal[0].objectives[1], count: 3, met: true };
    h.emit(progressed);
    assert.equal(book.querySelector('.crawler-journal-objectives li[data-met="true"]').textContent, 'Slay the guards — 3 of 3');
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the other pages are the product’s books: notes, places with the way to the automap, the calendar and the history', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running());
    press(h, 'j');
    const book = h.panel().querySelector('.crawler-journal-book');
    const open = (page) => book.querySelector(`.crawler-character-tab[data-page="${page}"]`).click();

    open('notes');
    assert.equal(book.querySelector('.crawler-journal-row-label').textContent, 'Learned the recipe for Cure Wounds');
    open('calendar');
    const calendar = published.journal.books.find((entry) => entry.kind === 'calendar');
    assert.equal(book.querySelector('.crawler-journal-row-label').textContent, calendar.rows[0].label);
    assert.equal(book.querySelector('.crawler-journal-row-detail').textContent, calendar.rows[0].detail);
    open('history');
    assert.equal(book.querySelector('.crawler-journal-row-label').textContent, 'Entered Emerald Island');
    open('maps');
    assert.equal(book.querySelector('.crawler-journal-row[data-marked="true"] .crawler-journal-row-label').textContent, 'Emerald Island');
    book.querySelector('.crawler-journal-open-map').click();
    assert.equal(h.panel().dataset.screen, 'map');

    // An empty book says so rather than showing a bare page.
    const empty = running();
    empty.journal.books = empty.journal.books.map((entry) => (entry.kind === 'history' ? { ...entry, rows: [] } : entry));
    h.emit(empty);
    press(h, 'j');
    open('history');
    assert.equal(book.querySelector('.crawler-journal-page .crawler-inspect-hint').textContent, 'Nothing is written here yet.');
    ui.dispose();
  } finally {
    h.restore();
  }
});

test('the automap book draws the walked squares and zooms in steps, while the small map does not', () => {
  const h = harness();
  try {
    const ui = mountProductUi(h.root, h.context);
    h.emit(running());
    press(h, 'v');
    const screen = h.panel().querySelector('.crawler-screen[data-screen="map"]');
    assert.equal(screen.querySelectorAll('.crawler-map-cell').length, published.map.drawing.cellsDrawn.length);
    const frame = screen.querySelector('.crawler-map-frame');
    assert.equal(frame.dataset.zoom, '1');
    screen.querySelector('.crawler-map-zoom-in').click();
    screen.querySelector('.crawler-map-zoom-in').click();
    screen.querySelector('.crawler-map-zoom-in').click();
    assert.equal(frame.dataset.zoom, '4');
    screen.querySelector('.crawler-map-zoom-out').click();
    assert.equal(frame.dataset.zoom, '2');
    assert.equal(h.panel().querySelector('.crawler-minimap .crawler-map-zoom').hidden, true);
    ui.dispose();
  } finally {
    h.restore();
  }
});
