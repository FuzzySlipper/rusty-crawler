/**
 * What each member has earned and what a level would cost: the level, the experience banked against the curve the
 * ruleset states, the points held, and the fee the counter the party stands at would charge. The train control is
 * offered per member and only where the product says a counter here trains them; the level it names is the one the
 * training step itself would reach.
 */

import type { Fields } from './reader.js';
import { ACTIONS } from './actions.js';
import { button, element, head, plural, redrawGuard, report, result, section, type Host, type Section } from './dom.js';

/** One member's growth: the level, what earned it, and what the next costs — every number the product's own. */
export interface ProgressionMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly level: number;
  /** How much experience the member has earned in total. */
  readonly experience: number;
  /** How many skill points the member holds unspent. */
  readonly skillPoints: number;
  /** How much experience the member's next level takes. */
  readonly nextLevelExperience: number;
  /** The level one training step would leave the member at. */
  readonly nextLevel: number;
  /** What the counter the party stands at would charge this member for a level; zero when none trains. */
  readonly fee: number;
  /** The highest level that counter trains to; zero when none trains. */
  readonly cap: number;
  /** Whether a counter here trains this member at all, which is when the train control is offered. */
  readonly canTrain: boolean;
}

/** What the party has earned and what a level costs. `available` is false when the session holds no owner. */
export interface ProgressionView {
  readonly available: boolean;
  readonly members: readonly ProgressionMemberView[];
  /** What the last progression event was: `none`, `awarded`, `trained`, or `refused`. */
  readonly outcome: string;
  /** What the last award came from, or the counter that last trained somebody. */
  readonly source: string;
  /** How much experience the last award was worth, zero when the last event was not one. */
  readonly earned: number;
  readonly code: string;
  readonly message: string;
}

export function readProgression(f: Fields): ProgressionView {
  return {
    available: f.flag('available'),
    members: f.list('members', (entry) => ({
      index: entry.number('index'),
      member: entry.text('member'),
      name: entry.text('name'),
      level: entry.number('level'),
      experience: entry.number('experience'),
      skillPoints: entry.number('skillPoints'),
      nextLevelExperience: entry.number('nextLevelExperience'),
      nextLevel: entry.number('nextLevel'),
      fee: entry.number('fee'),
      cap: entry.number('cap'),
      canTrain: entry.flag('canTrain'),
    })),
    outcome: f.text('outcome', 'none'),
    source: f.text('source'),
    earned: f.number('earned'),
    code: f.text('code'),
    message: f.text('message'),
  };
}

/** Mounts the levels section. */
export function mountProgression(host: Host): Section<ProgressionView> {
  const { panel, claim } = host;
  const progression = section('crawler-progression');
  const state = element('p', 'crawler-progression-state');
  const members = element('div', 'crawler-progression-members');
  const outcome = result('crawler-progression-result');
  progression.append(head('Experience and levels'), state, members, outcome);
  const changed = redrawGuard();

  const render = (view: ProgressionView): void => {
    panel.dataset.progression = view.available ? 'present' : 'none';
    panel.dataset.progressionOutcome = view.outcome;
    progression.hidden = !view.available;
    progression.dataset.source = view.source;
    state.textContent = view.available ? `${view.members.length} character${plural(view.members.length)}` : '';
    report(outcome, view.outcome, view.code, view.message);
    if (!changed(view)) return;

    members.replaceChildren(
      ...view.members.map((member) => {
        const row = element('div', 'crawler-progression-member');
        row.dataset.member = member.member;
        row.dataset.level = String(member.level);
        const label = element('span', 'crawler-row-label');
        // The curve is the product's own figure, so "how far along" is printed rather than worked out.
        label.textContent = `${member.name} — level ${member.level} — ${member.experience}/${member.nextLevelExperience} xp — ${member.skillPoints} skill point${plural(member.skillPoints)}`;
        row.append(label);
        if (member.canTrain) {
          const train = button(`Train to level ${member.nextLevel} for ${member.fee} gold (up to ${member.cap})`, 'crawler-train');
          train.addEventListener('click', () => claim(ACTIONS.serviceTrain, { member: member.index }));
          row.append(train);
        }

        return row;
      }),
    );
  };

  return { element: progression, render };
}
