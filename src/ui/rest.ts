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
  /** Each stop as the mechanism would judge it now, in the order the controls are offered. */
  readonly offers: readonly RestOfferView[];
}

/** One stop as the rest mechanism would judge it now: how long, what it would cost, or why it would be refused. */
export interface RestOfferView {
  readonly kind: string;
  readonly offered: boolean;
  /** How long it would last, as the product words it; empty when it would be refused. */
  readonly period: string;
  /** What it would take from the larder, as the product words it; empty when nothing. */
  readonly charge: string;
  readonly code: string;
  readonly reason: string;
  /** The members a completed sleep would leave as they are, each with the product's reason. */
  readonly unrestored: readonly string[];
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
    offers: f.list('offers', (entry) => ({
      kind: entry.text('kind'),
      offered: entry.flag('offered'),
      period: entry.text('period'),
      charge: entry.text('charge'),
      code: entry.text('code'),
      reason: entry.text('reason'),
      unrestored: entry.words('unrestored'),
    })),
  };
}

/** What the stop section draws from: the last stop, and the five stop controls in the order they are offered. */
export interface RestReading {
  readonly rest: RestView;
  readonly controls: readonly [ControlView, ControlView, ControlView, ControlView, ControlView];
}

/**
 * The five stops in the order their controls arrive: the kind each is published as, its button's words, and the
 * shorter word an answer to it is headed by.
 */
export const STOPS = [
  { kind: 'rest', label: 'Rest & heal 8 hours', act: 'Rest' },
  { kind: 'camp', label: 'Make camp', act: 'Camp' },
  { kind: 'wait-dawn', label: 'Wait until dawn', act: 'Wait until dawn' },
  { kind: 'wait-hour', label: 'Wait an hour', act: 'Wait an hour' },
  { kind: 'wait-five-minutes', label: 'Wait 5 minutes', act: 'Wait 5 minutes' },
] as const;

/** Mounts the stop controls. */
export function mountRest(host: Host): Section<RestReading> {
  const { panel, claim } = host;
  const rest = section('crawler-rest');
  const state = element('p', 'crawler-rest-state');
  // Each stop is an option of its own: its button, and beside it what the mechanism says it would take — how long and
  // what it costs — or why it would be refused, and whom a night would leave as they are.
  const actions = element('div', 'crawler-actions');
  const options = STOPS.map(({ label }) => {
    const option = element('div', 'crawler-rest-option');
    const stop = button(label);
    stop.addEventListener('click', () => {
      if (stop.dataset.action !== undefined && stop.dataset.action !== '') claim(stop.dataset.action);
    });
    const judged = element('p', 'crawler-rest-judgment');
    const left = element('ul', 'crawler-rest-unrestored');
    option.append(stop, judged, left);
    actions.append(option);
    return { option, stop, judged, left };
  });
  const outcome = result('crawler-rest-result');
  rest.append(head('Rest, camp, and wait'), state, actions, outcome);

  /**
   * The buttons follow the product's answer for each stop, which judges the stop itself and whether the session would
   * take one now, rather than anything this screen reads of the mode. What a night cost is written out in full — the larder and what it
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
    options.forEach(({ option, stop, judged, left }, index) => {
      stop.dataset.id = controls[index].action;
      stop.dataset.action = controls[index].action;
      stop.disabled = !controls[index].enabled;
      // Each option reads the judgment published for its own kind, so the order of the two lists cannot pair a stop
      // with another's answer.
      const offer = view.offers.find((judged) => judged.kind === STOPS[index].kind);
      option.dataset.offered = offer === undefined ? 'unknown' : offer.offered ? 'yes' : 'no';
      judged.dataset.code = offer?.code ?? '';
      judged.textContent = offer === undefined
        ? ''
        : offer.offered
          ? `${offer.period}${offer.charge === '' ? ' · costs nothing' : ` · costs ${offer.charge}`}`
          : offer.reason;
      left.replaceChildren(...(offer?.unrestored ?? []).map((line) => {
        const item = element('li');
        item.textContent = `Not restored — ${line}`;
        return item;
      }));
      left.hidden = left.childElementCount === 0;
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
