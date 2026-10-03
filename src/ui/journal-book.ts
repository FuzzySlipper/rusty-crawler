/**
 * The journal (`J`): a page for each of the party's books — its current quests, its notes, the places it knows, the
 * calendar and its history — as the original's journal tabs them. Each page is the book the product published: its
 * title, its standing in the product's words, and its rows, with a quest's objectives and how far each has come.
 *
 * The Maps page opens the automap; the calendar is the product's reading of the one clock. Nothing here is the browser's own record: every
 * entry, count and date is the product's. Which page is open is presentation state.
 */

import type { JournalBookView, JournalView } from './journal.js';
import type { QuestJournalView, QuestsView } from './quests.js';
import { button, element, redrawGuard, report, result, section, type Host, type Section } from './dom.js';

/** What the journal draws from. */
export interface JournalBookReading {
  readonly journal: JournalView;
  readonly quests: QuestsView;
}

/** The pages, in the order the original's journal tabs them. */
const PAGES = ['quests', 'notes', 'maps', 'calendar', 'history'] as const;
type Page = (typeof PAGES)[number];

/** A quiet line for a page with nothing on it yet. */
function hint(text: string): HTMLElement {
  const made = element('p', 'crawler-inspect-hint');
  made.textContent = text;
  return made;
}

/** One quest as its card: what it is, who gave it, what it asks and how far each part has come. */
function questCard(quest: QuestJournalView): HTMLElement {
  const card = element('div', 'crawler-journal-quest');
  card.dataset.quest = quest.quest;
  card.dataset.state = quest.state;
  const head = element('div', 'crawler-journal-quest-head');
  const name = element('p', 'crawler-inspect-name');
  name.textContent = quest.name;
  const state = element('span', 'crawler-journal-quest-state');
  state.textContent = quest.state;
  head.append(name, state);
  card.append(head);
  if (quest.giverName !== '') {
    const giver = element('p', 'crawler-inspect-kind');
    giver.textContent = `Given by ${quest.giverName}`;
    card.append(giver);
  }

  if (quest.note !== '') {
    const note = element('p', 'crawler-journal-quest-note');
    note.textContent = quest.note;
    card.append(note);
  }

  const objectives = element('ul', 'crawler-journal-objectives');
  for (const objective of quest.objectives) {
    const line = element('li');
    line.dataset.objective = objective.id;
    line.dataset.met = String(objective.met);
    line.textContent = objective.required > 1 ? `${objective.label} — ${objective.count} of ${objective.required}` : objective.label;
    objectives.append(line);
  }

  if (quest.objectives.length > 0) card.append(objectives);
  if (quest.canTurnIn) {
    const ready = element('p', 'crawler-journal-ready');
    ready.textContent = 'Everything is done; return to the giver.';
    card.append(ready);
  }

  if (quest.residue !== '') {
    const residue = element('p', 'crawler-journal-residue');
    residue.textContent = quest.residue;
    card.append(residue);
  }

  return card;
}

/** A book's rows: each entry with its detail and state, marked where the product marks it. */
function rows(book: JournalBookView): HTMLElement {
  const list = element('ul', 'crawler-journal-rows');
  for (const row of book.rows) {
    const item = element('li', 'crawler-journal-row');
    item.dataset.id = row.id;
    item.dataset.source = row.source;
    item.dataset.marked = String(row.marked);
    const label = element('span', 'crawler-journal-row-label');
    label.textContent = row.label;
    item.append(label);
    for (const [text, className] of [
      [row.detail, 'crawler-journal-row-detail'],
      [row.state, 'crawler-journal-row-state'],
    ] as const) {
      if (text === '') continue;
      const extra = element('span', className);
      extra.textContent = text;
      item.append(extra);
    }

    list.append(item);
  }

  return list;
}

/**
 * Mounts the journal.
 * @param openMap Opens the automap book, which the Maps page links to.
 */
export function mountJournalBook(host: Host, openMap: () => void): Section<JournalBookReading> {
  const book = section('crawler-journal-book');
  const tabs = element('div', 'crawler-character-tabs');
  const title = element('p', 'crawler-character-title');
  const state = element('p', 'crawler-journal-standing');
  const outcome = result('crawler-journal-outcome');
  const body = element('div', 'crawler-journal-page');
  book.append(tabs, title, state, outcome, body);
  const changed = redrawGuard();
  let page: Page = 'quests';
  let last: JournalBookReading | null = null;

  const render = (reading: JournalBookReading): void => {
    last = reading;
    const { journal, quests } = reading;
    book.hidden = !journal.available && !quests.available;
    host.panel.dataset.journalPage = page;
    report(outcome, quests.outcome.outcome, quests.outcome.code, page === 'quests' ? quests.outcome.message : '');
    if (!changed({ reading, page })) return;

    const books = new Map(journal.books.map((entry) => [entry.kind, entry]));
    tabs.replaceChildren(
      ...PAGES.filter((id) => books.has(id) || (id === 'quests' && quests.available)).map((id) => {
        const tab = button(books.get(id)?.title ?? 'Current Quests', 'crawler-character-tab');
        tab.dataset.page = id;
        tab.dataset.open = id === page ? 'yes' : 'no';
        tab.addEventListener('click', () => {
          page = id;
          if (last !== null) render(last);
        });
        return tab;
      }),
    );

    const shown = books.get(page);
    title.textContent = shown?.title ?? (page === 'quests' ? 'Current Quests' : '');
    state.textContent = shown?.state ?? '';
    if (page === 'quests') {
      body.replaceChildren(...(quests.journal.length === 0 ? [hint('The party has taken no errand yet. People with work to give will say so.')] : quests.journal.map(questCard)));
    } else if (page === 'maps') {
      const open = button('Open the automap', 'crawler-journal-open-map');
      open.addEventListener('click', openMap);
      body.replaceChildren(open, ...(shown === undefined || shown.rows.length === 0 ? [hint('No place is known yet.')] : [rows(shown)]));
    } else {
      body.replaceChildren(shown === undefined || shown.rows.length === 0 ? hint('Nothing is written here yet.') : rows(shown));
    }
  };

  return { element: book, render };
}
