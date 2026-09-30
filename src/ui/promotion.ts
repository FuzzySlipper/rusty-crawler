/**
 * The ranks, between the levels and the skills, because a promotion is what raises the ceiling the skill rows are
 * read against: each member's class and rank, every rank that class leads to, who gives it, and what it asks for.
 * Nothing here is a control — a rank is taken from the person who gives it, in the conversation the party reaches
 * them through — so the section shows the ladder and what the last rank did, redrawn from every projection.
 */

import type { Fields } from './reader.js';
import { element, head, plural, report, result, section, type Host, type Section } from './dom.js';

/** One thing a rank asks for: what it is and how it reads. */
export interface PromotionRequirementView {
  /** What kind of thing it is: `giver`, `item`, `award`, or `quest`. */
  readonly kind: string;
  readonly name: string;
  readonly label: string;
  readonly amount: number;
  readonly text: string;
}

/** One rank a class leads to. */
export interface PromotionRankView {
  readonly promotion: string;
  readonly toClass: string;
  readonly rank: number;
  /** The alternative it takes: `light`, `dark`, or empty for a class's first promotion. */
  readonly choice: string;
  readonly giver: string;
  readonly giverName: string;
  readonly words: string;
  readonly requirements: readonly PromotionRequirementView[];
}

/** One member and the ranks its class leads to. */
export interface PromotionMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly class: string;
  readonly rank: number;
  readonly promotions: readonly PromotionRankView[];
}

/** One member a rank was given to. */
export interface PromotionGrantView {
  readonly member: string;
  readonly name: string;
  readonly fromClass: string;
  readonly fromRank: number;
  readonly toClass: string;
  readonly rank: number;
  readonly choice: string;
  readonly met: readonly string[];
}

/** One member a rank was not given to, and what they were missing. */
export interface PromotionDenialView {
  readonly member: string;
  readonly name: string;
  readonly class: string;
  readonly rank: number;
  readonly missing: readonly string[];
}

/**
 * What the party may become and what its last rank did. `available` is false when the session's ruleset stated no
 * ladder, and a party at the top of its ladder is a different reading again: the block is there and no class leads
 * anywhere.
 */
export interface PromotionView {
  readonly available: boolean;
  readonly members: readonly PromotionMemberView[];
  /** What the last rank was: `none`, `granted`, or `refused`. */
  readonly outcome: string;
  readonly promotion: string;
  readonly toClass: string;
  readonly rank: number;
  readonly choice: string;
  readonly granted: readonly PromotionGrantView[];
  readonly denied: readonly PromotionDenialView[];
  readonly code: string;
  readonly message: string;
}

export function readPromotion(f: Fields): PromotionView {
  return {
    available: f.flag('available'),
    members: f.list('members', (entry) => ({
      index: entry.number('index'),
      member: entry.text('member'),
      name: entry.text('name'),
      class: entry.text('class'),
      rank: entry.number('rank'),
      promotions: entry.list('promotions', (row) => ({
        promotion: row.text('promotion'),
        toClass: row.text('toClass'),
        rank: row.number('rank'),
        choice: row.text('choice'),
        giver: row.text('giver'),
        giverName: row.text('giverName'),
        words: row.text('words'),
        requirements: row.list('requirements', (requirement) => ({
          kind: requirement.text('kind'),
          name: requirement.text('name'),
          label: requirement.text('label'),
          amount: requirement.number('amount'),
          text: requirement.text('text'),
        })),
      })),
    })),
    outcome: f.text('outcome', 'none'),
    promotion: f.text('promotion'),
    toClass: f.text('toClass'),
    rank: f.number('rank'),
    choice: f.text('choice'),
    granted: f.list('granted', (entry) => ({
      member: entry.text('member'),
      name: entry.text('name'),
      fromClass: entry.text('fromClass'),
      fromRank: entry.number('fromRank'),
      toClass: entry.text('toClass'),
      rank: entry.number('rank'),
      choice: entry.text('choice'),
      met: entry.words('met'),
    })),
    denied: f.list('denied', (entry) => ({
      member: entry.text('member'),
      name: entry.text('name'),
      class: entry.text('class'),
      rank: entry.number('rank'),
      missing: entry.words('missing'),
    })),
    code: f.text('code'),
    message: f.text('message'),
  };
}

