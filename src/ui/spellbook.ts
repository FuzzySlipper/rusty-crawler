/**
 * The spellbook: each member's own spells with what casting one costs that caster, the actors a casting may be
 * aimed at, what the last casting changed and what is running, and the magic the pack carries.
 *
 * Nothing here is computed: the price, the rung, the aim, the side a spell's target stands on, and whether a casting
 * could be aimed at all are the product's own answers, and the panel's buttons name the row a player pressed.
 */

import { aimRows, targetsOn, type MagicView, type SpellTargetView } from './magic.js';
import { ACTIONS } from './actions.js';
import { button, element, head, picker, plural, redrawGuard, report, result, section, type Host, type Section } from './dom.js';

/** Mounts the spellbook. */
export function mountSpellbook(host: Host): Section<MagicView> {
  const { panel, claim } = host;
  const magic = section('crawler-magic');
  const state = element('p', 'crawler-magic-state');
  const members = element('div', 'crawler-magic-members');
  const outcome = result('crawler-magic-result');
  const facts = element('ul', 'crawler-magic-facts');
  facts.hidden = true;
  const running = element('p', 'crawler-magic-running');
  running.hidden = true;
  // What runs on each character, as its own list: a ward cast on one member has to be readable as that member's,
  // beside the party's own carried effects rather than mixed into them.
  const memberRunning = element('ul', 'crawler-magic-member-running');
  memberRunning.hidden = true;
  // The magic the party carries in its pack: a scroll it can read and a wand it can fire, each with what is left of
  // it and the controls that use it, which send back the instance identity the row was handed.
  const items = element('div', 'crawler-magic-items');
  items.hidden = true;
  magic.append(head('Spellbook'), state, members, outcome, facts, running, memberRunning, items);
  const changed = redrawGuard();

  const render = (view: MagicView): void => {
    panel.dataset.magic = view.available ? 'present' : 'none';
    panel.dataset.magicOutcome = view.outcome;
    magic.hidden = !view.available;
    magic.dataset.caster = view.caster;
    magic.dataset.source = view.source;
    state.textContent = view.available
      ? `${view.members.length} character${plural(view.members.length)} · ${view.targets.length} target${plural(view.targets.length)} in reach`
      : '';
    report(outcome, view.outcome, view.code, view.message);

    // What the casting changed, as the product's own readings of the state it changed.
    facts.replaceChildren(
      ...view.facts.map((fact) => {
        const item = element('li', 'crawler-magic-fact');
        item.dataset.fact = fact.name;
        item.textContent = `${fact.name}: ${fact.value}`;
        return item;
      }),
    );
    facts.hidden = view.facts.length === 0;

    // What the party sees by, and what spells have left running with the moment each one lapses.
    running.dataset.sight = view.sight;
    running.textContent = [
      view.sight === '' ? '' : `sight: ${view.sight}`,
      view.running.length === 0
        ? ''
        : `running: ${view.running
            .map((effect) => `${effect.effect} (${effect.magnitude})${effect.endsAt === '' ? '' : ` until ${effect.endsAt}`}`)
            .join(', ')}`,
    ]
      .filter((part) => part !== '')
      .join(' · ');
    running.hidden = running.textContent === '';

    memberRunning.replaceChildren(
      ...view.memberRunning.map((effect) => {
        const row = element('li', 'crawler-magic-member-running-row');
        row.dataset.member = effect.member;
        row.dataset.effect = effect.effect;
        row.dataset.endsAt = effect.endsAt;
        row.textContent = `${effect.name}: ${effect.effect} (${effect.magnitude})${effect.endsAt === '' ? '' : ` until ${effect.endsAt}`}`;
        return row;
      }),
    );
    memberRunning.hidden = view.memberRunning.length === 0;
    if (!changed(view)) return;

    const candidates = (side: string): readonly SpellTargetView[] => targetsOn(view, side);
    const casters = view.members.map((member) => ({ value: String(member.index), text: member.name }));

    items.replaceChildren(
      ...view.items.map((item) => {
        const row = element('div', 'crawler-magic-item');
        row.dataset.item = item.item;
        row.dataset.kind = item.kind;
        row.dataset.spell = item.spell;
        row.dataset.targeting = item.targeting;
        row.dataset.charges = String(item.charges);
        row.dataset.chargesMax = String(item.chargesMax);
        row.dataset.wielded = item.wielded ? 'wielded' : 'packed';
        const label = element('span', 'crawler-row-label');
        label.textContent =
          item.kind === 'charged'
            ? `${item.name} — ${item.spellName} · ${item.charges}/${item.chargesMax} charge${plural(item.chargesMax)} · ${item.wielded ? `wielded by ${item.member}` : 'not wielded'}`
            : `${item.name} — ${item.spellName} · one use`;
        row.append(label);
        const caster = picker('crawler-item-member', casters);
        row.append(caster);
        const targets = candidates(item.targetSide);
        const target = targets.length === 0 ? null : picker('crawler-target', targets.map((entry) => ({ value: entry.target, text: entry.name })));
        if (target !== null) row.append(target);
        const use = button(item.kind === 'charged' ? 'Fire it' : 'Read it', 'crawler-use-item');
        use.disabled = !item.canUse;
        use.addEventListener('click', () =>
          claim(ACTIONS.cast, { member: Number(caster.value), spell: item.spell, target: target === null ? '' : target.value, item: item.item }),
        );
        row.append(use);
        return row;
      }),
    );
    items.hidden = view.items.length === 0;

    members.replaceChildren(
      ...view.members.map((member) => {
        const block = element('div', 'crawler-magic-member');
        block.dataset.member = member.member;
        const label = element('span', 'crawler-row-label');
        label.textContent = `${member.name} — ${member.class} · ${member.spellPoints}/${member.spellPointsMax} spell points · quick ${
          member.quickSpellName === '' ? 'none' : member.quickSpellName
        }`;
        block.append(label);
        for (const row of member.spells) {
          const line = element('div', 'crawler-spell');
          line.dataset.spell = row.spell;
          line.dataset.school = row.school;
          line.dataset.targeting = row.targeting;
          line.dataset.tierRung = String(row.tierRung);
          const text = element('span');
          text.textContent = `${row.name} · ${row.school} · ${row.tier} · ${row.cost} point${plural(row.cost)} · ${row.targeting} · ${row.effect}`;
          line.append(text);

          const aims = aimRows(view, row);
          const choice = aims.length > 0 ? picker('crawler-target', aims) : null;
          if (choice !== null) line.append(choice);

          const cast = button(`Cast for ${row.cost}`, 'crawler-cast');
          cast.disabled = !row.canCast;
          cast.addEventListener('click', () =>
            claim(ACTIONS.cast, { member: member.index, spell: row.spell, target: choice === null ? '' : choice.value }),
          );
          line.append(cast);

          const quick = button(member.quickSpell === row.spell ? 'Clear quick spell' : 'Make quick spell', 'crawler-quick');
          quick.addEventListener('click', () =>
            claim(ACTIONS.quickSpell, { member: member.index, spell: member.quickSpell === row.spell ? '' : row.spell }),
          );
          line.append(quick);
          block.append(line);
        }

        return block;
      }),
    );
  };

  return { element: magic, render };
}
