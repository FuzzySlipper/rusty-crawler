/**
 * The quests book: each errand with its state, the words the game tells about it, every objective with the party's
 * own progress, and what the last offer, acceptance, or turn-in did. Nothing here judges a quest — whether an
 * objective is met, whether the errand may be handed in, and why a turn-in refused are the product's own answers.
 * An errand is taken and handed in by talking to somebody, so the book is a reading of the party's own state rather
 * than a screen with commands.
 */

import type { JournalBookView } from './journal.js';
import type { Fields } from './reader.js';
import { element, head, plural, result, section, type Host } from './dom.js';

/** One thing a quest asks for. */
export interface QuestObjectiveView {
  readonly id: string;
  readonly label: string;
  readonly count: number;
  readonly required: number;
  readonly met: boolean;
}

/** One quest the party stands with. */
export interface QuestJournalView {
  readonly quest: string;
  readonly name: string;
  readonly state: string;
  readonly giver: string;
  /** What the giver is called, in the game's words. */
  readonly giverName: string;
  readonly note: string;
  /** What the errand asks for that this game does not judge. */
  readonly residue: string;
  readonly objectives: readonly QuestObjectiveView[];
  readonly canTurnIn: boolean;
}

/** What the last errand did: what was offered, taken, or handed in, and what it paid. */
export interface QuestOutcomeView {
  readonly action: string;
  readonly outcome: string;
  readonly quest: string;
  readonly experience: number;
  readonly coins: number;
  readonly items: readonly string[];
  readonly records: readonly string[];
  readonly delivered: readonly string[];
  readonly code: string;
  readonly message: string;
}

/** Every errand the party stands with, and what the last one did. */
export interface QuestsView {
  readonly available: boolean;
  readonly journal: readonly QuestJournalView[];
  readonly outcome: QuestOutcomeView;
}

export function readQuests(f: Fields): QuestsView {
  return {
    available: f.flag('available'),
    journal: f.list('journal', (quest) => ({
      quest: quest.text('quest'),
      name: quest.text('name'),
      state: quest.text('state'),
      giver: quest.text('giver'),
      giverName: quest.text('giverName'),
      note: quest.text('note'),
      residue: quest.text('residue'),
      objectives: quest.list('objectives', (objective) => ({
        id: objective.text('id'),
        label: objective.text('label'),
        count: objective.number('count'),
        required: objective.number('required'),
        met: objective.flag('met'),
      })),
      canTurnIn: quest.flag('canTurnIn'),
    })),
    // The last errand's answer is published on the block itself, beside the journal it changed.
    outcome: {
      action: f.text('action', 'none'),
      outcome: f.text('outcome', 'none'),
      quest: f.text('quest'),
      experience: f.number('experience'),
      coins: f.number('coins'),
      items: f.words('items'),
      records: f.words('records'),
      delivered: f.words('delivered'),
      code: f.text('code'),
      message: f.text('message'),
    },
  };
}

/** What the quests book draws from: the errands, and the book's own title and state when the journal names them. */
export interface QuestsReading {
  readonly quests: QuestsView;
  readonly book: JournalBookView | undefined;
}

/** The mounted quests book, and the journal section it opens when the errands exist without a journal block. */
export interface QuestsSection {
  readonly element: HTMLElement;
  render(reading: QuestsReading, journal: HTMLElement): void;
}

/** Mounts the quests book. */
export function mountQuests(host: Host): QuestsSection {
  const { panel } = host;
  const quests = section('crawler-quests');
  const questsHead = head('Current Quests');
  const state = element('p', 'crawler-quests-state');
  const list = element('div', 'crawler-quests-list');
  const outcome = result('crawler-quests-result');
  quests.append(questsHead, state, list, outcome);

  const render = ({ quests: view, book }: QuestsReading, journal: HTMLElement): void => {
    panel.dataset.quests = view.available ? 'present' : 'none';
    panel.dataset.questsOutcome = view.outcome.outcome;
    quests.hidden = !view.available;
    // The errands belong to the quest owner rather than to the journal, so a session that publishes them without a
    // journal block still shows this book: hiding it because the mechanism beside it is absent would hide them.
    if (view.available) journal.hidden = false;
    quests.dataset.book = 'quests';
    quests.dataset.available = String(book?.available ?? view.available);
    // The book's own words when the product published them, and this companion's only when it did not.
    questsHead.textContent = book?.title ?? 'Current Quests';
    state.textContent =
      book !== undefined
        ? book.state
        : view.journal.length === 0
          ? 'The party has been offered nothing.'
          : `${view.journal.length} errand${plural(view.journal.length)} in the journal`;
    outcome.hidden = view.outcome.message === '';
    outcome.dataset.outcome = view.outcome.outcome;
    outcome.dataset.code = view.outcome.code;
    outcome.dataset.action = view.outcome.action;
    outcome.dataset.quest = view.outcome.quest;
    outcome.textContent = view.outcome.message;

    list.replaceChildren(
      ...view.journal.map((quest) => {
        const block = element('div', 'crawler-quest');
        block.dataset.quest = quest.quest;
        block.dataset.state = quest.state;
        block.dataset.giver = quest.giver;
        block.dataset.canTurnIn = String(quest.canTurnIn);
        const label = element('span', 'crawler-row-label');
        label.textContent = `${quest.name} · ${quest.state} · given by ${quest.giverName}`;
        block.append(label);
        if (quest.note !== '') {
          const note = element('div', 'crawler-quest-note');
          note.textContent = quest.note;
          block.append(note);
        }

        for (const objective of quest.objectives) {
          const line = element('div', 'crawler-quest-objective');
          line.dataset.objective = objective.id;
          line.dataset.met = String(objective.met);
          line.textContent =
            objective.required > 1
              ? `${objective.met ? '✓' : '·'} ${objective.label} (${objective.count}/${objective.required})`
              : `${objective.met ? '✓' : '·'} ${objective.label}`;
          block.append(line);
        }

        // What the errand asks for that this game does not judge is printed where the player reads the errand.
        if (quest.residue !== '') {
          const residue = element('div', 'crawler-quest-residue');
          residue.textContent = quest.residue;
          block.append(residue);
        }

        return block;
      }),
    );
  };

  return { element: quests, render };
}