/** Mounts the ranks section. */
export function mountPromotion(host: Host): Section<PromotionView> {
  const { panel } = host;
  const promotion = section('crawler-promotion');
  const state = element('p', 'crawler-promotion-state');
  const members = element('div', 'crawler-promotion-members');
  const outcome = result('crawler-promotion-result');
  promotion.append(head('Ranks'), state, members, outcome);

  /**
   * Every row is the product's own: which class each rank names, which alternative it takes, who gives it, and
   * what it asks for. What was met and what was missing is the owner's own report, printed rather than worked out.
   */
  const render = (view: PromotionView): void => {
    panel.dataset.promotion = view.available ? 'present' : 'none';
    panel.dataset.promotionOutcome = view.outcome;
    promotion.hidden = !view.available;
    promotion.dataset.promotion = view.promotion;
    promotion.dataset.toClass = view.toClass;
    promotion.dataset.rank = String(view.rank);
    promotion.dataset.choice = view.choice;
    state.textContent = view.available ? `${view.members.length} character${plural(view.members.length)}` : '';
    report(outcome, view.outcome, view.code, view.message);

    members.replaceChildren(
      ...view.members.map((member) => {
        const block = element('div', 'crawler-promotion-member');
        block.dataset.member = member.member;
        block.dataset.class = member.class;
        block.dataset.rank = String(member.rank);
        const label = element('span', 'crawler-row-label');
        label.textContent = `${member.name} — ${member.class} of rank ${member.rank}`;
        block.append(label);
        if (member.promotions.length === 0) {
          const top = element('div', 'crawler-promotion-rank');
          top.textContent = 'No rank leads on from here.';
          block.append(top);
          return block;
        }

        for (const rank of member.promotions) {
          const line = element('div', 'crawler-promotion-rank');
          line.dataset.promotion = rank.promotion;
          line.dataset.toClass = rank.toClass;
          line.dataset.choice = rank.choice;
          line.dataset.giver = rank.giver;
          line.title = rank.words;
          const text = element('span');
          // The rank as a player reads it: what it makes them, which alternative it is, who gives it, and
          // everything it asks for in the ladder's own words.
          const alternative = rank.choice === '' ? '' : ` · the ${rank.choice} path`;
          const asks = rank.requirements.map((requirement) => requirement.text).join('; ');
          text.textContent = `${rank.toClass} (rank ${rank.rank})${alternative} · granted by ${rank.giverName} · asks for ${asks}`;
          line.append(text);
          block.append(line);
        }

        return block;
      }),
    );

    // What the last rank did: who rose and what each met, and who it passed over and what they were missing, which
    // is where a player reads what to bring next.
    const record = element('div', 'crawler-promotion-granted');
    for (const grant of view.granted) {
      const line = element('div', 'crawler-promotion-grant');
      line.dataset.member = grant.member;
      line.textContent = `${grant.name} rose from ${grant.fromClass} (rank ${grant.fromRank}) to ${grant.toClass} (rank ${grant.rank})${
        grant.choice === '' ? '' : `, the ${grant.choice} path`
      }: met ${grant.met.join('; ')}`;
      record.append(line);
    }

    for (const denial of view.denied) {
      const line = element('div', 'crawler-promotion-denial');
      line.dataset.member = denial.member;
      line.textContent = `${denial.name}, a ${denial.class} of rank ${denial.rank}, was missing ${denial.missing.join('; ')}`;
      record.append(line);
    }

    if (record.childElementCount > 0) members.append(record);
  };

  return { element: promotion, render };
}
