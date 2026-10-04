/**
 * The magic block: what each member can cast and at what price, what a casting may be aimed at, what the last
 * casting changed, what spells have left running, and the magic the party carries in its pack.
 *
 * Every price, rung, aim, side, and verdict is the product's own published answer: the panel offers a spell's
 * targets by the side the product says the spell names, and a casting's control by the product's own word on
 * whether it could be cast now.
 */

import type { Fields } from './reader.js';

/** Something a spell whose aim names no actor may be pointed at. */
export interface SpellAimView {
  /** The identity a casting echoes back to choose this. */
  readonly aim: string;
  readonly name: string;
  /** What sort of thing it is, as the game's own word. */
  readonly kind: string;
}

/** One effect a spell has left running on the party. */
export interface SpellRunningView {
  /** The effect identity the spell left, which the panel shows and never interprets. */
  readonly effect: string;
  readonly magnitude: number;
  /** When the clock ends it, empty when nothing has said. */
  readonly endsAt: string;
}

/** One effect a spell has left running on one character. */
export interface SpellMemberRunningView {
  /** The member's durable identity, which the row belongs to. */
  readonly member: string;
  readonly name: string;
  readonly effect: string;
  readonly magnitude: number;
  readonly endsAt: string;
}

/** One item the party holds that carries a spell. */
export interface SpellItemView {
  /** The instance identity a use command echoes back. */
  readonly item: string;
  readonly name: string;
  /** How using it spends it: `consumed` for an item a use uses up, `charged` for one that holds uses. */
  readonly kind: string;
  readonly spell: string;
  readonly spellName: string;
  /** What the spell it carries is aimed at: `none`, `caster`, `ally`, `foe`, `either`, or `party`. */
  readonly targeting: string;
  readonly charges: number;
  readonly chargesMax: number;
  /** Whether a member has it equipped, which is what an item that holds charges needs. */
  readonly wielded: boolean;
  /** The member wearing it, empty when nobody does. */
  readonly member: string;
  /** Which side the actor its spell names stands on, `party` or `opposition`, or `any` for either; empty when it names nobody. */
  readonly targetSide: string;
  /** Whether the product would take a use of it now. */
  readonly canUse: boolean;
}

/** One reading a cast left behind. */
export interface SpellFactView {
  readonly name: string;
  readonly value: string;
}

/** One spell a member knows. */
export interface SpellRowView {
  readonly spell: string;
  readonly name: string;
  readonly school: string;
  /** The rung of the school's ladder the spell asks for, as the game's own word. */
  readonly tier: string;
  readonly tierRung: number;
  /** What one casting costs this caster, as the product's own answer for that member. */
  readonly cost: number;
  /** What the spell is aimed at: `none`, `caster`, `ally`, `foe`, `either`, or `party`. */
  readonly targeting: string;
  /** The effect identity the spell carries, which the panel shows and never interprets. */
  readonly effect: string;
  /** What it may be pointed at when its aim names no actor, empty when its aim names nothing. */
  readonly aims: readonly SpellAimView[];
  /** Which side the actor it names stands on, `party` or `opposition`, or `any` for either; empty when it names nobody. */
  readonly targetSide: string;
  /** Whether a casting of it could be aimed at all now, which is when its control is offered. */
  readonly canCast: boolean;
}

/** One spell on a school's page, learned or not, with what would stop this member casting it. */
export interface SpellPageEntryView {
  readonly spell: string;
  readonly name: string;
  readonly tier: string;
  readonly known: boolean;
  readonly cost: number;
  readonly refusalCode: string;
  /** Why the member could not cast it now — not learned, too low a rung, too few points — or empty. */
  readonly refusal: string;
}

/** One school's page of a member's spellbook. */
export interface SpellPageView {
  readonly school: string;
  /** The game's word for the rung of the school the member holds. */
  readonly held: string;
  readonly spells: readonly SpellPageEntryView[];
}

/** One member's spellbook and what casting from it costs. */
export interface MagicMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly class: string;
  readonly spellPoints: number;
  readonly spellPointsMax: number;
  readonly quickSpell: string;
  readonly quickSpellName: string;
  readonly spells: readonly SpellRowView[];
  /** The member's spellbook by school, every spell of each school they hold. */
  readonly pages: readonly SpellPageView[];
}

/** One actor or body a casting may be aimed at, with the side it is on. */
export interface SpellTargetView {
  readonly target: string;
  readonly name: string;
  /** Which side the actor is on: `party` or `opposition`. */
  readonly side: string;
  /** The step of place units the fight's distance falls within, zero for a member; the product lists the nearest first. */
  readonly distance: number;
  /** `actor` for a live member or creature; `body` for a downed creature a body-capable spell may name. */
  readonly kind: string;
}

/** What the party can cast, what it may aim at, and what the last casting did. */
export interface MagicView {
  readonly available: boolean;
  readonly members: readonly MagicMemberView[];
  readonly targets: readonly SpellTargetView[];
  readonly outcome: string;
  /** The caster's place in the party, zero before any casting. */
  readonly member: number;
  readonly caster: string;
  readonly spell: string;
  readonly cost: number;
  readonly target: string;
  readonly effect: string;
  readonly code: string;
  readonly message: string;
  /** What the last casting changed, as readings of the state it changed. */
  readonly facts: readonly SpellFactView[];
  /** The effects spells have left running on the party, in the order they were applied. */
  readonly running: readonly SpellRunningView[];
  /** The effects spells have left running on the party's characters, each naming the character. */
  readonly memberRunning: readonly SpellMemberRunningView[];
  /** The items the party carries that hold a spell, in the order the pack holds them. */
  readonly items: readonly SpellItemView[];
  /** What carried the last casting, empty when it came from the caster's own spellbook. */
  readonly source: string;
  /** What the party sees by: `daylight`, `light`, `dark`, or empty when nothing states it. */
  readonly sight: string;
}

