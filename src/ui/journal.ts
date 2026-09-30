/**
 * The journal: the five books, and what each of them holds. Their titles are the game's words, their state
 * sentences and rows come from the product, and this companion counts, dates, and remembers nothing of its own. A
 * book whose owner the session does not compose says so in the game's words rather than showing an empty list,
 * which is why each carries its own state sentence and its availability as an attribute a reader can check.
 */

import type { Fields } from './reader.js';
import { element, head, section, type Host } from './dom.js';

/** One row of one book. */
export interface JournalRowView {
  readonly id: string;
  readonly label: string;
  readonly detail: string;
  readonly state: string;
  readonly source: string;
  readonly marked: boolean;
}

/** One of the five books. */
export interface JournalBookView {
  readonly kind: string;
  readonly title: string;
  readonly available: boolean;
  readonly state: string;
  readonly rows: readonly JournalRowView[];
}

/** The five books, in the product's own order. */
export interface JournalView {
  readonly available: boolean;
  readonly books: readonly JournalBookView[];
}

export function readJournal(f: Fields): JournalView {
  return {
    available: f.flag('available'),
    books: f.list('books', (book) => ({
      kind: book.text('kind'),
      title: book.text('title'),
      available: book.flag('available'),
      state: book.text('state'),
      rows: book.list('rows', (row) => ({
        id: row.text('id'),
        label: row.text('label'),
        detail: row.text('detail'),
        state: row.text('state'),
        source: row.text('source'),
        marked: row.flag('marked'),
      })),
    })),
  };
}

/** The mounted journal: the section, and where the quests book is placed inside it. */
export interface JournalSection {
  readonly element: HTMLElement;
  /** Places the quests book as the journal's first book. */
  hold(quests: HTMLElement): void;
  render(view: JournalView): void;
}

/** The four books besides the quests book, which the quests block draws. */
const BOOKS = ['notes', 'maps', 'calendar', 'history'] as const;

/** Mounts the journal and its four own books. */
export function mountJournal(host: Host): JournalSection {
  const { panel } = host;
  const journal = section('crawler-journal');
  const journalState = element('p', 'crawler-journal-state');
  journal.append(head('Journal'), journalState);
  const books = new Map<string, { readonly section: HTMLElement; readonly head: HTMLElement; readonly state: HTMLElement; readonly rows: HTMLElement }>();
  const bookSections = BOOKS.map((kind) => {
    const book = section('crawler-book');
    book.dataset.book = kind;
    const bookHead = head();
    const state = element('p', 'crawler-book-state');
    const rows = element('div', 'crawler-book-rows');
    book.append(bookHead, state, rows);
    books.set(kind, { section: book, head: bookHead, state, rows });
    return book;
  });

  const render = (view: JournalView): void => {
    panel.dataset.journal = view.available ? 'present' : 'none';
    journal.hidden = !view.available;
    for (const book of view.books) {
      const drawn = books.get(book.kind);
      if (drawn === undefined) continue;
      drawn.section.hidden = false;
      drawn.section.dataset.available = String(book.available);
      drawn.head.textContent = book.title;
      drawn.state.textContent = book.state;
      drawn.rows.replaceChildren(
        ...book.rows.map((row) => {
          const line = element('div', 'crawler-book-row');
          line.dataset.id = row.id;
          line.dataset.source = row.source;
          line.dataset.marked = String(row.marked);
          const label = element('span', 'crawler-row-label');
          label.textContent = `${row.marked ? '✓' : '·'} ${row.label}`;
          line.append(label);
          if (row.detail !== '') {
            const detail = element('div', 'crawler-book-detail');
            detail.textContent = row.detail;
            line.append(detail);
          }

          if (row.state !== '') {
            const state = element('div', 'crawler-book-row-state');
            state.textContent = row.state;
            line.append(state);
          }

          return line;
        }),
      );
    }

    // A projection that carries no journal empties the books rather than leaving the last one's rows behind a hidden
    // section, so a reload cannot show a book the save no longer has.
    if (!view.available) {
      for (const drawn of books.values()) {
        drawn.section.hidden = true;
        drawn.section.dataset.available = 'false';
        drawn.head.textContent = '';
        drawn.state.textContent = '';
        drawn.rows.replaceChildren();
      }
    }
  };

  return {
    element: journal,
    hold(quests) {
      journal.append(quests, ...bookSections);
    },
    render,
  };
}
