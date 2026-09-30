/**
 * The skills, under the levels they were bought with: each member's own rows, the ceiling their class and rank
 * impose, and one control per row that raises it. The panel prints the price and the refusal the product worked out
 * and raises nothing itself, which keeps one spend path in the product rather than one per screen: a row the
 * product would refuse carries the refusal and no control, and a row it would accept carries the price it quoted.
 */

import type { Fields } from './reader.js';
import { ACTIONS } from './actions.js';
import { button, element, head, plural, redrawGuard, report, result, section, type Host, type Section } from './dom.js';

/** One skill a member holds. */
export interface SkillRowView {
  readonly skill: string;
  /** `weapon`, `armour`, `magic`, `miscellaneous`, or `unused`. */
  readonly block: string;
  readonly level: number;
  /** What the rung the member stands at is called, in the product's own word. */
  readonly tier: string;
  readonly ceilingLevel: number;
  /** What the highest rung the member's class and rank allow is called. */
  readonly ceilingTier: string;
  readonly pointsSpent: number;
  /** The level one more point would leave the skill at, as the product worked it out. */
  readonly reached: number;
  /** What one more level would cost, zero when it would be refused. */
  readonly cost: number;
  /** Why one more level would be refused, empty when it would land. */
  readonly refusal: string;
  /** The refusal's code, empty when it would land. */
  readonly refusalCode: string;
}

/** One member's skills. */
export interface SkillMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly class: string;
  readonly rank: number;
  readonly skills: readonly SkillRowView[];
}

/** What every member can hold and what the next point would buy. `available` is false with no skill policy. */
export interface SkillsView {
  readonly available: boolean;
  readonly members: readonly SkillMemberView[];
  /** What the last raise was: `none`, `raised`, or `refused`. */
  readonly outcome: string;
  readonly member: string;
  readonly skill: string;
  readonly level: number;
  readonly cost: number;
  readonly code: string;
  readonly message: string;
}

export function readSkills(f: Fields): SkillsView {
  return {
    available: f.flag('available'),
    members: f.list('members', (entry) => ({
      index: entry.number('index'),
      member: entry.text('member'),
      name: entry.text('name'),
      class: entry.text('class'),
      rank: entry.number('rank'),
      skills: entry.list('skills', (row) => ({
        skill: row.text('skill'),
        block: row.text('block'),
        level: row.number('level'),
        tier: row.text('tier'),
        ceilingLevel: row.number('ceilingLevel'),
        ceilingTier: row.text('ceilingTier'),
        pointsSpent: row.number('pointsSpent'),
        reached: row.number('reached'),
        cost: row.number('cost'),
        refusal: row.text('refusal'),
        refusalCode: row.text('refusalCode'),
      })),
    })),
    outcome: f.text('outcome', 'none'),
    member: f.text('member'),
    skill: f.text('skill'),
    level: f.number('level'),
    cost: f.number('cost'),
    code: f.text('code'),
    message: f.text('message'),
  };
}

/** Mounts the skills section. */
export function mountSkills(host: Host): Section<SkillsView> {
  const { panel, claim } = host;
  const skills = section('crawler-skills');
  const state = element('p', 'crawler-skills-state');
  const members = element('div', 'crawler-skills-members');
  const outcome = result('crawler-skills-result');
  skills.append(head('Skills'), state, members, outcome);
  const changed = redrawGuard();

  const render = (view: SkillsView): void => {
    panel.dataset.skills = view.available ? 'present' : 'none';
    panel.dataset.skillsOutcome = view.outcome;
    skills.hidden = !view.available;
    state.textContent = view.available ? `${view.members.length} character${plural(view.members.length)}` : '';
    report(outcome, view.outcome, view.code, view.message);
    if (!changed(view)) return;

    members.replaceChildren(
      ...view.members.map((member) => {
        const block = element('div', 'crawler-skills-member');
        block.dataset.member = member.member;
        const label = element('span', 'crawler-row-label');
        label.textContent = `${member.name} — ${member.class} of rank ${member.rank}`;
        block.append(label);
        for (const row of member.skills) {
          const line = element('div', 'crawler-skill');
          line.dataset.skill = row.skill;
          line.dataset.block = row.block;
          line.dataset.tier = row.tier;
          line.dataset.refusal = row.refusal;
          line.dataset.refusalCode = row.refusalCode;
          const text = element('span');
          // The skill, the rung it stands at, how far it has come against the ceiling the product published, and
          // why there is no next point when there is none.
          const reading = `${row.skill} · ${row.block} · ${row.tier} · ${row.level}/${row.ceilingLevel} (${row.ceilingTier})`;
          text.textContent = row.refusal === '' ? reading : `${reading} · ${row.refusal}`;
          if (row.refusal !== '') text.className = 'crawler-skill-refusal';
          line.append(text);
          if (row.refusal === '') {
            const raise = button(`Raise to ${row.reached} for ${row.cost} point${plural(row.cost)}`, 'crawler-raise');
            raise.addEventListener('click', () => claim(ACTIONS.raiseSkill, { member: member.index, skill: row.skill }));
            line.append(raise);
          }

          block.append(line);
        }

        return block;
      }),
    );
  };

  return { element: skills, render };
}
