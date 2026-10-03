/**
 * The inventory page of the character book: what the member being looked at wears, slot by slot, beside the party's
 * one shared pack, and an inspector for the thing a player picked — its picture, what it is, the facts the game
 * states about it, and the actions the product offers for it.
 *
 * Nothing here knows a slot or a rule. A pack row offers Equip because the figure has a place for it, Use because the
 * game names an action for it, and Read or Fire because the spellbook's own item row casts it; whether any of them is
 * taken is the product's answer, shown in the outcome line beneath. Which thing is picked is presentation state: it
 * changes nothing in the game and is forgotten once the thing is gone.
 */

import type { EquipmentPackView, EquipmentView, EquipmentWornView } from './equipment.js';
import { targetsOn, type MagicView, type SpellItemView } from './magic.js';
import { ACTIONS } from './actions.js';
import { button, element, heldPicker, plural, redrawGuard, report, result, type Host } from './dom.js';

/** What the inventory page draws from: the figure and pack, the item spells, and the member being looked at. */
export interface InventoryReading {
  readonly equipment: EquipmentView;
  readonly magic: MagicView;
  /** The member being looked at, by their place in the party, or -1 when there is nobody. */
  readonly member: number;
}

/** One thing a player may pick: a worn piece, with its slot, or a pack row. */
type Picked = { readonly kind: 'worn'; readonly slot: string; readonly row: EquipmentWornView } | { readonly kind: 'pack'; readonly row: EquipmentPackView };

/** A tile: the thing's picture, or its name where there is none to show. */
function tile(name: string, image: string, className: string): HTMLButtonElement {
  const made = button('', className);
  made.title = name;
  if (image !== '') {
    const picture = element('img', 'crawler-item-picture');
    picture.src = image;
    picture.alt = name;
    made.append(picture);
  } else {
    const words = element('span', 'crawler-item-words');
    words.textContent = name;
    made.append(words);
  }

  return made;
}

