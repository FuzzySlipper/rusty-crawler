/**
 * The stop controls: one button per act, and the answer the last one got. A rest heals and a wait does not, so the
 * buttons are never collapsed into one; and the fatigue line is the clock's own deadline, which is why a player can
 * see when the party next needs to sleep.
 */

import type { ControlView } from './overview.js';
import type { Fields } from './reader.js';
import { button, element, head, result, section, type Host, type Section } from './dom.js';

/**
 * What the party's last stop did, what it cost, and what going without sleep is doing to it. `available` is false
 * when the session holds no rest mechanism; `outcome` is `none` until the party has stopped; `interrupted` says a
 * night was broken; and the fatigue facts are the clock's own deadline rather than a count kept here.
 */
export interface RestView {
  readonly available: boolean;
  /** What the last stop asked for: `rest`, `camp`, `wait-dawn`, `wait-hour`, or `wait-five-minutes`. */
  readonly kind: string;
  /** `none`, `applied`, or `refused`. */
  readonly outcome: string;
  readonly code: string;
  readonly message: string;
  /** Where the clock stood when the stop was asked for, and where it stands now. */
  readonly from: string;
  readonly to: string;
  readonly elapsedSeconds: number;
  /** What the larder was charged and what it covered, in the unit the party's food is measured in. */
  readonly charged: number;
  readonly covered: number;
  readonly unit: string;
  readonly interrupted: boolean;
  readonly recovered: boolean;
  /** How many members a completed sleep restored. */
  readonly restored: number;
  /** The conditions a completed sleep cleared, empty when it cleared none. */
  readonly cleared: string;
  /** The state the larder's own rule left on the party, empty when it left none. */
  readonly shortage: string;
  /** Whether the party currently carries the state going without sleep puts on it. */
  readonly tired: boolean;
  /** When the debt of sleep next falls due, empty while the party is asleep. */
  readonly fatigueDue: string;
  /** How many times the debt has fallen due since the session began. */
  readonly fatigueLanded: number;
}

export function readRest(f: Fields): RestView {
  return {
    available: f.flag('available'),
    kind: f.text('kind'),
    outcome: f.text('outcome', 'none'),
    code: f.text('code'),
    message: f.text('message'),
    from: f.text('from'),
    to: f.text('to'),
    elapsedSeconds: f.number('elapsedSeconds'),
    charged: f.number('charged'),
    covered: f.number('covered'),
    unit: f.text('unit'),
    interrupted: f.flag('interrupted'),
    recovered: f.flag('recovered'),
    restored: f.number('restored'),
    cleared: f.text('cleared'),
    shortage: f.text('shortage'),
    tired: f.flag('tired'),
    fatigueDue: f.text('fatigueDue'),
    fatigueLanded: f.number('fatigueLanded'),
  };
}

/** What the stop section draws from: the last stop, and the five stop controls in the order they are offered. */
export interface RestReading {
  readonly rest: RestView;
  readonly controls: readonly [ControlView, ControlView, ControlView, ControlView, ControlView];
}

/** The five stop buttons' words, in the order the controls arrive. */
const LABELS = ['Rest & heal 8 hours', 'Make camp', 'Wait until dawn', 'Wait an hour', 'Wait 5 minutes'] as const;

/** Mounts the stop controls. */
export function mountRest(host: Host): Section<RestReading> {
  const { panel, claim } = host;
  const rest = section('crawler-rest');
  const state = element('p', 'crawler-rest-state');
  const actions = element('div', 'crawler-actions');
  const buttons = LABELS.map((label) => {
    const stop = button(label);
    stop.addEventListener('click', () => {
      if (stop.dataset.action !== undefined && stop.dataset.action !== '') claim(stop.dataset.action);
    });
    actions.append(stop);
    return stop;
  });
  const outcome = result('crawler-rest-result');
  rest.append(head('Rest, camp, and wait'), state, actions, outcome);

  /**
   * The buttons follow the product's answer for each stop rather than the mode, because a stop is an instant: a
   * held session still lets the party sleep. What a night cost is written out in full — the larder and what it
   * cleared — because "the party rested" and "it rested and it cost two portions" are different facts.
   */
  const render = ({ rest: view, controls }: RestReading): void => {
    // A session with no mechanism, a party that has not stopped, and a refused stop are three different facts.
    panel.dataset.rest = !view.available ? 'none' : view.outcome === 'none' ? 'ready' : view.outcome;
    panel.dataset.restAction = view.kind;
    panel.dataset.restOutcome = view.outcome;
    panel.dataset.tired = view.tired ? 'yes' : 'no';
    rest.hidden = !view.available;
    state.textContent = !view.available
      ? ''
      : view.tired
        ? `Tired${view.fatigueDue === '' ? '' : ` · next sleep due ${view.fatigueDue}`}${view.fatigueLanded > 0 ? ` · landed ${view.fatigueLanded}×` : ''}`
        : `Rested${view.fatigueDue === '' ? '' : ` · next sleep due ${view.fatigueDue}`}`;
    buttons.forEach((stop, index) => {
      stop.dataset.id = controls[index].action;
      stop.dataset.action = controls[index].action;
      stop.disabled = !controls[index].enabled;
    });
    outcome.hidden = view.message === '';
    outcome.dataset.outcome = view.outcome;
    outcome.dataset.code = view.code;
    // What a night cost and what it cleared are both appended when both are known: a rest that spent the last of
    // the larder and left the party weak is exactly the case a player needs to read in full.
    outcome.textContent =
      view.message === ''
        ? ''
        : `${view.message}${view.charged > 0 ? ` Cost ${view.covered} of ${view.charged} ${view.unit}.` : ''}${
            view.cleared === '' ? '' : ` Cleared: ${view.cleared}.`
          }`;
  };

  return { element: rest, render };
}
