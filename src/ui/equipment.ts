/**
 * The figure screen: what each member wears, what in the pack the game's figure has a place for, and the answer the
 * last change got. Nothing here knows a slot or a rule: the slots are the product's words, a pack row is listed
 * because the product published it, and whether this member may wear it is judged when the Equip control is pressed —
 * a refusal names the skill, the mastery, or the full hand.
 */

import type { Fields } from './reader.js';
import { ACTIONS } from './actions.js';
import { button, element, head, picker, plural, redrawGuard, report, result, section, type Host, type Section } from './dom.js';

/** One thing a member wears. */
export interface EquipmentWornView {
  readonly slot: string;
  readonly item: string;
  readonly definition: string;
  readonly name: string;
}

/** One member's figure. */
export interface EquipmentMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly worn: readonly EquipmentWornView[];
}

/** One thing in the pack the figure has a place for, with the places it is shaped for. */
export interface EquipmentItemView {
  readonly item: string;
  readonly definition: string;
  readonly name: string;
  readonly slots: readonly string[];
}

/** What the last change did, or why nothing changed. */
export interface EquipmentOutcomeView {
  readonly outcome: string;
  readonly code: string;
  readonly message: string;
  readonly member: number;
  readonly wearer: string;
  readonly slot: string;
  readonly item: string;
  readonly itemName: string;
  readonly displaced: string;
  readonly displacedName: string;
}

/** The figures, the pack's wearable things, whether a change would be taken now, and what the last one did. */
export interface EquipmentView {
  readonly available: boolean;
  readonly slots: readonly string[];
  readonly members: readonly EquipmentMemberView[];
  readonly items: readonly EquipmentItemView[];
  /** Whether the product would take a change now, which is when the Equip and Take off controls are offered. */
  readonly canEquip: boolean;
  readonly outcome: EquipmentOutcomeView;
}

export function readEquipment(f: Fields): EquipmentView {
  const outcome = f.object('outcome');
  return {
    available: f.flag('available'),
    slots: f.words('slots'),
    members: f.list('members', (entry) => ({
      index: entry.number('index'),
      member: entry.text('member'),
      name: entry.text('name'),
      worn: entry.list('worn', (worn) => ({
        slot: worn.text('slot'),
        item: worn.text('item'),
        definition: worn.text('definition'),
        name: worn.text('name'),
      })),
    })),
    items: f.list('items', (item) => ({
      item: item.text('item'),
      definition: item.text('definition'),
      name: item.text('name'),
      slots: item.words('slots'),
    })),
    canEquip: f.flag('canEquip'),
    outcome: {
      outcome: outcome.text('outcome'),
      code: outcome.text('code'),
      message: outcome.text('message'),
      member: outcome.number('member'),
      wearer: outcome.text('wearer'),
      slot: outcome.text('slot'),
      item: outcome.text('item'),
      itemName: outcome.text('itemName'),
      displaced: outcome.text('displaced'),
      displacedName: outcome.text('displacedName'),
    },
  };
}

/** Mounts the figure screen. */
export function mountEquipment(host: Host): Section<EquipmentView> {
  const { panel, claim } = host;
  const equipment = section('crawler-equipment');
  const state = element('p', 'crawler-equipment-state');
  const figures = element('div', 'crawler-equipment-figures');
  const pack = element('div', 'crawler-equipment-pack');
  const outcome = result('crawler-equipment-result');
  equipment.append(head('Equipment'), state, figures, pack, outcome);
  const changed = redrawGuard();

  const render = (view: EquipmentView): void => {
    panel.dataset.equipment = view.available ? 'present' : 'none';
    panel.dataset.equipmentOutcome = view.outcome.outcome;
    equipment.hidden = !view.available;
    state.textContent = !view.available
      ? ''
      : view.items.length === 0
        ? 'The pack holds nothing to wear.'
        : `${view.items.length} thing${plural(view.items.length)} in the pack to wear`;
    report(outcome, view.outcome.outcome, view.outcome.code, view.outcome.message);
    outcome.dataset.slot = view.outcome.slot;
    if (!changed(view)) return;

    // Each member's figure, slot by slot as the product listed them; taking a thing off names the member and the slot.
    figures.replaceChildren(
      ...view.members.map((member) => {
        const figure = element('div', 'crawler-equipment-member');
        figure.dataset.member = String(member.index);
        const name = element('span', 'crawler-row-label');
        name.textContent = member.worn.length === 0 ? `${member.name} wears nothing` : member.name;
        figure.append(name);
        for (const worn of member.worn) {
          const row = element('div', 'crawler-equipment-worn');
          row.dataset.slot = worn.slot;
          row.dataset.item = worn.item;
          const label = element('span', 'crawler-row-label');
          label.textContent = `${worn.slot}: ${worn.name}`;
          const off = button('Take off', 'crawler-unequip');
          off.disabled = !view.canEquip;
          off.addEventListener('click', () => claim(ACTIONS.unequip, { member: member.index, slot: worn.slot }));
          row.append(label, off);
          figure.append(row);
        }

        return figure;
      }),
    );

    // Who wears it is the player's choice; whether they may is the product's answer when Equip is pressed.
    const wearers = view.members.map((member) => ({ value: String(member.index), text: member.name }));
    pack.replaceChildren(
      ...view.items.map((item) => {
        const row = element('div', 'crawler-equipment-item');
        row.dataset.item = item.item;
        row.dataset.definition = item.definition;
        const label = element('span', 'crawler-row-label');
        label.textContent = `${item.name} · ${item.slots.join(' or ')}`;
        const wearer = picker('crawler-equipment-wearer', wearers);
        const on = button('Equip', 'crawler-equip');
        on.disabled = !view.canEquip;
        on.addEventListener('click', () => claim(ACTIONS.equip, { member: Number(wearer.value), item: item.item }));
        row.append(label, wearer, on);
        return row;
      }),
    );
  };

  return { element: equipment, render };
}