/** Mounts the inventory page. */
export function mountInventory(host: Host): { readonly element: HTMLElement; render(reading: InventoryReading): void } {
  const { panel, claim } = host;
  const page = element('div', 'crawler-inventory');
  const figure = element('div', 'crawler-inventory-figure');
  const pack = element('div', 'crawler-inventory-pack');
  const inspector = element('div', 'crawler-inventory-inspect');
  const outcome = result('crawler-inventory-outcome');
  const used = result('crawler-inventory-use-outcome');
  const cast = result('crawler-inventory-cast-result');
  // The answers come first, where a player looking at the thing they just pressed will see them.
  page.append(outcome, used, cast, figure, pack, inspector);
  const changed = redrawGuard();
  let picked = '';
  // The target chosen for each member's use of an item, kept across redraws so a cast names the actor the player chose.
  const aimed = new Map<string, string>();

  /** The inspector for one picked thing: what it is, and what the product offers for it. */
  const inspect = (reading: InventoryReading, choice: Picked, spell: SpellItemView | undefined): HTMLElement[] => {
    const { equipment, magic, member } = reading;
    const row = choice.row;
    const lines: HTMLElement[] = [];
    const heading = element('div', 'crawler-inspect-head');
    heading.append(tile(row.name, row.image, 'crawler-inspect-picture'));
    const title = element('div');
    const name = element('p', 'crawler-inspect-name');
    name.textContent = row.name;
    const kind = element('p', 'crawler-inspect-kind');
    kind.textContent = choice.kind === 'worn' ? `${row.kind} · worn in the ${choice.slot}` : row.kind;
    title.append(name, kind);
    heading.append(title);
    lines.push(heading);
    const facts = element('ul', 'crawler-inspect-facts');
    facts.append(
      ...row.facts.map((fact) => {
        const item = element('li');
        item.textContent = fact;
        return item;
      }),
    );
    lines.push(facts);
    if (choice.kind === 'pack' && choice.row.retained !== '') {
      const retained = element('p', 'crawler-inspect-retained');
      retained.textContent = choice.row.retained;
      lines.push(retained);
    }

    const actions = element('div', 'crawler-inspect-actions');
    if (member >= 0) {
      if (choice.kind === 'worn') {
        const off = button('Take off', 'crawler-inventory-unequip');
        off.disabled = !equipment.canEquip;
        off.addEventListener('click', () => claim(ACTIONS.unequip, { member, slot: choice.slot }));
        actions.append(off);
      } else {
        if (choice.row.slots.length > 0) {
          const on = button(`Equip (${choice.row.slots.join(' or ')})`, 'crawler-inventory-equip');
          on.disabled = !equipment.canEquip;
          on.addEventListener('click', () => claim(ACTIONS.equip, { member, item: choice.row.item }));
          actions.append(on);
        }

        if (choice.row.use !== '') {
          const use = button(choice.row.use, 'crawler-inventory-use');
          use.disabled = !equipment.canEquip;
          use.addEventListener('click', () => claim(ACTIONS.useItem, { member, item: choice.row.item }));
          actions.append(use);
        }
      }

      // A scroll is read and a wand fired through the spellbook's own casting, with the targets that spell takes.
      if (spell !== undefined) {
        const targets = targetsOn(magic, spell.targetSide);
        const target =
          targets.length === 0
            ? null
            : heldPicker('crawler-inventory-target', targets.map((entry) => ({ value: entry.target, text: entry.name })), aimed, `${member}:${spell.item}`);
        if (target !== null) actions.append(target);
        const fire = button(spell.kind === 'charged' ? `Fire ${spell.spellName}` : `Use: ${spell.spellName}`, 'crawler-inventory-cast');
        fire.disabled = !spell.canUse;
        fire.addEventListener('click', () =>
          claim(ACTIONS.cast, { member, spell: spell.spell, target: target === null ? '' : target.value, item: spell.item }),
        );
        actions.append(fire);
      }
    }

    lines.push(actions);
    return lines;
  };

  const render = (reading: InventoryReading): void => {
    const { equipment, magic, member } = reading;
    report(outcome, equipment.outcome.outcome, equipment.outcome.code, equipment.outcome.message);
    report(used, equipment.useOutcome.outcome, equipment.useOutcome.code, equipment.useOutcome.message);
    report(cast, magic.outcome, magic.code, magic.source === '' ? '' : magic.message);
    if (!changed({ reading, picked })) return;

    const wearer = equipment.members.find((entry) => entry.index === member);
    const choices = new Map<string, Picked>();
    for (const worn of wearer?.worn ?? []) choices.set(`worn:${worn.item}`, { kind: 'worn', slot: worn.slot, row: worn });
    for (const row of equipment.pack) choices.set(`pack:${row.item}`, { kind: 'pack', row });
    // A picked thing that is gone — worn by somebody else now, drunk, sold — is forgotten rather than shown stale.
    if (!choices.has(picked)) picked = '';
    page.dataset.picked = picked;

    const pick = (key: string): void => {
      picked = key;
      render(reading);
    };

    // The member's figure: every slot the game's figure lists, empty ones included, so a player sees where things go.
    const heading = element('p', 'crawler-row-label');
    heading.textContent = wearer === undefined ? 'Nobody to wear anything' : `Worn by ${wearer.name}`;
    const slots = element('div', 'crawler-inventory-slots');
    for (const slot of equipment.slots) {
      const cell = element('div', 'crawler-inventory-slot');
      cell.dataset.slot = slot;
      const label = element('span', 'crawler-slot-label');
      label.textContent = slot;
      const worn = wearer?.worn.find((entry) => entry.slot === slot);
      if (worn === undefined) {
        const empty = element('span', 'crawler-slot-empty');
        empty.textContent = '—';
        cell.append(label, empty);
      } else {
        const key = `worn:${worn.item}`;
        const piece = tile(worn.name, worn.image, 'crawler-item-tile');
        piece.dataset.item = worn.item;
        piece.dataset.picked = String(key === picked);
        piece.addEventListener('click', () => pick(key));
        cell.append(label, piece);
      }

      slots.append(cell);
    }

    if (wearer !== undefined && wearer.powers !== '') {
      const powers = element('p', 'crawler-inventory-powers');
      powers.textContent = wearer.powers;
      figure.replaceChildren(heading, slots, powers);
    } else {
      figure.replaceChildren(heading, slots);
    }

    // The party's one pack: everything it carries, wearable or not.
    const packHead = element('p', 'crawler-row-label');
    packHead.textContent = equipment.pack.length === 0 ? 'The party’s pack is empty' : `The party’s pack · ${equipment.pack.length} item${plural(equipment.pack.length)}`;
    const grid = element('div', 'crawler-inventory-grid');
    for (const row of equipment.pack) {
      const key = `pack:${row.item}`;
      const piece = tile(row.name, row.image, 'crawler-item-tile');
      piece.dataset.item = row.item;
      piece.dataset.picked = String(key === picked);
      if (row.retained !== '') piece.dataset.retained = 'yes';
      piece.addEventListener('click', () => pick(key));
      grid.append(piece);
    }

    pack.replaceChildren(packHead, grid);

    const choice = choices.get(picked);
    if (choice === undefined) {
      const hint = element('p', 'crawler-inspect-hint');
      hint.textContent = 'Pick something worn or in the pack to inspect it.';
      inspector.replaceChildren(hint);
      return;
    }

    const spell = magic.items.find((entry) => entry.item === choice.row.item);
    inspector.replaceChildren(...inspect(reading, choice, spell));
  };

  panel.dataset.inventory = 'mounted';
  return { element: page, render };
}
