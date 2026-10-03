/**
 * The answer to the party's latest act: the one line along the top of the adventure frame.
 *
 * The product publishes the latest answer whichever owner gave it, numbered and naming the act and what it concerned,
 * so the line is always the answer to what the player last did: a sign read before an attack is replaced by the attack's
 * own answer, and nothing here picks among the owners' older results. The words that name each act are presentation.
 */

import type { Fields } from './reader.js';
import { STOPS } from './rest.js';

/** The latest answer, as the product published it. */
export interface FeedbackView {
  /** How many answers the session has given; zero before the first. */
  readonly serial: number;
  /** Which act it answers: `use`, `attack`, `cast`, `stop`, `save`, `counter`, `conversation`, `item`, `mix`, or `equip`. */
  readonly source: string;
  readonly actor: string;
  readonly subject: string;
  /** `applied`, `refused`, or `none` before the first. */
  readonly outcome: string;
  readonly code: string;
  readonly message: string;
}

export function readFeedback(f: Fields): FeedbackView {
  return {
    serial: f.number('serial'),
    source: f.text('source'),
    actor: f.text('actor'),
    subject: f.text('subject'),
    outcome: f.text('outcome', 'none'),
    code: f.text('code'),
    message: f.text('message'),
  };
}

/** The word each act is shown under. */
const ACTS: Readonly<Record<string, string>> = {
  use: 'Use',
  attack: 'Attack',
  cast: 'Cast',
  stop: 'Stop',
  save: 'Save',
  counter: 'Counter',
  conversation: 'Talk',
  item: 'Item',
  mix: 'Mix',
  equip: 'Equip',
};


/**
 * The heading an answer is shown under: the act, and what it concerned — "Use · Lever", "Attack · Roderick → Giant
 * Rat", "Rest" — so the line reads as the answer to that act and no other.
 */
export function feedbackHeading(view: FeedbackView): string {
  if (view.source === 'stop') return STOPS.find((stop) => stop.kind === view.subject)?.act ?? ACTS.stop;
  const act = ACTS[view.source] ?? view.source;
  const who = view.actor === '' ? '' : view.actor;
  // A sentence that already begins with what it concerned is not headed by it twice.
  const named = view.subject !== '' && view.message.startsWith(`${view.subject}:`);
  const what = view.source === 'save' || named ? '' : view.subject;
  const aimed = who !== '' && what !== '' ? `${who} → ${what}` : who !== '' ? who : what;
  return aimed === '' ? act : `${act} · ${aimed}`;
}
