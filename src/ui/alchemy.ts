/**
 * The mixing screen: what the pack holds that mixes, which of the band a mixture may be asked of, the pairs the
 * product published, and the answer the last attempt got. Nothing here knows a recipe: the pairs are the pack's own
 * rows crossed with the game's own table, and the Mix control sends the two instance identities and the member the
 * chooser holds.
 */

import type { Fields } from './reader.js';
import { ACTIONS } from './actions.js';
import { button, element, head, picker, plural, redrawGuard, report, result, section, type Host, type Section } from './dom.js';

/** One thing in the pack that some mixture takes part in. */
export interface AlchemyItemView {
  readonly item: string;
  readonly definition: string;
  readonly name: string;
  readonly kind: string;
  readonly potency: number;
  readonly count: number;
}

/** One mixture the pack currently offers: two of the pack's rows, and nothing about what they will do. */
export interface AlchemyMixtureView {
  readonly first: string;
  readonly second: string;
  readonly firstName: string;
  readonly secondName: string;
}

/** What the last mixture did, or why nothing was mixed. */
export interface AlchemyOutcomeView {
  readonly member: number;
  readonly mixer: string;
  readonly outcome: string;
  readonly result: string;
  readonly resultName: string;
  readonly power: number;
  readonly burst: number;
  readonly harm: number;
  readonly condition: string;
  readonly note: number;
  readonly code: string;
  readonly message: string;
}

/** One character a mixture may be asked of. */
export interface AlchemyMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  /** The rung of mixing the character stands at, as the product published it. */
  readonly alchemy: number;
}

/** What mixes, who may mix it, whether a mixture would be taken now, and what the last attempt did. */
export interface AlchemyView {
  readonly available: boolean;
  readonly members: readonly AlchemyMemberView[];
  readonly items: readonly AlchemyItemView[];
  readonly mixtures: readonly AlchemyMixtureView[];
  /** Whether the product would take a mixture now, which is when the Mix controls are offered. */
  readonly canMix: boolean;
  readonly outcome: AlchemyOutcomeView;
}

export function readAlchemy(f: Fields): AlchemyView {
  const outcome = f.object('outcome');
  return {
    available: f.flag('available'),
    members: f.list('members', (entry) => ({
      index: entry.number('index'),
      member: entry.text('member'),
      name: entry.text('name'),
      alchemy: entry.number('alchemy'),
    })),
    items: f.list('items', (item) => ({
      item: item.text('item'),
      definition: item.text('definition'),
      name: item.text('name'),
      kind: item.text('kind'),
      potency: item.number('potency'),
      count: item.number('count'),
    })),
    mixtures: f.list('mixtures', (mixture) => ({
      first: mixture.text('first'),
      second: mixture.text('second'),
      firstName: mixture.text('firstName'),
      secondName: mixture.text('secondName'),
    })),
    canMix: f.flag('canMix'),
    outcome: {
      member: outcome.number('member'),
      mixer: outcome.text('mixer'),
      outcome: outcome.text('outcome'),
      result: outcome.text('result'),
      resultName: outcome.text('resultName'),
      power: outcome.number('power'),
      burst: outcome.number('burst'),
      harm: outcome.number('harm'),
      condition: outcome.text('condition'),
      note: outcome.number('note'),
      code: outcome.text('code'),
      message: outcome.text('message'),
    },
  };
}

/** Mounts the mixing screen. */
export function mountAlchemy(host: Host): Section<AlchemyView> {
  const { panel, claim } = host;
  const alchemy = section('crawler-alchemy');
  const state = element('p', 'crawler-alchemy-state');
  const items = element('div', 'crawler-alchemy-items');
  const mixtures = element('div', 'crawler-alchemy-mixtures');
  const outcome = result('crawler-alchemy-result');
  alchemy.append(head('Mixing'), state, items, mixtures, outcome);
  const changed = redrawGuard();

  const render = (view: AlchemyView): void => {
    panel.dataset.alchemy = view.available ? 'present' : 'none';
    panel.dataset.alchemyOutcome = view.outcome.outcome;
    alchemy.hidden = !view.available;
    state.textContent = !view.available
      ? ''
      : view.items.length === 0
        ? 'The pack holds nothing that mixes.'
        : `${view.items.length} thing${plural(view.items.length)} in the pack that mixes · ${view.mixtures.length} mixture${plural(view.mixtures.length)} to attempt`;
    report(outcome, view.outcome.outcome, view.outcome.code, view.outcome.message);
    outcome.dataset.result = view.outcome.result;
    if (!changed(view)) return;

    items.replaceChildren(
      ...view.items.map((item) => {
        const row = element('div', 'crawler-alchemy-item');
        row.dataset.item = item.item;
        row.dataset.definition = item.definition;
        row.dataset.kind = item.kind;
        row.dataset.potency = String(item.potency);
        const label = element('span', 'crawler-row-label');
        label.textContent = `${item.name} · ${item.kind} · strength ${item.potency}${item.count === 1 ? '' : ` · ${item.count}`}`;
        row.append(label);
        return row;
      }),
    );

    // Who mixes is the character's own mastery, so the chooser is the party's own rows and the rung each stands at;
    // the product refuses a mixture the chosen character's mastery does not reach.
    const mixers = view.members.map((member) => ({ value: String(member.index), text: `${member.name} (rung ${member.alchemy})` }));
    mixtures.replaceChildren(
      ...view.mixtures.map((mixture) => {
        const row = element('div', 'crawler-alchemy-mixture');
        row.dataset.first = mixture.first;
        row.dataset.second = mixture.second;
        const label = element('span', 'crawler-row-label');
        label.textContent = `${mixture.firstName} + ${mixture.secondName}`;
        row.append(label);
        const mixer = picker('crawler-alchemy-mixer', mixers);
        row.append(mixer);
        const mix = button('Mix', 'crawler-mix');
        mix.disabled = !view.canMix;
        mix.addEventListener('click', () => claim(ACTIONS.mix, { member: Number(mixer.value), first: mixture.first, second: mixture.second }));
        row.append(mix);
        return row;
      }),
    );
  };

  return { element: alchemy, render };
}