/**
 * The actors on the side the product says a casting names, every one for `any`, or none when it names nobody.
 * Body rows are hidden by default because ordinary foe and item uses cannot name a body; an `either` spell opts in
 * through its row so a ruleset effect such as reanimation can retain the product's durable target identity.
 */
export function targetsOn(view: MagicView, side: string, includeBodies = false): readonly SpellTargetView[] {
  const targets = side === '' ? [] : side === 'any' ? view.targets : view.targets.filter((target) => target.side === side);
  return includeBodies ? targets : targets.filter((target) => target.kind !== 'body');
}

/**
 * What a casting of a spell may be pointed at, as the rows a picker offers: the actors on the side its aim names, else
 * the places or things the product says it may be pointed at, else nothing — a spell neither names is cast with no target.
 */
export function aimRows(view: MagicView, row: SpellRowView): readonly { readonly value: string; readonly text: string }[] {
  const targets = targetsOn(view, row.targetSide, row.targeting === 'either');
  if (targets.length === 0) return row.aims.map((aim) => ({ value: aim.aim, text: `${aim.name} (${aim.kind})` }));

  // The wire keeps durable identities in option values, while the words a player sees must distinguish two
  // creatures with one name. Targets arrive nearest first from the fight's current poses; use that order for
  // a natural near-to-far qualifier, even when both fall in the same published distance band.
  const sameName = new Map<string, number>();
  for (const target of targets) {
    const key = target.name;
    sameName.set(key, (sameName.get(key) ?? 0) + 1);
  }
  const seen = new Map<string, number>();
  return targets.map((target) => {
    const key = target.name;
    const total = sameName.get(key) ?? 1;
    const index = seen.get(key) ?? 0;
    seen.set(key, index + 1);
    const qualifier = total > 1 ? ` · ${targetPosition(index, total)}` : '';
    const distance = target.distance > 0 ? ` · within ${target.distance}` : '';
    return { value: target.target, text: `${target.name}${qualifier}${distance}` };
  });
}

/** A short near-to-far word for one of several same-named target rows. */
function targetPosition(index: number, total: number): string {
  if (total === 2) return index === 0 ? 'nearer' : 'farther';
  if (index === 0) return 'nearest';
  if (index === total - 1) return 'farthest';
  const suffix = index + 1 === 2 ? 'nd' : index + 1 === 3 ? 'rd' : 'th';
  return `${index + 1}${suffix} nearest`;
}

export function readMagic(f: Fields): MagicView {
  return {
    available: f.flag('available'),
    members: f.list('members', (entry) => ({
      index: entry.number('index'),
      member: entry.text('member'),
      name: entry.text('name'),
      class: entry.text('class'),
      spellPoints: entry.number('spellPoints'),
      spellPointsMax: entry.number('spellPointsMax'),
      quickSpell: entry.text('quickSpell'),
      quickSpellName: entry.text('quickSpellName'),
      spells: entry.list('spells', (spell) => ({
        spell: spell.text('spell'),
        name: spell.text('name'),
        school: spell.text('school'),
        tier: spell.text('tier'),
        tierRung: spell.number('tierRung'),
        cost: spell.number('cost'),
        targeting: spell.text('targeting', 'none'),
        effect: spell.text('effect'),
        aims: spell.list('aims', (aim) => ({ aim: aim.text('aim'), name: aim.text('name'), kind: aim.text('kind') })),
        targetSide: spell.text('targetSide'),
        canCast: spell.flag('canCast'),
      })),
      pages: entry.list('pages', (page) => ({
        school: page.text('school'),
        held: page.text('held'),
        spells: page.list('spells', (spell) => ({
          spell: spell.text('spell'),
          name: spell.text('name'),
          tier: spell.text('tier'),
          known: spell.flag('known'),
          cost: spell.number('cost'),
          refusalCode: spell.text('refusalCode'),
          refusal: spell.text('refusal'),
        })),
      })),
    })),
    targets: f.list('targets', (entry) => ({
      target: entry.text('target'),
      name: entry.text('name'),
      side: entry.text('side'),
      distance: entry.number('distance'),
      kind: entry.text('kind', 'actor'),
    })),
    outcome: f.text('outcome', 'none'),
    member: f.number('member'),
    caster: f.text('caster'),
    spell: f.text('spell'),
    cost: f.number('cost'),
    target: f.text('target'),
    effect: f.text('effect'),
    code: f.text('code'),
    message: f.text('message'),
    facts: f.list('facts', (fact) => ({ name: fact.text('name'), value: fact.text('value') })),
    running: f.list('running', (effect) => ({
      effect: effect.text('effect'),
      magnitude: effect.number('magnitude'),
      endsAt: effect.text('endsAt'),
    })),
    memberRunning: f.list('memberRunning', (effect) => ({
      member: effect.text('member'),
      name: effect.text('name'),
      effect: effect.text('effect'),
      magnitude: effect.number('magnitude'),
      endsAt: effect.text('endsAt'),
    })),
    items: f.list('items', (item) => ({
      item: item.text('item'),
      name: item.text('name'),
      kind: item.text('kind'),
      spell: item.text('spell'),
      spellName: item.text('spellName'),
      targeting: item.text('targeting'),
      charges: item.number('charges'),
      chargesMax: item.number('chargesMax'),
      wielded: item.flag('wielded'),
      member: item.text('member'),
      targetSide: item.text('targetSide'),
      canUse: item.flag('canUse'),
    })),
    source: f.text('source'),
    sight: f.text('sight'),
  };
}
